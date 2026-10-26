//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous;

using MiscUtil.Conversion;

namespace Antmicro.Renode.Peripherals.Crypto;

public class Caliptra_SHA512_384_Accelerator : BasicDoubleWordPeripheral, IKnownSize
{
    public Caliptra_SHA512_384_Accelerator(IMachine machine, Caliptra_Mailbox mbox) : base(machine)
    {
        DefineRegisters();
        this.mbox = mbox;
        digest = new byte[64];
    }

    public override void Reset()
    {
        base.Reset();
        Array.Clear(digest);
    }

    public long Size => 0xA14;

    private void DefineRegisters()
    {
        Registers.Lock.Define(this, 0x1)
            .WithFlag(0,
                out @lock,
                FieldMode.ReadToSet | FieldMode.WriteOneToClear,
                name: "Lock",
                writeCallback: (_, val) =>
                {
                    // Clear lock
                    if(val)
                    {
                        execFlag.Value = false;
                        valid.Value = false;
                        Array.Clear(digest);
                    }
                }
            )
            .WithReservedBits(1, 31);

        // AXI user
        Registers.User.Define(this)
            .WithTag("User", 0, 32); // RO

        Registers.Mode.Define(this)
            .WithEnumField(0, 2, out mode, name: "Mode")
            .WithEnumField(2, 1, out endianToggle, name: "Endian toggle")
            .WithReservedBits(3, 29);

        Registers.StartAddress.Define(this)
            .WithValueField(0, 32, out startAddress, name: "Start address");

        Registers.DataLength.Define(this)
            .WithValueField(0, 32, out dataLength, name: "Data length",
                writeCallback: (_, val) =>
                {
                    remainingDataLength = (int)val;
                }
            );

        Registers.DataIn.Define(this)
            .WithValueField(0, 32, out dataIn, name: "Data in (streaming)",
                writeCallback: (_, _) =>
                {
                    if(mode.Value != Mode.SHA_STREAM_384 && mode.Value != Mode.SHA_STREAM_512)
                    {
                        this.WarningLog("DataIn streaming register written to despite mode {0}, ignoring write", mode);
                        return;
                    }

                    if(remainingDataLength <= 0)
                    {
                        this.WarningLog("DataIn streaming register written to with more data than declared, ignoring write");
                        return;
                    }
                    FeedHasher(fromMbox: false);
                }
            );

        Registers.Execute.Define(this)
            .WithFlag(0, name: "Execute", flagField: out execFlag, writeCallback: (_, val) =>
            {
                if(!val)
                {
                    return;
                }
                // The hasher is initialized when the first block of data is fed into it. When hashing zero-length data in streaming mode, initialize it here.
                if(hasher == null)
                {
                    InitHasher();
                }
                switch(mode.Value)
                {
                case Mode.SHA_STREAM_384 or Mode.SHA_STREAM_512:
                    // Handled by write callbacks
                    break;
                case Mode.SHA_MBOX_384 or Mode.SHA_MBOX_512:
                    FeedHasher(fromMbox: true);
                    break;
                }
                FinalizeHash();
            })
            .WithReservedBits(1, 31);

        Registers.Status.Define(this) // RO
            .WithFlag(0, out valid, FieldMode.Read, name: "Valid")
            .WithTaggedFlag("SoC has lock", 1)
            .WithReservedBits(2, 30);

        Registers.Digest0.DefineMany(this, 16,
            (reg, regIdx) =>
            {
                reg.WithValueField(0, 32, FieldMode.Read, name: $"Digest[{regIdx}]",
                        valueProviderCallback: (_) => BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(regIdx * 4))
                    );
            },
            name: "Digest"
        );

