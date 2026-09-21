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
    public class STM32_SBS : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32_SBS(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.HDPLCR.Define(this)
                .WithValueField(0, 8, out hardwareDebugProtectionLevel, name: "HDPL")
                .WithReservedBits(8, 24);

            Registers.HDPLSR.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => hardwareDebugProtectionLevel.Value,
                    name: "HDPL")
                .WithReservedBits(8, 24);

            Registers.NEXTHDCR.Define(this)
                .WithValueField(0, 8, name: "NEXTHD")
                .WithReservedBits(8, 24);

            Registers.FPUIMR.Define(this)
                .WithValueField(0, 6, out fpuInterruptMask, name: "FPU_IE")
                .WithReservedBits(6, 26);

            Registers.PMCR.Define(this)
                .WithFlag(8, out boostEnable, name: "BOOSTEN")
                .WithReservedBits(0, 8)
                .WithReservedBits(9, 23);

            Registers.CCSR.Define(this)
                .WithFlag(0, out compensationCellEnable, name: "EN")
                .WithFlag(1, out compensationCellSpeedSelect, name: "CS")
                .WithReservedBits(2, 6)
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
                .WithReservedBits(9, 23);

            Registers.CFGR2.Define(this)
                .WithFlag(0, out coreLockupLock, name: "CLL")
                .WithFlag(1, out sramParityLock, name: "SPL")
                .WithReservedBits(2, 30);
        }

        private IValueRegisterField hardwareDebugProtectionLevel;
        private IValueRegisterField fpuInterruptMask;
        private IFlagRegisterField boostEnable;
        private IFlagRegisterField compensationCellEnable;
        private IFlagRegisterField compensationCellSpeedSelect;
        private IFlagRegisterField coreLockupLock;
        private IFlagRegisterField sramParityLock;

        private enum Registers
        {
            HDPLCR = 0x00,
            HDPLSR = 0x04,
            NEXTHDCR = 0x08,
            FPUIMR = 0x14,
            PMCR = 0x18,
            CCSR = 0x24,
            CFGR2 = 0x28,
        }
    }
}
