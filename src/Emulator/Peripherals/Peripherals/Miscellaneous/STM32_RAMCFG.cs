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

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32_RAMCFG : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32_RAMCFG(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public GPIO IRQ { get; }
        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.CR.Define(this)
                .WithValueField(0, 3, out waitStateControl, name: "WSC")
                .WithReservedBits(3, 29);

            Registers.IER.Define(this)
                .WithFlag(0, out singleErrorInterruptEnable, name: "SEIE")
                .WithFlag(1, out doubleErrorInterruptEnable, name: "DEIE")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Registers.ISR.Define(this)
                .WithFlag(0, out singleErrorDetected, FieldMode.Read | FieldMode.WriteZeroToClear, name: "SEDC")
                .WithFlag(1, out doubleErrorDetected, FieldMode.Read | FieldMode.WriteZeroToClear, name: "DEDC")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Registers.SEDR.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "SEDR");

            Registers.DEDR.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "DEDR");

            Registers.M1CR.Define(this)
                .WithFlag(0, out sram1Erase, name: "SRAMER")
                .WithReservedBits(1, 31);

            Registers.M1SR.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "SRAMBUSY")
                .WithReservedBits(1, 31);

            Registers.M2CR.Define(this)
                .WithFlag(0, out sram2Erase, name: "SRAMER")
                .WithReservedBits(1, 31);

            Registers.M2SR.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "SRAMBUSY")
                .WithReservedBits(1, 31);
        }

        private void UpdateInterrupt()
        {
            var isAsserted = (singleErrorDetected.Value && singleErrorInterruptEnable.Value) ||
                             (doubleErrorDetected.Value && doubleErrorInterruptEnable.Value);
            IRQ.Set(isAsserted);
        }

        private IValueRegisterField waitStateControl;
        private IFlagRegisterField singleErrorInterruptEnable;
        private IFlagRegisterField doubleErrorInterruptEnable;
        private IFlagRegisterField singleErrorDetected;
        private IFlagRegisterField doubleErrorDetected;
        private IFlagRegisterField sram1Erase;
        private IFlagRegisterField sram2Erase;

        private enum Registers
        {
            CR = 0x00,
            IER = 0x04,
            ISR = 0x08,
            SEDR = 0x0C,
            DEDR = 0x10,
            M1CR = 0x20,
            M1SR = 0x24,
            M2CR = 0x40,
            M2SR = 0x44,
        }
    }
}
