//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class K6xF_RNG : IDoubleWordPeripheral, IKnownSize
    {
        public K6xF_RNG(IMachine machine)
        {
            IRQ = new GPIO();

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithReservedBits(5, 26)
                    .WithFlag(4, out sleep, name: "SLP")
                    .WithTaggedFlag("CLRI", 3)
                    .WithTaggedFlag("INTM", 2)
                    .WithTaggedFlag("HA", 1)
                    .WithFlag(0, out enable, name: "GO")
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(16, 8, name: "OREG_SIZE")
                    .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (enable.Value) ? 1u : 0u, name: "OREG_LVL")
                    .WithReservedBits(5, 3)
                    .WithTaggedFlag("SLP", 4)
                    .WithTaggedFlag("ERRI", 3)
                    .WithTaggedFlag("ORU", 2)
                    .WithTaggedFlag("LRS", 1)
                    .WithTaggedFlag("SECV", 0)
                },
                {(long)Registers.Entropy, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name:"EXT_ENT")
                },
                {(long)Registers.Output, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (enable.Value) ? unchecked((uint)rng.Next()) : 0u, name: "RNDOUT")
                }
            };

            registers = new DoubleWordRegisterCollection(this, registerMap);

            rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; private set; }

        private readonly DoubleWordRegisterCollection registers;
        private readonly PseudorandomNumberGenerator rng;
        private readonly IFlagRegisterField enable;
        private readonly IFlagRegisterField sleep;

        private enum Registers
        {
            Control = 0x0,
            Status  = 0x4,
            Entropy = 0x8,
            Output  = 0xC
        }
    }
}
