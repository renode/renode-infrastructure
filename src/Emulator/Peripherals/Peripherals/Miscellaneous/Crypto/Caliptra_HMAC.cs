//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.Crypto;

public class Caliptra_HMAC : BasicDoubleWordPeripheral, IKnownSize
{
    public Caliptra_HMAC(IMachine machine, Caliptra_KeyVault keyVault) : base(machine)
    {
        this.keyVault = keyVault;

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();

        hmac = null;
        Array.Clear(hmacKey);
        Array.Clear(hmacBlock);
        Array.Clear(hmacTag);
    }

    public long Size => 0xA14;

    private static IEnumerable<byte> SwapEndiannessByWord(IEnumerable<byte> reg) => reg.Chunk(4).SelectMany(e => e.Reverse());

    private static IEnumerable<byte> ToBytes(uint[] reg) => reg.SelectMany(e => BitConverter.GetBytes(e));

    private void DefineRegisters()
    {
        Registers.Name.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(HmacCoreName >> (idx * 32)),
                name: $"NAME_{idx}");
        });

        Registers.Version.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(HmacCoreVersion >> (idx * 32)),
                name: $"VERSION_{idx}");
        });

        Registers.Control.Define(this, 0x8)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        InitEngine();
                    }
                },
                name: "INIT")
            .WithFlag(1, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        RunEngine();
                    }
                },
                name: "NEXT")
            .WithFlag(2, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        Zeroize();
                    }
                },
                name: "ZEROIZE")
            .WithEnumField(3, 1, out mode, FieldMode.Write, name: "MODE")
            .WithFlag(4, out csrMode, FieldMode.Write, name: "CSR_MODE")
            .WithReservedBits(5, 27);

        Registers.Status.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out valid, FieldMode.Read, name: "VALID")
            .WithReservedBits(2, 30);

        Registers.Key.DefineMany(this, (uint)hmacKey.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write,
                writeCallback: (_, val) => hmacKey[idx] = (uint)val,
                name: $"KEY_{idx}");
        });

        Registers.Block.DefineMany(this, (uint)hmacBlock.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write,
                writeCallback: (_, val) => hmacBlock[idx] = (uint)val,
                name: $"BLOCK_{idx}");
        });

        Registers.Tag.DefineMany(this, (uint)hmacTag.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: val => hmacTag[idx],
                name: $"TAG_{idx}");
        });

        // Seed is used by hardware for SCA countermeasures (no action in functional simulation)
        // and has no effect on the computed digest value
        Registers.EntropySeed.DefineMany(this, 12, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, name: $"LFSR_SEED_{idx}");
        });

        Registers.KeyvaultReadKeyControl.Define(this)
            .WithFlag(0, out kvKeyRead,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKeyvaultKeyRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out kvKeyReadSlot, name: "read_entry")
            .WithReservedBits(6, 26);

        Registers.KeyvaultReadKeyStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "ready")
            .WithFlag(1, out kvKeyReadValid, FieldMode.Read, name: "valid")
            .WithEnumField(2, 8, out kvKeyReadStatus, FieldMode.Read, name: "error")
            .WithReservedBits(10, 22);

        Registers.KeyvaultReadBlockControl.Define(this)
            .WithFlag(0, out kvBlockRead,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKeyvaultBlockRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out kvBlockReadSlot, name: "read_entry")
            .WithReservedBits(6, 26);

        Registers.KeyvaultReadBlockStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "ready")
            .WithFlag(1, out kvBlockReadValid, FieldMode.Read, name: "valid")
            .WithEnumField(2, 8, out kvBlockReadStatus, FieldMode.Read, name: "error")
            .WithReservedBits(10, 22);

        Registers.KeyvaultWriteControl.Define(this)
            .WithFlag(0, out kvTagWrite, name: "write_en")
            .WithValueField(1, 5, out kvTagWriteSlot, name: "write_entry")
            .WithEnumField(6, 9, out kvTagWriteAllowed, name: "dest_valid")
            .WithReservedBits(15, 17);

        Registers.KeyvaultWriteStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "ready")
            .WithFlag(1, out kvTagWriteValid, FieldMode.Read, name: "valid")
            .WithEnumField(2, 8, out kvTagWriteStatus, FieldMode.Read, name: "error")
            .WithReservedBits(10, 22);
    }

    private void HandleKeyvaultKeyRead()
    {
        kvKeyReadValid.Value = true;
        if(!keyVault.TryGetKey((uint)kvKeyReadSlot.Value, out var keyBytes, Caliptra_KeyVault.DestinationCode.HmacKey))
        {
            kvKeyReadStatus.Value = KeyvalutOperationStatus.ReadFail;
            this.Log(LogLevel.Warning, "Reading key from KeyVault slot {0} failed", kvKeyReadSlot.Value);
            return;
        }

        var items = BitHelper.ToUInt32Array(keyBytes, false);
        for(int i = 0; i < items.Length; ++i)
        {
            hmacKey[i] = items[i];
        }
        kvKeyReadStatus.Value = KeyvalutOperationStatus.Success;
    }

    private void HandleKeyvaultBlockRead()
    {
        kvBlockReadValid.Value = true;
        if(!keyVault.TryGetKey((uint)kvBlockReadSlot.Value, out var blockBytes, Caliptra_KeyVault.DestinationCode.HmacBlock))
        {
            kvBlockReadStatus.Value = KeyvalutOperationStatus.ReadFail;
            this.Log(LogLevel.Warning, "Reading block from KeyVault slot {0} failed", kvBlockRead.Value);
            return;
        }

        var items = BitHelper.ToUInt32Array(blockBytes, false);
        for(int i = 0; i < items.Length; ++i)
        {
            hmacBlock[i] = items[i];
        }
        kvBlockReadStatus.Value = KeyvalutOperationStatus.Success;
    }

    private void InitEngine()
    {
        valid.Value = false;

        byte[] keyBytes;
        if(csrMode.Value)
        {
            keyBytes = ToBytes(defaultCsrHmacKey).ToArray();
        }
        else
        {
            keyBytes = ToBytes(hmacKey).ToArray();
        }

        // SHA384 uses a shorter key
        if(mode.Value == HMACMode.HMAC384)
        {
            keyBytes = keyBytes[..(12 * sizeof(uint))];
        }
        hmac = mode.Value switch
        {
            HMACMode.HMAC384 => new CaliptraHmacEngine(new Sha384Digest()),
            HMACMode.HMAC512 => new CaliptraHmacEngine(new Sha512Digest()),
            _ => throw new UnreachableException(),
        };
        hmac.Init(keyBytes);
        RunEngine();
    }

    private void RunEngine()
    {
        valid.Value = false;

        if(hmac is null)
        {
            this.Log(LogLevel.Warning, "HMAC engine not initialized. Hash will not be computed");
            return;
        }
        byte[] blockBytes = ToBytes(hmacBlock).ToArray();

        var digest = new byte[TagLength * sizeof(uint)];
        try
        {
            hmac.Next(blockBytes);
            digest = hmac.Tag;
        }
        catch(Exception e)
        {
            this.Log(LogLevel.Error, "Error when computing the hash: {0}", e.Message);
            return;
        }

        if(kvTagWrite.Value)
        {
            if(!keyVault.TrySetKey((uint)kvTagWriteSlot.Value, digest, kvTagWriteAllowed.Value))
            {
                kvTagWriteStatus.Value = KeyvalutOperationStatus.WriteFail;
                this.Log(LogLevel.Warning, "Writing tag to KeyVault slot {0} failed", kvTagWriteSlot.Value);
            }
            kvTagWriteValid.Value = true;
            kvTagWriteStatus.Value = KeyvalutOperationStatus.Success;
        }
        // Do not output the tag if the secret (key or block) was obtained from the Keyvault
        else if(!kvKeyReadValid.Value && !kvBlockReadValid.Value)
        {
            Array.Copy(BitHelper.ToUInt32Array(digest, false), hmacTag, TagLength);
        }

        valid.Value = true;
    }

    private void Zeroize()
    {
        this.Log(LogLevel.Info, "Zeroize requested");
        this.Reset();
    }

    private IFlagRegisterField kvKeyRead;
    private IValueRegisterField kvKeyReadSlot;

    private IFlagRegisterField kvKeyReadValid;
    private IEnumRegisterField<KeyvalutOperationStatus> kvKeyReadStatus;

    private IFlagRegisterField kvBlockRead;
    private IValueRegisterField kvBlockReadSlot;

    private IFlagRegisterField kvBlockReadValid;
    private IEnumRegisterField<KeyvalutOperationStatus> kvBlockReadStatus;

    private IFlagRegisterField kvTagWrite;
    private IValueRegisterField kvTagWriteSlot;
    private IEnumRegisterField<Caliptra_KeyVault.DestinationCode> kvTagWriteAllowed;

    private IFlagRegisterField kvTagWriteValid;
    private IEnumRegisterField<KeyvalutOperationStatus> kvTagWriteStatus;

    private IFlagRegisterField valid;
    private IFlagRegisterField csrMode;
    private IEnumRegisterField<HMACMode> mode;

    private CaliptraHmacEngine hmac;

    private readonly Caliptra_KeyVault keyVault;

    private readonly uint[] defaultCsrHmacKey = [
        0x14552AD, 0x19550757, 0x50C602DD, 0x85DE4E9B, 0x815CC9EF, 0xBA81A35, 0x7A05D7C0, 0x7F5EFAEB,
        0xF76DD9D2, 0x9E38197F, 0x4052537, 0x25B568F4, 0x432665F1, 0xD11D02A7, 0xBFB9279F, 0xA2EB96D7,
    ];

    private readonly uint[] hmacKey = new uint[KeyLength];
    private readonly uint[] hmacBlock = new uint[BlockLength];
    private readonly uint[] hmacTag = new uint[TagLength];

    private const int KeyLength = 16;
    private const int BlockLength = BlockSizeInBytes / sizeof(uint);
    private const int TagLength = TagSizeInBytes / sizeof(uint);

    private const int BlockSizeInBytes = 128;
    private const int TagSizeInBytes = 64;

    private const ulong HmacCoreName    = 0x61327368_6163686d; // "hmacsha2"
    private const ulong HmacCoreVersion = 0x00000000_3030322e; // "2.00"

    private class CaliptraHmacEngine
    {
        public CaliptraHmacEngine(LongDigest digest)
        {
            this.hashInner = digest;
            this.hashOuter = (LongDigest)digest.Copy();
        }

        public void Init(byte[] key)
        {
            key = SwapEndiannessByWord(key).ToArray();

            var ipadBlock = new byte[BlockSizeInBytes];
            opadBlock = new byte[BlockSizeInBytes];
            Array.Fill(ipadBlock, Ipad);
            Array.Fill(opadBlock, Opad);

            for(int i = 0; i < key.Length; ++i)
            {
                ipadBlock[i] ^= key[i];
                opadBlock[i] ^= key[i];
            }

            hashInner.BlockUpdate(ipadBlock);
        }

        public void Next(byte[] block)
        {
            block = SwapEndiannessByWord(block).ToArray();
            hashInner.BlockUpdate(block);

            hashOuter.BlockUpdate(opadBlock);

            // DoFinal is destructive and applies padding. To keep accumulating data
            // we need to extract raw internal state
            var innerTag = new byte[BlockSizeInBytes];
            hashInner.GetCurrentDigest(innerTag, 0);

            hashOuter.BlockUpdate(innerTag, 0, hashInner.GetDigestSize());
            Array.Clear(Tag);
            // DoFinal is destructive and resets the engine state. This is fine
            // Also, apply automatic padding to the innerTag
            hashOuter.DoFinal(Tag);
        }

        public byte[] Tag { get; } = new byte[TagSizeInBytes];

        private byte[] opadBlock;

        private readonly LongDigest hashInner;
        private readonly LongDigest hashOuter;

        private const byte Ipad = 0x36;
        private const byte Opad = 0x5c;
    }

    private enum KeyvalutOperationStatus
    {
        Success,
        ReadFail,
        WriteFail,
    }

    private enum HMACMode
    {
        HMAC384,
        HMAC512,
    }

    private enum Registers
    {
        Name = 0x000,
        Version = 0x008,
        Control = 0x010,
        Status = 0x018,
        Key = 0x040,
        Block = 0x080,
        Tag = 0x100,
        EntropySeed = 0x140,
        KeyvaultReadKeyControl = 0x600,
        KeyvaultReadKeyStatus = 0x604,
        KeyvaultReadBlockControl = 0x608,
        KeyvaultReadBlockStatus = 0x60C,
        KeyvaultWriteControl = 0x610,
        KeyvaultWriteStatus = 0x614,

        // Interrupt block (not implemented)
        GlobalIntrEn = 0x800,
        ErrorIntrEn = 0x804,
        NotifIntrEn = 0x808,
        ErrorGlobalIntr = 0x80C,
        NotifGlobalIntr = 0x810,
        ErrorInternalIntr = 0x814,
        NotifInternalIntr = 0x818,
        ErrorIntrTrig = 0x81C,
        NotifIntrTrig = 0x820,
        KeyModeErrorIntrCount = 0x900,
        KeyZeroErrorIntrCount = 0x904,
        Error2IntrCount = 0x908,
        Error3IntrCount = 0x90C,
        NotifCmdDoneIntrCount = 0x980,
        KeyModeErrorIntrCountIncr = 0xA00,
        KeyZeroErrorIntrCountIncr = 0xA04,
        Error2IntrCountIncr = 0xA08,
        Error3IntrCountIncr = 0xA0C,
        NotifCmdDoneIntrCountIncr = 0xA10,
    }
}
