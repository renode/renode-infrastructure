//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32H5_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H5_PWR(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.PowerModeControl.Define(this)
                .WithTag("PMCR", 0, 32);

            Registers.PowerModeStatus.Define(this)
                .WithTag("PMSR", 0, 32);

            Registers.VoltageScalingControl.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 2, out vosValue, name: "VOS")
                .WithReservedBits(6, 26);

            Registers.VoltageScalingStatus.Define(this)
                .WithReservedBits(0, 3)
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "VOSRDY")
                .WithReservedBits(4, 9)
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => true, name: "ACTVOSRDY")
                .WithValueField(14, 2, FieldMode.Read, valueProviderCallback: _ => vosValue.Value, name: "ACTVOS")
                .WithReservedBits(16, 16);

            Registers.BackupDomainControl.Define(this)
                .WithTag("BDCR", 0, 32);

            Registers.DebugPortControl.Define(this)
                .WithTag("DBPCR", 0, 32);

            Registers.BackupDomainStatus.Define(this)
                .WithTag("BDSR", 0, 32);

            Registers.USBTypeCPowerDelivery.Define(this)
                .WithTag("UCPDR", 0, 32);

            Registers.SupplyConfigurationControl.Define(this)
                .WithTag("SCCR", 0, 32);

            Registers.VoltageMonitoringControl.Define(this)
                .WithTag("VMCR", 0, 32);

            Registers.USBSupplyControl.Define(this)
                .WithTag("USBSCR", 0, 32);

            Registers.VoltageMonitoringStatus.Define(this)
                .WithTag("VMSR", 0, 32);

            Registers.WakeupStatusClear.Define(this)
                .WithTag("WUSCR", 0, 32);

            Registers.WakeupStatus.Define(this)
                .WithTag("WUSR", 0, 32);

            Registers.WakeupControl.Define(this)
                .WithTag("WUCR", 0, 32);

            Registers.IORetention.Define(this)
                .WithTag("IORETR", 0, 32);

            Registers.SecurityConfiguration.Define(this)
                .WithTag("SECCFGR", 0, 32);

            Registers.PrivilegeConfiguration.Define(this)
                .WithTag("PRIVCFGR", 0, 32);
        }

        private IValueRegisterField vosValue;

        private enum Registers
        {
            PowerModeControl = 0x00,
            PowerModeStatus = 0x04,
            VoltageScalingControl = 0x10,
            VoltageScalingStatus = 0x14,
            BackupDomainControl = 0x20,
            DebugPortControl = 0x24,
            BackupDomainStatus = 0x28,
            USBTypeCPowerDelivery = 0x2C,
            SupplyConfigurationControl = 0x30,
            VoltageMonitoringControl = 0x34,
            USBSupplyControl = 0x38,
            VoltageMonitoringStatus = 0x3C,
            WakeupStatusClear = 0x40,
            WakeupStatus = 0x44,
            WakeupControl = 0x48,
            IORetention = 0x50,
            SecurityConfiguration = 0x100,
            PrivilegeConfiguration = 0x104,
        }
    }
}
