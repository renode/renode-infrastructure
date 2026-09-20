//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Cryptography
{
    public class STM32_PKA : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private readonly uint[] internalRam = new uint[1334]; // 0x400 to 0x18D4
        private bool procedureEnded;
        private bool interruptEnabled;

        public STM32_PKA(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // CR at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN")
                    .WithFlag(1, FieldMode.Write, name: "START", writeCallback: (_, val) => {
                        if(val)
                        {
                            procedureEnded = true;
                            UpdateInterrupt();
                        }
                    })
                    .WithValueField(8, 6, name: "MODE")
                    .WithFlag(17, name: "PROCENDIE", valueProviderCallback: _ => interruptEnabled, changeCallback: (_, val) => {
                        interruptEnabled = val;
                        UpdateInterrupt();
                    }),

                // SR at 0x04
                [0x04] = new DoubleWordRegister(this)
                    .WithFlag(16, FieldMode.Read, name: "BUSY", valueProviderCallback: _ => false)
                    .WithFlag(17, FieldMode.Read, name: "PROCENDF", valueProviderCallback: _ => procedureEnded),

                // CLRFR at 0x08
                [0x08] = new DoubleWordRegister(this)
                    .WithFlag(17, FieldMode.Write, name: "PROCENDFC", writeCallback: (_, val) => {
                        if(val)
                        {
                            procedureEnded = false;
                            UpdateInterrupt();
                        }
                    })
            };

            registers = new DoubleWordRegisterCollection(this, map);
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptEnabled && procedureEnded);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0x400 && offset < 0x400 + (internalRam.Length * 4))
            {
                var idx = (offset - 0x400) / 4;
                return internalRam[idx];
            }
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= 0x400 && offset < 0x400 + (internalRam.Length * 4))
            {
                var idx = (offset - 0x400) / 4;
                internalRam[idx] = value;
                return;
            }
            registers.Write(offset, value);
        }

        public void Reset()
        {
            procedureEnded = false;
            interruptEnabled = false;
            UpdateInterrupt();
            System.Array.Clear(internalRam, 0, internalRam.Length);
            registers.Reset();
        }

        public long Size => 0x1900;
    }
}