        Registers.Control.Define(this)
            .WithFlag(0, name: "Zeroize", writeCallback: (_, _) =>
                {
                    this.InfoLog("Zeroed SHA accelerator internal registers");
                    Zeroize();
                },
                valueProviderCallback: _ => false
            );
    }

    private void Zeroize()
    {
        Array.Clear(digest);
        hasher = null;
    }

    private void InitHasher()
    {
        var algo = mode.Value switch
        {
            Mode.SHA_STREAM_384 or Mode.SHA_MBOX_384 => HashAlgorithmName.SHA384,
            Mode.SHA_STREAM_512 or Mode.SHA_MBOX_512 => HashAlgorithmName.SHA512,
            _ => throw new UnreachableException()
        };
        Array.Clear(digest);
        hasher = IncrementalHash.CreateHash(algo);
        valid.Value = false;
    }

    private void FeedHasher(bool fromMbox)
    {
        if(hasher == null)
        {
            InitHasher();
        }
        if(remainingDataLength <= 0)
        {
            return;
        }

        byte[] bytes;
        int numberOfBytesToAppend = 0;
        if(fromMbox)
        {
            DebugHelper.Assert(mode.Value == Mode.SHA_MBOX_384 || mode.Value == Mode.SHA_MBOX_512);
            if(!(startAddress.Value % 4 == 0))
            {
                this.ErrorLog("Start address not aligned to 4 bytes in mailbox mode, aborting");
                return;
            }

            var bytesToRead = dataLength.Value;
            var rawMem = mbox.GetInternalBackingMemory();
            if(!((long)(bytesToRead + startAddress.Value) < rawMem.Size))
            {
                var prevBytesToRead = bytesToRead;
                bytesToRead = (ulong)rawMem.Size - startAddress.Value;
                this.WarningLog("Supplied data length {0} would result in overflow, only reading to end of mailbox, {1} bytes", prevBytesToRead, bytesToRead);
            }

            numberOfBytesToAppend = (int)bytesToRead;

            // We need to fetch full doublewords, the unused bytes get cut off later (via numberOfBytesToAppend)
            // Assuming the memory size is a multiple of 4 and the start address is 4-byte-aligned, this is safe - we'll at most round up to the memory size
            // In hardware, masking of unused bytes is the last step *after* endianness conversion, so rounding up is required
            if(bytesToRead % 4 != 0)
            {
                bytesToRead += 4 - (bytesToRead % 4);
            }

            // We don't know what the internal endianness of the underlying IMemory might be,
            // so read full doublewords and only then convert to bytes, instead of using `ReadBytes`
            bytes = Enumerable.Range(0, (int)(bytesToRead / 4))
                .Select(k => rawMem.ReadDoubleWord((int)startAddress.Value + k * 4))
                // Treat the read bytes `AS_IS` and later convert if `SWIZZLE` is set,
                // simulating Caliptra's expectation that bytes from memory are in little-endian order
                .SelectMany(v => EndianBitConverter.Big.GetBytes(v))
                .ToArray();
        }
        else // from DataIn register
        {
            // The cast to uint is crucial here:
            // The dataIn register is 4 bytes long, but Renode's underlying register values are ulongs - 8 bytes long
            // Without the cast, we would be appending too many bytes due to the `GetBytes` conversion
            bytes = EndianBitConverter.Big.GetBytes((uint)dataIn.Value);
            numberOfBytesToAppend = Math.Min(bytes.Length, remainingDataLength);
        }

        if(endianToggle.Value == DataInEndianness.SWIZZLE)
        {
            DebugHelper.Assert(bytes.Length % 4 == 0);
            var asDWords = MemoryMarshal.Cast<byte, uint>(bytes.AsSpan());
            BinaryPrimitives.ReverseEndianness(asDWords, asDWords);
        }

        remainingDataLength -= numberOfBytesToAppend;
        hasher.AppendData(bytes, 0, numberOfBytesToAppend);
    }

    private void FinalizeHash()
    {
        Array.Clear(digest);
        hasher.GetCurrentHash().CopyTo(digest, 0);
        valid.Value = true;
        hasher = null;
    }

    private IFlagRegisterField valid;

    private IncrementalHash hasher;
    private int remainingDataLength;
    private IValueRegisterField dataLength;
    private IValueRegisterField dataIn;
    private IEnumRegisterField<DataInEndianness> endianToggle;
    private IValueRegisterField startAddress;

    private IEnumRegisterField<Mode> mode;
    private IFlagRegisterField @lock;
    private IFlagRegisterField execFlag;
    private readonly byte[] digest;
    private readonly Caliptra_Mailbox mbox;

    private const int InterruptBlockBase = 0x800;

    public enum DataInEndianness
    {
        SWIZZLE = 0x0,
        AS_IS = 0x1,
    }

    public enum Mode
    {
        SHA_STREAM_384 = 0b00,
        SHA_STREAM_512 = 0b01,
        SHA_MBOX_384 = 0b10,
        SHA_MBOX_512 = 0b11,
    }

    public enum Registers
    {
        Lock         = 0x000,
        User         = 0x004,
        Mode         = 0x008,
        StartAddress = 0x00C,
        DataLength   = 0x010,
        DataIn       = 0x014,
        Execute      = 0x018,
        Status       = 0x1C,
        Digest0      = 0x020,
        Digest1      = 0x024,
        Digest2      = 0x028,
        Digest3      = 0x02C,
        Digest4      = 0x030,
        Digest5      = 0x034,
        Digest6      = 0x038,
        Digest7      = 0x03C,
        Digest8      = 0x040,
        Digest9      = 0x044,
        Digest10     = 0x048,
        Digest11     = 0x04C,
        Digest12     = 0x050,
        Digest13     = 0x054,
        Digest14     = 0x058,
        Digest15     = 0x05C,
        Control      = 0x060,

        GlobalIntrEn              = InterruptBlockBase + 0x000,
        ErrorIntrEn               = InterruptBlockBase + 0x004,
        NotifIntrEn               = InterruptBlockBase + 0x008,
        ErrorGlobalIntr           = InterruptBlockBase + 0x00C,
        NotifGlobalIntr           = InterruptBlockBase + 0x010,
        ErrorInternalIntr         = InterruptBlockBase + 0x018,
        ErrorIntrTrig             = InterruptBlockBase + 0x01C,
        NotifIntrTrig             = InterruptBlockBase + 0x020,
        // Unused/Reserved 0x024
        Error0IntrCount           = InterruptBlockBase + 0x100,
        Error1IntrCount           = InterruptBlockBase + 0x104,
        Error2IntrCount           = InterruptBlockBase + 0x10C,
        // Unused/Reserved 0x110,
        NotifCmdDoneIntrCount     = InterruptBlockBase + 0x180,
        // Unused/Reserved 0x184
        Error0IntrCountIncr       = InterruptBlockBase + 0x200,
        Error1IntrCountIncr       = InterruptBlockBase + 0x204,
        Error2IntrCountIncr       = InterruptBlockBase + 0x208,
        Error3IntrCountIncr       = InterruptBlockBase + 0x20C,
        NotifCmdDoneIntrCountIncr = InterruptBlockBase + 0x210,
    };
}
