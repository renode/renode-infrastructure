//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RenesasDA14_GeneralPurposeRegisters : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasDA14_GeneralPurposeRegisters(IMachine machine, RenesasDA_Watchdog sysWatchdog) : base(machine)
        {
            this.sysWatchdog = sysWatchdog;
            DefineRegisters();
        }

        public long Size => 0x18;

        private void DefineRegisters()
        {
            Registers.SetFreeze.Define(this)
                .WithTaggedFlag("FRZ_WKUPTIM", 0)
                .WithTaggedFlag("FRZ_SWTIM", 1)
                .WithTaggedFlag("FRZ_RESERVED", 2)
                // W1C because datasheet doesn't mention what is read. We assume it stays set until
                // propagated to the watchdog which happens immediately in Renode so it's always 0.
                .WithFlag(3, FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        sysWatchdog.Frozen = true;
                    }
                }, name: "FRZ_SYS_WDOG")
                .WithReservedBits(4, 1)
                .WithTaggedFlag("FRZ_DMA", 5)
                .WithTaggedFlag("FRZ_SWTIM2", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("FRZ_SWTIM3", 8)
                .WithTaggedFlag("FRZ_SWTIM4", 9)
                .WithTaggedFlag("FRZ_CMAC_WDOG", 10)
                .WithReservedBits(11, 21);

            Registers.ResetFreeze.Define(this)
                .WithTaggedFlag("FRZ_WKUPTIM", 0)
                .WithTaggedFlag("FRZ_SWTIM", 1)
                .WithTaggedFlag("FRZ_RESERVED", 2)
                // W1C because datasheet doesn't mention what is read. We assume it stays set until
                // propagated to the watchdog which happens immediately in Renode so it's always 0.
                .WithFlag(3, FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        sysWatchdog.Frozen = false;
                    }
                }, name: "FRZ_SYS_WDOG")
                .WithReservedBits(4, 1)
                .WithTaggedFlag("FRZ_DMA", 5)
                .WithTaggedFlag("FRZ_SWTIM2", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("FRZ_SWTIM3", 8)
                .WithTaggedFlag("FRZ_SWTIM4", 9)
                .WithTaggedFlag("FRZ_CMAC_WDOG", 10)
                .WithReservedBits(11, 21);

            // These are flags to preserve written values.
            Registers.Debug.Define(this, 1 << 0 | 1 << 8)
                .WithFlag(0, name: "SYS_CPU_FREEZE_EN")
                .WithFlag(1, name: "CMAC_CPU_FREEZE_EN")
                .WithFlag(2, name: "HALT_SYS_CMAC_CPU_EN")
                .WithFlag(3, name: "HALT_CMAC_SYS_CPU_EN")
                .WithFlag(4, FieldMode.Read, name: "SYS_CPU_IS_HALTED")
                .WithFlag(5, FieldMode.Read, name: "CMAC_CPU_IS_HALTED")
                .WithFlag(6, name: "SYS_CPUWAIT")
                .WithFlag(7, name: "SYS_CPUWAIT_ON_JTAG")
                .WithFlag(8, name: "CROSS_CPU_HALT_SENSITIVITY")
                .WithReservedBits(9, 23);

            // These are flags to preserve written values.
            Registers.GeneralPurposeStatus.Define(this)
                .WithFlag(0, name: "CAL_PHASE")
                .WithReservedBits(1, 31);

            // These are flags to preserve written values.
            Registers.SysCPUFCUTag.Define(this)
                .WithFlag(0, name: "SCPU_FCU_TAG_EN")
                .WithFlag(1, name: "SCPU_FCU_TAG_ALL_TRANS")
                .WithReservedBits(2, 30);
        }

        private readonly RenesasDA_Watchdog sysWatchdog;

        private enum Registers
        {
            SetFreeze = 0x0,
            ResetFreeze = 0x4,
            Debug = 0x8,
            GeneralPurposeStatus = 0xC,
            SysCPUFCUTag = 0x14
        }
    }
}
