//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32_WWDG : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32_WWDG(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public GPIO IRQ { get; }
        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.CR.Define(this, 0x7F)
                .WithValueField(0, 7, out counter, name: "T")
                .WithFlag(7, out activationBit, name: "WDGA")
                .WithReservedBits(8, 24);

            Registers.CFR.Define(this, 0x7F)
                .WithValueField(0, 7, out windowValue, name: "W")
                .WithReservedBits(7, 2)
                .WithFlag(9, out earlyWakeupInterruptEnable, name: "EWI")
                .WithReservedBits(10, 1)
                .WithValueField(11, 3, out timerBase, name: "WDGTB")
                .WithReservedBits(14, 18);

            Registers.SR.Define(this)
                .WithFlag(0, out earlyWakeupInterruptFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EWIF")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupt());
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(earlyWakeupInterruptFlag.Value && earlyWakeupInterruptEnable.Value);
        }

        private IValueRegisterField counter;
        private IFlagRegisterField activationBit;
        private IValueRegisterField windowValue;
        private IFlagRegisterField earlyWakeupInterruptEnable;
        private IValueRegisterField timerBase;
        private IFlagRegisterField earlyWakeupInterruptFlag;

        private enum Registers
        {
            CR = 0x00,
            CFR = 0x04,
            SR = 0x08,
        }
    }
}
