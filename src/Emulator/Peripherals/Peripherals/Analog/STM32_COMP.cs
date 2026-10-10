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

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32_COMP : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private bool comp1Value;
        private bool comp2Value;

        public STM32_COMP(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // COMP1_CSR at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN")
                    .WithValueField(4, 4, name: "INMSEL")
                    .WithValueField(8, 2, name: "INPSEL")
                    .WithFlag(15, name: "POL")
                    .WithValueField(16, 2, name: "HYST")
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => comp1Value, name: "VALUE")
                    .WithFlag(31, name: "LOCK"),

                // COMP2_CSR at 0x04
                [0x04] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN")
                    .WithValueField(4, 4, name: "INMSEL")
                    .WithValueField(8, 2, name: "INPSEL")
                    .WithFlag(15, name: "POL")
                    .WithValueField(16, 2, name: "HYST")
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => comp2Value, name: "VALUE")
                    .WithFlag(31, name: "LOCK"),
            };
            registers = new DoubleWordRegisterCollection(this, map);
        }

        public void SetComp1(bool value)
        {
            comp1Value = value;
            IRQ.Blink();
        }

        public void SetComp2(bool value)
        {
            comp2Value = value;
            IRQ.Blink();
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            comp1Value = false;
            comp2Value = false;
            registers.Reset();
        }

        public long Size => 0x400;
    }
}
