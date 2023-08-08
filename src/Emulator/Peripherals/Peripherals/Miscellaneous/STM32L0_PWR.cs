//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32L0_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32L0_PWR(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            Registers.PowerControl.Define(this, 0x1000)
                // The LPSDSR flag has no functionality because low-power run mode is not implemented
                .WithFlag(0, name: "LPSDSR")
                .WithTaggedFlag("PDDS", 1)
                .WithFlag(2, FieldMode.WriteOneToClear, writeCallback: (_, __) => wakeupFlag.Value = false, name: "CWUF")
                .WithFlag(3, FieldMode.WriteOneToClear, writeCallback: (_, __) => standbyFlag.Value = false, name: "CSBF")
                .WithFlag(4, out pvdEnableFlag, name: "PVDE")
                .WithEnumField<DoubleWordRegister, PvdLevelSelection>(5, 3, out pvdLevel, writeCallback: (_, value) =>
                    {
                        if(value == PvdLevelSelection.ExternalInput)
                        {
                            this.Log(LogLevel.Warning, "External PVD input selected, this is not supported");
                        }
                    }, name: "PLS")
                .WithFlag(8, name: "DBP")
                .WithTaggedFlag("ULP", 9)
                .WithTaggedFlag("FWU", 10)
                .WithEnumField<DoubleWordRegister, VoltageScalingRangeSelection>(11, 2, out vosValue, name: "VOS", writeCallback: (oldValue, value) =>
                    {
                        if(value == VoltageScalingRangeSelection.Forbidden)
                        {
                            this.Log(LogLevel.Warning, "Trying to set forbidden VOS value, ignoring write");
                            vosValue.Value = oldValue;
                        }
                    })
                .WithTaggedFlag("DS_EE_KOFF", 13)
                .WithTaggedFlag("LPRUN", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("LPDS", 16)
                .WithReservedBits(17, 15);

            Registers.PowerControlStatus.Define(this, 0x8)
                .WithFlag(0, out wakeupFlag, FieldMode.Read, name: "WUF")
                .WithFlag(1, out standbyFlag, FieldMode.Read, name: "SBF")
                .WithFlag(2, out pvdoFlag, FieldMode.Read, name: "PVDO")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "VREFINTRDYF")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "VOSF")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "REGLPF")
                .WithReservedBits(6, 2)
                .WithTaggedFlag("EWUP1", 8)
                .WithTaggedFlag("EWUP2", 9)
                .WithTaggedFlag("EWUP3", 10)
                .WithReservedBits(11, 21);

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

        public double? ThresholdVoltage
        {
            get
            {
                if(pvdLevel.Value == PvdLevelSelection.ExternalInput)
                {
                    return null;
                }
                else
                {
                    return 1.9 + (int)pvdLevel.Value * 0.2;
                }
            }
        }

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

        private void UpdatePvd()
        {
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

        private IFlagRegisterField wakeupFlag;
        private IFlagRegisterField standbyFlag;
        private IFlagRegisterField pvdoFlag;
        private IFlagRegisterField pvdEnableFlag;
        private IEnumRegisterField<PvdLevelSelection> pvdLevel;
        private IEnumRegisterField<VoltageScalingRangeSelection> vosValue;
        private double voltage;
        private bool prevPvdo;

        private const double Hysteresis = 0.1;

        public enum PvdLevelSelection
        {
            V1_9,
            V2_1,
            V2_3,
            V2_5,
            V2_7,
            V2_9,
            V3_1,
            ExternalInput,
        }

        private enum Registers
        {
            PowerControl = 0x00,
            PowerControlStatus = 0x04,
        }

        private enum VoltageScalingRangeSelection
        {
            Forbidden,
            Range1_V1_8,
            Range2_V1_5,
            Range3_V1_2,
        }
    }
}
