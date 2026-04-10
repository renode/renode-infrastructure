//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32_RNG : IDoubleWordPeripheral, IKnownSize
    {
        public STM32_RNG(STM32Series series)
        {
            IRQ = new GPIO();
            var supportedSeries = new List<STM32Series>{
                STM32Series.F4,
                STM32Series.F7,
                STM32Series.H7,
                STM32Series.L0,
                STM32Series.L5,
            };
            if(!supportedSeries.Contains(series))
            {
                throw new ConstructionException($"Unsupported STM32 series value: {series}!");
            }
            this.series = series;

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithReservedBits(0, 2)
                    .WithFlag(2, out enable, changeCallback: (_, value) => Update(), name: "RNGEN")
                    .WithFlag(3, out interruptEnable, changeCallback: (_, value) => Update(), name: "IE")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => enable.Value, name: "DRDY")
                    .WithTag("CECS", 1, 1)
                    .WithTag("SECS", 2, 1)
                    .WithReservedBits(3, 2)
                    .WithTag("CEIS", 5, 1)
                    .WithTag("SEIS", 6, 1)
                    .WithReservedBits(7, 25)
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if(enable.Value)
                    {
                        return unchecked((uint)rng.Next());
                    }
                    else
                    {
                        return 0;
                    }
                }, name: "RNDATA")
                },
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x400;

        public GPIO IRQ { get; private set; }

        private void Update()
        {
            IRQ.Set(enable.Value && interruptEnable.Value);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly PseudorandomNumberGenerator rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private readonly IFlagRegisterField enable;
        private readonly IFlagRegisterField interruptEnable;
        private readonly STM32Series series;

        private enum Registers
        {
            Control = 0x0,
            Status = 0x4,
            Data = 0x8,
        }
    }
}
