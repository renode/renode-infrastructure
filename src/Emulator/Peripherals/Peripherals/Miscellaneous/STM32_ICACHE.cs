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
    public class STM32_ICACHE : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32_ICACHE(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public GPIO IRQ { get; }
        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.CR.Define(this)
                .WithFlag(0, out cacheEnable, name: "EN")
                .WithFlag(1, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            // Invalidation completes immediately in simulation
                            busyEndFlag = true;
                            UpdateInterrupt();
                        }
                    },
                    name: "INVALIDATE")
                .WithFlag(2, out waySelection, name: "WAYSEL")
                .WithReservedBits(3, 29);

            Registers.SR.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSYF")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => busyEndFlag, name: "BSYENDF")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => errorFlag, name: "ERRF")
                .WithReservedBits(3, 29);

            Registers.IER.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, out busyEndInterruptEnable, name: "BSYENDIE")
                .WithFlag(2, out errorInterruptEnable, name: "ERRIE")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Registers.FCR.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            busyEndFlag = false;
                            UpdateInterrupt();
                        }
                    },
                    name: "CBSYENDF")
                .WithFlag(2, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            errorFlag = false;
                            UpdateInterrupt();
                        }
                    },
                    name: "CERRF")
                .WithReservedBits(3, 29);

            Registers.HMONR.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "HMONR");

            Registers.MMONR.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "MMONR");
        }

        private void UpdateInterrupt()
        {
            var isAsserted = (busyEndFlag && busyEndInterruptEnable.Value) ||
                             (errorFlag && errorInterruptEnable.Value);
            IRQ.Set(isAsserted);
        }

        private IFlagRegisterField cacheEnable;
        private IFlagRegisterField waySelection;
        private IFlagRegisterField busyEndInterruptEnable;
        private IFlagRegisterField errorInterruptEnable;
        private bool busyEndFlag;
        private bool errorFlag;

        private enum Registers
        {
            CR = 0x00,
            SR = 0x04,
            IER = 0x08,
            FCR = 0x0C,
            HMONR = 0x10,
            MMONR = 0x14,
        }
    }
}
