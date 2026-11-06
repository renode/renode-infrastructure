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
    public class STM32_AES : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private readonly uint[] key = new uint[8];
        private readonly uint[] iv = new uint[4];
        private readonly Queue<uint> inputQueue = new Queue<uint>();
        private readonly Queue<uint> outputQueue = new Queue<uint>();

        private bool enabled;
        private uint mode; // 0: Encrypt, 1: KeyDeriv, 2: Decrypt
        private uint chainingMode; // 0: ECB, 1: CBC, 2: CTR, 3: GCM
        private bool is256BitKey;
        private bool computationComplete;
        private bool interruptEnabled;

        public STM32_AES(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // CR at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN", valueProviderCallback: _ => enabled, changeCallback: (_, val) => {
                        enabled = val;
                        if(!enabled) ResetQueues();
                    })
                    .WithValueField(1, 2, name: "DATATYPE")
                    .WithValueField(3, 2, name: "MODE", valueProviderCallback: _ => mode, changeCallback: (_, val) => mode = (uint)val)
                    .WithValueField(5, 2, name: "CHMOD", valueProviderCallback: _ => chainingMode, changeCallback: (_, val) => chainingMode = (uint)val)
                    .WithFlag(7, FieldMode.Write, name: "CCFC", writeCallback: (_, val) => {
                        if(val)
                        {
                            computationComplete = false;
                            UpdateInterrupt();
                        }
                    })
                    .WithFlag(18, name: "KEYSIZE", valueProviderCallback: _ => is256BitKey, changeCallback: (_, val) => is256BitKey = val),

                // SR at 0x04
                [0x04] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "CCF", valueProviderCallback: _ => computationComplete)
                    .WithFlag(1, FieldMode.Read, name: "RDERR", valueProviderCallback: _ => false)
                    .WithFlag(2, FieldMode.Read, name: "WRERR", valueProviderCallback: _ => false)
                    .WithFlag(3, FieldMode.Read, name: "BUSY", valueProviderCallback: _ => false),

                // DINR at 0x08
                [0x08] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "DIN", writeCallback: (_, val) => PushDataIn((uint)val)),

                // DOUTR at 0x0C
                [0x0C] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "DOUT", valueProviderCallback: _ => PopDataOut()),

                // IER at 0x300
                [0x300] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "CCFIE", valueProviderCallback: _ => interruptEnabled, changeCallback: (_, val) => {
                        interruptEnabled = val;
                        UpdateInterrupt();
                    }),

                // ISR at 0x304
                [0x304] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "CCFIF", valueProviderCallback: _ => computationComplete),

                // ICR at 0x308
                [0x308] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "CCF", writeCallback: (_, val) => {
                        if(val)
                        {
                            computationComplete = false;
                            UpdateInterrupt();
                        }
                    })
            };

            // Key registers: KEYR0..KEYR7 at 0x10..0x3C
            for(var i = 0; i < 8; i++)
            {
                var idx = i;
                map[0x10 + (idx * 4)] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"KEYR{idx}",
                        valueProviderCallback: _ => key[idx],
                        changeCallback: (_, val) => key[idx] = (uint)val);
            }

            // IV registers: IVR0..IVR3 at 0x20..0x2C
            for(var i = 0; i < 4; i++)
            {
                var idx = i;
                map[0x20 + (idx * 4)] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"IVR{idx}",
                        valueProviderCallback: _ => iv[idx],
                        changeCallback: (_, val) => iv[idx] = (uint)val);
            }

            registers = new DoubleWordRegisterCollection(this, map);
        }

        private void PushDataIn(uint word)
        {
            if(!enabled) return;
            inputQueue.Enqueue(word);
            if(inputQueue.Count >= 4)
            {
                ProcessBlock();
            }
        }

        private void ProcessBlock()
        {
            var rawInput = new byte[16];
            for(var i = 0; i < 4; i++)
            {
                var word = inputQueue.Dequeue();
                var bytes = BitConverter.GetBytes(word);
                Array.Copy(bytes, 0, rawInput, i * 4, 4);
            }

            var keyBytes = new byte[is256BitKey ? 32 : 16];
            for(var i = 0; i < (is256BitKey ? 8 : 4); i++)
            {
                var kb = BitConverter.GetBytes(key[i]);
                Array.Copy(kb, 0, keyBytes, i * 4, 4);
            }

            var ivBytes = new byte[16];
            for(var i = 0; i < 4; i++)
            {
                var ivb = BitConverter.GetBytes(iv[i]);
                Array.Copy(ivb, 0, ivBytes, i * 4, 4);
            }

            byte[] result;
            try
            {
                using(var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.Mode = (chainingMode == 0) ? CipherMode.ECB : CipherMode.CBC;
                    aes.Padding = PaddingMode.None;
                    if(aes.Mode == CipherMode.CBC) aes.IV = ivBytes;

                    var transform = (mode == 2) ? aes.CreateDecryptor() : aes.CreateEncryptor();
                    result = transform.TransformFinalBlock(rawInput, 0, 16);
                }
            }
            catch
            {
                // Fallback pass-through if cipher mode is CTR/GCM or unpadded
                result = rawInput;
            }

            for(var i = 0; i < 4; i++)
            {
                outputQueue.Enqueue(BitConverter.ToUInt32(result, i * 4));
            }

            computationComplete = true;
            UpdateInterrupt();
        }

        private uint PopDataOut()
        {
            return outputQueue.Count > 0 ? outputQueue.Dequeue() : 0u;
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptEnabled && computationComplete);
        }

        private void ResetQueues()
        {
            inputQueue.Clear();
            outputQueue.Clear();
            computationComplete = false;
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            ResetQueues();
            enabled = false;
            mode = 0;
            chainingMode = 0;
            is256BitKey = false;
            interruptEnabled = false;
            registers.Reset();
        }

        public long Size => 0x400;
    }
}
