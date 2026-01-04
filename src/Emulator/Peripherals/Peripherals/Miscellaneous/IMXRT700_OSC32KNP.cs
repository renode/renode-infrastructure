//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class IMXRT700_OSC32KNP : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMXRT700_OSC32KNP(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x0F00080A)
                .WithFlag(0, name: "OSC_DIS")
                .WithFlag(1, name: "MODE") // This field can only be written 1 by reset. It is part of the programming model, so we don't prevent writing other value at peripheral level.
                .WithFlag(2, name: "BYPASS_EN")
                .WithFlag(3) // Do not change the value of this bit. It is not reserved neither.
                .WithReservedBits(4, 3)
                .WithValueField(8, 4, name: "CAP_TRIM")
                .WithReservedBits(12, 4)
                .WithFlag(16, name: "CLKMON_EN")
                .WithReservedBits(17, 1)
                .WithReservedBits(18, 6)
                .WithReservedBits(24, 8);

            Registers.ClockStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "TCXO_STABLE")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "SCXO_STABLE")
                .WithReservedBits(2, 6)
                .WithFlag(8, FieldMode.Read | FieldMode.WriteZeroToClear, valueProviderCallback: _ => false, name: "CLK_TAMPER_DETECTED")
                .WithReservedBits(9, 23);
        }

        private enum Registers
        {
            Control = 0x00, // CTRL
            ClockStatus = 0x08 // STAT
        }
    }
}