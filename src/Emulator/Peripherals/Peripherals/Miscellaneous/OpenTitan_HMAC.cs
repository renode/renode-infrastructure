//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // OpenTitan HMACi HWIP as per https://docs.opentitan.org/hw/ip/hmac/doc/ (30.06.2021)
    public class OpenTitan_HMAC: BasicDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public OpenTitan_HMAC(IMachine machine) : base(machine)
        {
            key = new byte[SecretKeyLength * 4];
            digest = new byte[DigestLength * 4];

            packer = new Packer(this);
            IRQ = new GPIO();
            FatalAlert = new GPIO();

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            Array.Clear(key, 0, key.Length);
            Array.Clear(digest, 0, digest.Length);

            UpdateInterrupts();
            FatalAlert.Unset();
        }
        
        public override void WriteDoubleWord(long offset, uint value)
        {
            if(IsInFifoWindowRange(offset))
            {
                PushData(value, 4);
            }
            else
            {
                base.WriteDoubleWord(offset, value);
            }
        }
        
        public void WriteWord(long offset, ushort value)
        {
            if(IsInFifoWindowRange(offset))
            {
                PushData(value, 2);
            }
            else
            {
                this.Log(LogLevel.Warning, "Tried to write value 0x{0:X} at offset 0x{1:X}, but word access to registers is not supported", value, offset);
            }
        }
        
        public void WriteByte(long offset, byte value)
        {
            if(IsInFifoWindowRange(offset))
            {
                PushData(value, 1);
            }
            else
            {
                this.Log(LogLevel.Warning, "Tried to write value 0x{0:X} at offset 0x{1:X}, but byte access to registers is not supported", value, offset);
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(IsInFifoWindowRange(offset))
            {
                this.Log(LogLevel.Warning, $"Returning 0");
                return 0;
            }
            return base.ReadDoubleWord(offset);
        }
        
        //Below methods are required to properly register writes for byte and word accesses
        public ushort ReadWord(long offset)
        {
            this.Log(LogLevel.Warning, "Tried to read value at offset 0x{0:X}, but word access to registers is not supported", offset);
            return 0;
        }
        
        public byte ReadByte(long offset)
        {
            this.Log(LogLevel.Warning, "Tried to read value at offset 0x{0:X}, but byte access to registers is not supported", offset);
            return 0;
        }
        
        public long Size =>  0x1000;
        
        public GPIO IRQ { get; }
        public GPIO FatalAlert { get;  private set;}

        private void HashProcess()
        {
            this.Log(LogLevel.Debug, "Received a 'hash_process' command");

            var message = packer.DataToArray(endianSwap.Value);
            byte[] hash;

            if(hmacEnabled.Value)
            {
                using(var hmac = new HMACSHA256(key))
                {
                    hash = hmac.ComputeHash(message);
                }
            }
            else
            {
                using(var sha = SHA256.Create())
                {
                    hash = sha.ComputeHash(message);
                }
            }
           
            if(digestSwap.Value)
            {
                Misc.EndiannessSwapInPlace(hash, sizeof(uint));
            }

            Array.Copy(hash, 0, digest, 0, digest.Length);
            hmacDoneInterrupt.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var hmacDone = hmacDoneInterrupt.Value && hmacDoneInterruptEnable.Value;
            var fifoEmpty = fifoEmptyInterrupt.Value && fifoEmptyInterruptEnable.Value;
            var hmacError = hmacErrorInterrupt.Value && hmacErrorInterruptEnable.Value;
            IRQ.Set(hmacDone || fifoEmpty || hmacError);
        }

        private bool IsInFifoWindowRange(long offset)
        {
            return offset >= (long)Registers.FifoWindow && offset < ((long)Registers.FifoWindow + 0x800);
        }

        private void PushData(uint value, int accessByteCount)
        {
            this.Log(LogLevel.Noisy, "Pushing {0} bytes from value 0x{1:X}", accessByteCount, value);

            packer.PushData(value, accessByteCount);

            // There should be blink on fifoFull bit, but we pretend that we handle hashing fast enough for software not to notice that fifo is ever full
            // We just raise fifoEmptyInterrupt to make sure it keeps them bytes coming 
            fifoEmptyInterrupt.Value = true;
            UpdateInterrupts();
        }
        
        private void DefineRegisters()
        {
            Registers.InterruptStatus.Define(this)
                .WithFlag(0, out hmacDoneInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "hmac_done")
                .WithFlag(1, out fifoEmptyInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "fifo_empty")
                .WithFlag(2, out hmacErrorInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "hmac_err")
                .WithIgnoredBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out hmacDoneInterruptEnable, name: "hmac_done")
                .WithFlag(1, out fifoEmptyInterruptEnable, name: "fifo_empty")
                .WithFlag(2, out hmacErrorInterruptEnable, name: "hmac_err")
                .WithIgnoredBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) hmacDoneInterrupt.Value = true; }, name: "hmac_done")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if(value) fifoEmptyInterrupt.Value = true; }, name: "fifo_empty")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if(value) hmacErrorInterrupt.Value = true; }, name: "hmac_err")
                .WithIgnoredBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                .WithIgnoredBits(1, 31);

            Registers.ConfigurationRegister.Define(this, 0x4)
                .WithFlag(0, out hmacEnabled, name: "hmac_en")
                .WithFlag(1, out shaEnabled, name: "sha_en")
                .WithFlag(2, out endianSwap, name: "endian_swap") //0 - big endian; 1 - little endian
                .WithFlag(3, out digestSwap, name: "digest_swap") //1 - big-endian 
                .WithIgnoredBits(4, 28)
                .WithWriteCallback((_, __) => 
                {
                    this.Log(LogLevel.Debug, "Configuration set to hmac_en: {0}, sha_en: {1}, endian_swap: {2}, digest_swap: {3}", hmacEnabled.Value, shaEnabled.Value, endianSwap.Value, digestSwap.Value);
                });

            Registers.Command.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { }, name: "hash_start") // intentionally do nothing
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if(value) HashProcess(); }, name: "hash_process")
                .WithIgnoredBits(2, 30);

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "fifo_empty", valueProviderCallback: _ => true) // fifo is always empty
                .WithFlag(1, FieldMode.Read, name: "fifo_full", valueProviderCallback: _ => false) // fifo is never full
                .WithReservedBits(2, 2)
                .WithValueField(4, 5, FieldMode.Read, name: "fifo_depth", valueProviderCallback: _ => 0)
                .WithIgnoredBits(9, 23);

            Registers.ErrorCode.Define(this)
                .WithTag("err_code", 0, 32);

            Registers.RandomizationInput.Define(this)
                .WithTag("secret", 0, 32);

            Registers.SecretKey_0.DefineMany(this, SecretKeyLength, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => SetKeyPart(idx, (uint)value), name: "key");
            });

            Registers.Digest_0.DefineMany(this, DigestLength, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => GetDigestPart(idx), name: "digest");
            });

            Registers.MessageLengthLowerPart.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)ReceivedLengthInBits, name: "v");

            Registers.MessageLengthUpperPart.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(ReceivedLengthInBits >> 32), name: "v");
        }

        private void SetKeyPart(int part, uint value)
        {
            DebugHelper.Assert(part >= 0 && part < SecretKeyLength);

            this.Log(LogLevel.Noisy, "Setting key_{0} to 0x{1:X2}", part, value); 
            var offset = part * 4;
            for(int i = 3; i >= 0; i--)
            {
                key[i + offset] = (byte)value;
                value = value >> 8;
            }
        }            

        private uint GetDigestPart(int part)
        {
            DebugHelper.Assert(part >= 0 && part < DigestLength);
            return BitHelper.ToUInt32(digest, part * 4, 4, false);
        }

        private ulong ReceivedLengthInBits => (ulong)packer.Count * 8;

        private readonly byte[] digest;
        private readonly byte[] key;
        private readonly Packer packer;
        
        private IFlagRegisterField hmacDoneInterrupt;
        private IFlagRegisterField fifoEmptyInterrupt;
        private IFlagRegisterField hmacErrorInterrupt;
        private IFlagRegisterField hmacDoneInterruptEnable;
        private IFlagRegisterField fifoEmptyInterruptEnable;
        private IFlagRegisterField hmacErrorInterruptEnable;
        private IFlagRegisterField hmacEnabled;
        private IFlagRegisterField shaEnabled;
        private IFlagRegisterField endianSwap;
        private IFlagRegisterField digestSwap;

        private const int SecretKeyLength = 8;
        private const int DigestLength = 8;

        private class Packer
        {
            public Packer(IEmulationElement parent)
            {
                this.parent = parent;
                byteQueue = new Queue<byte>();
                queueLock = new object();
            }

            public void PushData(uint data, int accessByteCount)
            {
                lock(queueLock)
                {
                    for(int i = 0; i < accessByteCount; i++)
                    {
                        var b = (byte)(data >> (8 * i)); 
                        byteQueue.Enqueue(b);
                        parent.Log(LogLevel.Noisy, "Pushed byte 0x{0:X2}", b);
                    }
                }
            }

            public byte[] DataToArray(bool reverse)
            {
                var byteList = new List<byte>();
                lock(queueLock)
                {
                    while(byteQueue.Count > 0)
                    {
                        byteList.Add(byteQueue.Dequeue());
                    }
                }
                
                var output = byteList.ToArray();
                if(reverse)
                {
                    Array.Reverse(output);
                }
                
                return output;
            }

            public ulong Count => (ulong)byteQueue.Count; 
            
            private readonly IEmulationElement parent;
            private readonly Queue<byte> byteQueue;
            private readonly object queueLock;
        }
        
        private enum Registers
        {
            InterruptStatus        = 0x0,
            InterruptEnable        = 0x4,
            InterruptTest          = 0x8,
            AlertTest              = 0xC,
            ConfigurationRegister  = 0x10,
            Command                = 0x14,
            Status                 = 0x18,
            ErrorCode              = 0x1C,
            RandomizationInput     = 0x20,
            SecretKey_0            = 0x24,
            SecretKey_1            = 0x28,
            SecretKey_2            = 0x2C,
            SecretKey_3            = 0x30,
            SecretKey_4            = 0x34,
            SecretKey_5            = 0x38,
            SecretKey_6            = 0x3C,
            SecretKey_7            = 0x40,
            Digest_0               = 0x44,
            Digest_1               = 0x48,
            Digest_2               = 0x4C,
            Digest_3               = 0x50,
            Digest_4               = 0x54,
            Digest_5               = 0x58,
            Digest_6               = 0x5C,
            Digest_7               = 0x60,
            MessageLengthLowerPart = 0x64,
            MessageLengthUpperPart = 0x68, 
            FifoWindow             = 0x800,
        }
    }
}
