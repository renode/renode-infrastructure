//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Cryptography
{
    public class STM32_HASH : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private readonly List<byte> inputBuffer = new List<byte>();
        private readonly uint[] digest = new uint[8];

        private uint algorithm; // 0: SHA-1, 1: MD5, 2: SHA-224, 3: SHA-256
        private bool digestReady;
        private bool interruptEnabled;

        public STM32_HASH(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // CR at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(2, FieldMode.Write, name: "INIT", writeCallback: (_, val) => {
                        if(val) ResetBuffer();
                    })
                    .WithValueField(4, 2, name: "DATATYPE")
                    .WithFlag(6, name: "MODE")
                    .WithValueField(7, 2, name: "ALGO0", valueProviderCallback: _ => algorithm & 0x3, changeCallback: (_, val) => algorithm = (uint)val)
                    .WithFlag(18, name: "ALGO1", valueProviderCallback: _ => (algorithm & 0x4) != 0, changeCallback: (_, val) => {
                        if(val) algorithm |= 0x4;
                        else algorithm &= ~0x4u;
                    }),

                // DIN at 0x04
                [0x04] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "DIN", writeCallback: (_, val) => {
                        var bytes = BitConverter.GetBytes((uint)val);
                        inputBuffer.AddRange(bytes);
                    }),

                // STR at 0x08
                [0x08] = new DoubleWordRegister(this)
                    .WithValueField(0, 5, name: "NBLW")
                    .WithFlag(8, FieldMode.Write, name: "DCAL", writeCallback: (_, val) => {
                        if(val) ComputeDigest();
                    }),

                // IMR at 0x20
                [0x20] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "DINIE")
                    .WithFlag(1, name: "DCIE", valueProviderCallback: _ => interruptEnabled, changeCallback: (_, val) => {
                        interruptEnabled = val;
                        UpdateInterrupt();
                    }),

                // SR at 0x24
                [0x24] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "DINIS", valueProviderCallback: _ => true)
                    .WithFlag(1, name: "DCIS", valueProviderCallback: _ => digestReady, changeCallback: (_, val) => {
                        if(!val) digestReady = false;
                        UpdateInterrupt();
                    })
                    .WithFlag(3, FieldMode.Read, name: "BUSY", valueProviderCallback: _ => false)
            };

            // Digest registers: HR0..HR7 at 0x0C..0x28
            for(var i = 0; i < 8; i++)
            {
                var idx = i;
                map[0x0C + (idx * 4)] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: $"HR{idx}", valueProviderCallback: _ => digest[idx]);
            }

            registers = new DoubleWordRegisterCollection(this, map);
        }

        private void ComputeDigest()
        {
            byte[] hash;
            var data = inputBuffer.ToArray();

            // Default to SHA-256 for modern STM32 firmware
            if(algorithm == 0)
            {
                using(var sha1 = SHA1.Create())
                {
                    hash = sha1.ComputeHash(data);
                }
            }
            else
            {
                using(var sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(data);
                }
            }

            Array.Clear(digest, 0, digest.Length);
            for(var i = 0; i < hash.Length / 4; i++)
            {
                digest[i] = BitConverter.ToUInt32(hash, i * 4);
            }

            digestReady = true;
            UpdateInterrupt();
        }

        private void ResetBuffer()
        {
            inputBuffer.Clear();
            digestReady = false;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptEnabled && digestReady);
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            ResetBuffer();
            registers.Reset();
        }

        public long Size => 0x400;
    }
}
