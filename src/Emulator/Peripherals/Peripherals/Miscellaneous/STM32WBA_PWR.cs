//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32WBA_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32WBA_PWR(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            Voltage = 3.3;
            prevPvdo = false;
        }

        public long Size => 0x400;
        public GPIO IRQ { get; }

        public double? ThresholdVoltage { get => PvdLevelToVoltage(pvdLevel.Value); }

        public double Voltage
        {
            get
            {
                return voltage;
            }
            set
            {
                voltage = value;
                UpdatePvd();
            }
        }

        public PvdLevelSelection PvdLevel
        {
            get
            {
                return pvdLevel.Value;
            }
            set
            {
                pvdLevel.Value = value;
                UpdatePvd();
            }
        }

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithTag("LPMS", 0, 3)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("R2RSB1", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("ULPMEN", 7)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("RADIORSB", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("R1RSB1", 12)
                .WithReservedBits(13, 19);
            Registers.Control2.Define(this)
                .WithTaggedFlag("SRAM1PDS1", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("SRAM2PDS1", 4)
                .WithReservedBits(5, 3)
                .WithTaggedFlag("ICRAMPDS", 8)
                .WithReservedBits(9, 5)
                .WithTaggedFlag("FLASHFWU", 14)
                .WithReservedBits(15, 17);
            Registers.Control3.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("FSTEN", 2)
                .WithReservedBits(3, 29);
            Registers.VoltageScaling.Define(this, 0x0000_8000)
                .WithReservedBits(0, 15)
                .WithTaggedFlag("VOSRDY", 15)
                .WithEnumField<DoubleWordRegister, VoltageScalingRangeSelection>(16, 1, out vosValue, name: "VOS")
                .WithReservedBits(17, 15);
            Registers.SupplyVoltageMonitoringControl.Define(this)
                .WithReservedBits(0, 4)
                .WithFlag(4, out pvdEnableFlag, name: "PVDE")
                .WithEnumField<DoubleWordRegister, PvdLevelSelection>(5, 3, out pvdLevel, writeCallback: (_, value) =>
                    {
                        if(value == PvdLevelSelection.ExternalInput)
                        {
                            this.Log(LogLevel.Warning, "External PVD input selected, this is not supported");
                        }
                        UpdatePvd();
                    }, name: "PVDLS")
                .WithReservedBits(8, 24);
            Registers.WakeupControl1.Define(this)
                .WithTaggedFlag("WUPEN1", 0)
                .WithTaggedFlag("WUPEN2", 1)
                .WithTaggedFlag("WUPEN3", 2)
                .WithTaggedFlag("WUPEN4", 3)
                .WithTaggedFlag("WUPEN5", 4)
                .WithTaggedFlag("WUPEN6", 5)
                .WithTaggedFlag("WUPEN7", 6)
                .WithTaggedFlag("WUPEN8", 7)
                .WithReservedBits(8, 24);
            Registers.WakeupControl2.Define(this)
                .WithTaggedFlag("WUPP1", 0)
                .WithTaggedFlag("WUPP2", 1)
                .WithTaggedFlag("WUPP3", 2)
                .WithTaggedFlag("WUPP4", 3)
                .WithTaggedFlag("WUPP5", 4)
                .WithTaggedFlag("WUPP6", 5)
                .WithTaggedFlag("WUPP7", 6)
                .WithTaggedFlag("WUPP8", 7)
                .WithReservedBits(8, 24);
            Registers.WakeupControl3.Define(this)
                .WithTag("WUSEL1", 0, 2)
                .WithTag("WUSEL2", 2, 2)
                .WithTag("WUSEL3", 4, 2)
                .WithTag("WUSEL4", 6, 2)
                .WithTag("WUSEL5", 8, 2)
                .WithTag("WUSEL6", 10, 2)
                .WithTag("WUSEL7", 12, 2)
                .WithTag("WUSEL8", 14, 2)
                .WithReservedBits(16, 16);
            Registers.BackupDomain.Define(this)
                .WithFlag(0, out backupDomainDisabled, name: "DBP")
                .WithReservedBits(1, 31);
            Registers.SecurityConfiguration.Define(this)
                .WithTaggedFlag("WUP1SEC", 0)
                .WithTaggedFlag("WUP2SEC", 1)
                .WithTaggedFlag("WUP3SEC", 2)
                .WithTaggedFlag("WUP4SEC", 3)
                .WithTaggedFlag("WUP5SEC", 4)
                .WithTaggedFlag("WUP6SEC", 5)
                .WithTaggedFlag("WUP7SEC", 6)
                .WithTaggedFlag("WUP8SEC", 7)
                .WithReservedBits(8, 4)
                .WithTaggedFlag("LPMSEC", 12)
                .WithTaggedFlag("VDMSEC", 13)
                .WithTaggedFlag("VBSEC", 14)
                .WithReservedBits(15, 17);
            Registers.PrivilegeControl.Define(this)
                .WithTaggedFlag("SPRIV", 0)
                .WithTaggedFlag("NSPRIV", 1)
                .WithReservedBits(2, 30);
            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "CSSF",
                    writeCallback: (_, __) =>
                    {
                        standbyFlag.Value = false;
                        stopFlag.Value = false;
                    })
                .WithFlag(1, out stopFlag, FieldMode.Read, name: "STOPF")
                .WithFlag(2, out standbyFlag, FieldMode.Read, name: "SBF")
                .WithReservedBits(3, 29);
            Registers.SupplyVoltageMonitoringStatus.Define(this, 0x0000_8000)
                .WithReservedBits(0, 4)
                .WithFlag(4, out pvdoFlag, FieldMode.Read, name: "PVDO")
                .WithReservedBits(5, 10)
                .WithTaggedFlag("ACTVOSRDY", 15)
                .WithTaggedFlag("ACTVOS", 16)
                .WithReservedBits(17, 15);
            Registers.WakeupStatus.Define(this)
                .WithFlags(0, 8, out wakeupFlags, FieldMode.Read, name: "WUF")
                .WithReservedBits(8, 24);
            Registers.WakeupStatusClear.Define(this)
                .WithFlags(0, 8, FieldMode.WriteOneToClear, name: "CWUF",
                    writeCallback: (idx, _, __) => wakeupFlags[idx].Value = false)
                .WithReservedBits(8, 24);
            Registers.GpioAStandbyEnable.Define(this)
                .WithTag("EN[0:3]", 0, 4)
                .WithReservedBits(4, 1)
                .WithTag("EN[5:15]", 5, 11)
                .WithReservedBits(16, 16);
            Registers.GpioAStandbyStatus.Define(this)
                .WithTag("RET[0:3]", 0, 4)
                .WithReservedBits(4, 1)
                .WithTag("RET[5:15]", 5, 11)
                .WithReservedBits(16, 16);
            Registers.GpioBStandbyEnable.Define(this)
                .WithTag("EN[0:15]", 0, 16)
                .WithReservedBits(16, 16);
            Registers.GpioBStandbyStatus.Define(this)
                .WithTag("RET[0:15]", 0, 16)
                .WithReservedBits(16, 16);
            Registers.GpioCStandbyEnable.Define(this)
                .WithReservedBits(0, 13)
                .WithTag("EN[13:15]", 13, 3)
                .WithReservedBits(16, 16);
            Registers.GpioCStandbyStatus.Define(this)
                .WithReservedBits(0, 13)
                .WithTag("RET[13:15]", 13, 3)
                .WithReservedBits(16, 16);
            Registers.GpioHStandbyEnable.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("EN3", 3)
                .WithReservedBits(4, 28);
            Registers.GpioHStandbyStatus.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("RET3", 3)
                .WithReservedBits(4, 28);
            Registers.RadioStatusAndControl.Define(this)
                .WithTag("MODE", 0, 2)
                .WithTaggedFlag("PHYMODE", 2)
                .WithTaggedFlag("ENCMODE", 3)
                .WithReservedBits(4, 4)
                .WithTag("RFVDDHPA", 8, 5)
                .WithReservedBits(13, 2)
                .WithTaggedFlag("REGPARDYVDDRFPA", 15)
                .WithReservedBits(16, 16);
        }

        private double? PvdLevelToVoltage(PvdLevelSelection level)
        {
            switch(level)
            {
            case PvdLevelSelection.V2_0:
                return 2.0;
            case PvdLevelSelection.V2_2:
                return 2.2;
            case PvdLevelSelection.V2_4:
                return 2.4;
            case PvdLevelSelection.V2_5:
                return 2.5;
            case PvdLevelSelection.V2_6:
                return 2.6;
            case PvdLevelSelection.V2_8:
                return 2.8;
            case PvdLevelSelection.V2_9:
                return 2.9;
            case PvdLevelSelection.ExternalInput:
            default:
                return null;
            }
        }

        private void UpdatePvd()
        {
            if(PvdLevel == PvdLevelSelection.ExternalInput)
            {
                // External input is not supported yet, skip updating the pvd
                return;
            }

            bool pvdo;
            if(prevPvdo && Voltage > ThresholdVoltage + Hysteresis)
            {
                // PVDO should be false if the voltage was below and is above the threshold
                pvdo = false;
            }
            else if(!prevPvdo && Voltage < ThresholdVoltage - Hysteresis)
            {
                // PVDO should be true if the voltage was above and is below the threshold
                pvdo = true;
            }
            else
            {
                // No change (within hysteresis)
                pvdo = prevPvdo;
            }
            prevPvdo = pvdo;
            pvdo &= pvdEnableFlag.Value;

            pvdoFlag.Value = pvdo;
            IRQ.Set(pvdo);
        }

        private IFlagRegisterField[] wakeupFlags = new IFlagRegisterField[8];
        private IFlagRegisterField standbyFlag;
        private IFlagRegisterField stopFlag;
        private IFlagRegisterField pvdoFlag;
        private IFlagRegisterField pvdEnableFlag;
        private IEnumRegisterField<PvdLevelSelection> pvdLevel;
        private IEnumRegisterField<VoltageScalingRangeSelection> vosValue;
        private IFlagRegisterField backupDomainDisabled;
        private double voltage;
        private bool prevPvdo;

        private const double Hysteresis = 0.1;

        public enum PvdLevelSelection
        {
            V2_0          = 0b000,
            V2_2          = 0b001,
            V2_4          = 0b010,
            V2_5          = 0b011,
            V2_6          = 0b100,
            V2_8          = 0b101,
            V2_9          = 0b110,
            ExternalInput = 0b111,
        }

        private enum Registers
        {
            Control1                       = 0x00,
            Control2                       = 0x04,
            Control3                       = 0x08,
            VoltageScaling                 = 0x0C,
            SupplyVoltageMonitoringControl = 0x10,
            WakeupControl1                 = 0x14,
            WakeupControl2                 = 0x18,
            WakeupControl3                 = 0x1C,
            // Reserved                    = 0x20 - 0x24
            BackupDomain                   = 0x28,
            SecurityConfiguration          = 0x30,
            PrivilegeControl               = 0x34,
            Status                         = 0x38,
            SupplyVoltageMonitoringStatus  = 0x3C,
            // Reserved                    = 0x40
            WakeupStatus                   = 0x44,
            WakeupStatusClear              = 0x48,
            // Reserved                    = 0x4C
            GpioAStandbyEnable             = 0x50,
            GpioAStandbyStatus             = 0x54,
            GpioBStandbyEnable             = 0x58,
            GpioBStandbyStatus             = 0x5C,
            GpioCStandbyEnable             = 0x60,
            GpioCStandbyStatus             = 0x64,
            // Reserved                    = 0x68 - 0x84
            GpioHStandbyEnable             = 0x88,
            GpioHStandbyStatus             = 0x8C,
            // Reserved                    = 0x90 - 0xFC
            RadioStatusAndControl          = 0x100,
        }

        private enum VoltageScalingRangeSelection
        {
            LowestPower,
            HighestFrequency,
        }
    }
}
