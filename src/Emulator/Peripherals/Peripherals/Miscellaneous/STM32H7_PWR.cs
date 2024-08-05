//
// Copyright (c) 2024 Antmicro
// Copyright (c) 2024 Nu Quantum Ltd
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32H7_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H7_PWR(IMachine machine) : base(machine)
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
            Registers.Control1.Define(this, 0xF000_C000)
                // The LPDS flag has no functionality because low-power run mode is not implemented
                .WithFlag(0, name: "LPDS")
                .WithReservedBits(1, 3)
                .WithFlag(4, out pvdEnableFlag, name: "PVDE")
                .WithEnumField<DoubleWordRegister, PvdLevelSelection>(5, 3, out pvdLevel, writeCallback: (_, value) =>
                    {
                        if(value == PvdLevelSelection.ExternalInput)
                        {
                            this.Log(LogLevel.Warning, "External PVD input selected, this is not supported");
                        }
                        UpdatePvd();
                    }, name: "PLS")
                .WithFlag(8, out backupDomainDisabled, name: "DBP")
                // The FLPS flag has no functionality as the respective functionality is not implemented
                .WithFlag(9, name: "FLPS")
                .WithReservedBits(10, 4)
                .WithEnumField<DoubleWordRegister, StopModeVoltageScalingSelection>(14, 2, out vsosValue, name: "VSOS")
                .WithFlag(16, out avdEnableFlag, name: "AVDEN")
                .WithEnumField<DoubleWordRegister, AvdLevelSelection>(17, 2, out avdLevel, writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Warning, $"avdLevel set to {value:X}");
                        this.Log(LogLevel.Warning, $"avdLevel is {avdLevel.Value:X}");
                        UpdateAvd();
                    }, name: "ALS")

                .WithReservedBits(19, 13);

            Registers.ControlStatus1.Define(this, 0x0000_4000)
                .WithReservedBits(0, 4)
                .WithFlag(4, out pvdoFlag, FieldMode.Read, name: "PVDO")
                .WithReservedBits(5, 8)
                .WithFlag(13, mode: FieldMode.Read, name: "ACTVOSRDY", valueProviderCallback: _ => true)
                .WithValueField(14, 2, FieldMode.Read, name: "ACTVOS", valueProviderCallback: _ => (uint)vosValue.Value)
                .WithFlag(16, out avdoFlag, FieldMode.Read, name: "AVDO")
                .WithReservedBits(17, 15);

            Registers.Control2.Define(this)
                .WithTaggedFlag("BREN", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MONEN", 4)
                .WithReservedBits(5, 11)
                .WithTaggedFlag("BRRDY", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("VBATL", 20)
                .WithTaggedFlag("VBATH", 21)
                .WithTaggedFlag("TEMPL", 22)
                .WithTaggedFlag("TEMPH", 23)
                .WithReservedBits(24, 8);

            Registers.Control3.Define(this, 0x0000_0046)
                .WithFlag(0, name: "BYPASS")
                .WithFlag(1, name: "LDOEN")
                .WithFlag(2, name: "SCUEN")
                .WithReservedBits(3, 5)
                .WithFlag(8, name: "VBE")
                .WithFlag(9, name: "VBRS")
                .WithReservedBits(10, 14)
                .WithFlag(24, name: "USB33DEN")
                .WithFlag(25, name: "USBREGEN")
                .WithFlag(26, name: "USB33RDY")
                .WithReservedBits(27, 5);

            Registers.CPUControl1.Define(this)
                .WithTaggedFlag("PDDS_D1", 0)
                .WithTaggedFlag("PDDS_D2", 1)
                .WithTaggedFlag("PDDS_D3", 2)
                .WithReservedBits(3, 2)
                .WithFlag(5, out stopFlag, FieldMode.Read, name: "STOPF")
                .WithFlag(6, out standbyFlag, FieldMode.Read, name: "SBF")
                .WithFlag(7, out standbyFlagD1, FieldMode.Read, name: "SBF_D1")
                .WithFlag(8, out standbyFlagD2, FieldMode.Read, name: "SBF_D2")
                .WithFlag(9, FieldMode.WriteOneToClear, name: "CSSF",
                    writeCallback: (_, __) =>
                    {
                        stopFlag.Value = false;
                        standbyFlag.Value = false;
                        standbyFlagD1.Value = false;
                        standbyFlagD2.Value = false;
                    })
                .WithReservedBits(10, 1)
                .WithTaggedFlag("RUN_D3", 11)
                .WithReservedBits(12, 20);

            Registers.D3DomainControl.Define(this, 0x0000_4000)
                .WithReservedBits(0, 13)
                .WithFlag(13, mode: FieldMode.Read, name: "VOSRDY", valueProviderCallback: _ => true)
                .WithEnumField<DoubleWordRegister, VoltageScalingRangeSelection>(14, 2, out vosValue, name: "VOS")
                .WithReservedBits(16, 16);

            Registers.WakeupFlag.Define(this)
                .WithFlags(0, 6, out wakeupFlags, FieldMode.Read, name: "WKUPF")
                .WithReservedBits(6, 26);

            Registers.WakeupClear.Define(this)
                .WithFlags(0, 6, FieldMode.WriteOneToClear, name: "WKUPC",
                    writeCallback: (idx, _, __) => wakeupFlags[idx].Value = false)
                .WithReservedBits(6, 26);

            Registers.WakeupEnableAndPolarity.Define(this)
                .WithTaggedFlag("WKUPEN1", 0)
                .WithTaggedFlag("WKUPEN2", 1)
                .WithTaggedFlag("WKUPEN3", 2)
                .WithTaggedFlag("WKUPEN4", 3)
                .WithTaggedFlag("WKUPEN5", 4)
                .WithTaggedFlag("WKUPEN6", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("WKUPP1", 8)
                .WithTaggedFlag("WKUPP2", 9)
                .WithTaggedFlag("WKUPP3", 10)
                .WithTaggedFlag("WKUPP4", 11)
                .WithTaggedFlag("WKUPP5", 12)
                .WithTaggedFlag("WKUPP6", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlags("WKUPPUPD1", 16, 2)
                .WithTaggedFlags("WKUPPUPD2", 18, 2)
                .WithTaggedFlags("WKUPPUPD3", 20, 2)
                .WithTaggedFlags("WKUPPUPD4", 22, 2)
                .WithTaggedFlags("WKUPPUPD5", 24, 2)
                .WithTaggedFlags("WKUPPUPD6", 26, 2)
                .WithReservedBits(28, 4);
        }

        private double? PvdLevelToVoltage(PvdLevelSelection level)
        {
            switch(level)
            {
            case PvdLevelSelection.V1_95:
                return 1.95;
            case PvdLevelSelection.V2_1:
                return 2.1;
            case PvdLevelSelection.V2_25:
                return 2.25;
            case PvdLevelSelection.V2_4:
                return 2.4;
            case PvdLevelSelection.V2_55:
                return 2.55;
            case PvdLevelSelection.V2_7:
                return 2.7;
            case PvdLevelSelection.V2_85:
                return 2.85;
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

        private void UpdateAvd()
        {
            this.Log(LogLevel.Warning, "REVISIT: AVD not implemented");
        }

        private IFlagRegisterField[] wakeupFlags = new IFlagRegisterField[6];
        private IFlagRegisterField standbyFlag;
        private IFlagRegisterField standbyFlagD1;
        private IFlagRegisterField standbyFlagD2;
        private IFlagRegisterField stopFlag;
        private IFlagRegisterField avdoFlag;
        private IFlagRegisterField avdEnableFlag;
        private IEnumRegisterField<AvdLevelSelection> avdLevel;
        private IFlagRegisterField pvdoFlag;
        private IFlagRegisterField pvdEnableFlag;
        private IEnumRegisterField<PvdLevelSelection> pvdLevel;
        private IEnumRegisterField<VoltageScalingRangeSelection> vosValue;
        private IEnumRegisterField<StopModeVoltageScalingSelection> vsosValue;
        private IFlagRegisterField backupDomainDisabled;
        private double voltage;
        private bool prevPvdo;

        private const double Hysteresis = 0.1;

        public enum AvdLevelSelection
        {
            V1_7 = 0b00,
            V2_1 = 0b01,
            V2_5 = 0b10,
            V2_8 = 0b11,
        }

        public enum PvdLevelSelection
        {
            V1_95         = 0b000,
            V2_1          = 0b001,
            V2_25         = 0b010,
            V2_4          = 0b011,
            V2_55         = 0b100,
            V2_7          = 0b101,
            V2_85         = 0b110,
            ExternalInput = 0b111,
        }

        private enum Registers
        {
            Control1                       = 0x00,
            ControlStatus1                 = 0x04,
            Control2                       = 0x08,
            Control3                       = 0x0C,
            CPUControl1                    = 0x10,
            // Reserved                    = 0x14,
            D3DomainControl                = 0x18,
            WakeupClear                    = 0x20,
            WakeupFlag                     = 0x24,
            WakeupEnableAndPolarity        = 0x28,
        }

        private enum VoltageScalingRangeSelection
        {
            Reserved = 0b00,
            Scale3   = 0b01, // Default
            Scale2   = 0b10,
            Scale1   = 0b11,
        }

        private enum StopModeVoltageScalingSelection
        {
            Reserved = 0b00,
            Scale5   = 0b01,
            Scale4   = 0b10,
            Scale3   = 0b00,
        }
    }
}
