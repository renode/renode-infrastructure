//
// Copyright (c) 2022 SICK AG
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32_PWR : IDoubleWordPeripheral, IKnownSize
    {
        public STM32_PWR(Machine machine)
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PowerControl, new DoubleWordRegister(this, 0xFCC00)
                    .WithFlag(0, name: "LPDS")
                    .WithFlag(1, name: "PDDS")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "CWUF", valueProviderCallback: _ => { return false; }, writeCallback: (_, value) => {
                            if (value)
                            {
                                wufValue.Value = false;
                            }
                        })
                    .WithFlag(3, FieldMode.Read | FieldMode.WriteOneToClear, name: "CSBF", valueProviderCallback: _ => { return false; }, writeCallback: (_, value) => {
                            if (value)
                            {
                                sbfValue.Value = false;
                            }
                        })
                    .WithFlag(4, name: "PVDE")
                    .WithEnumField<DoubleWordRegister, PvdLevelSelection>(5, 3, name: "PLS")
                    .WithFlag(8, name: "DBP")
                    .WithFlag(9, name: "FPDS")
                    .WithFlag(10, name: "LPUDS")
                    .WithFlag(11, name: "MRUDS")
                    .WithReservedBits(12, 1)
                    .WithFlag(13, name: "ADCD1")
                    .WithEnumField<DoubleWordRegister, RegulatorVoltageScalingOutputSelection>(14, 2, out vosValue, name: "VOS", writeCallback: (_, value) => {
                            if (value == RegulatorVoltageScalingOutputSelection.Reserved)
                            {
                                vosValue.Value = RegulatorVoltageScalingOutputSelection.ScaleMode3;
                            }
                        })
                    .WithFlag(16, name: "ODEN", writeCallback: (_, value) => {
                            if (!value)
                            {
                                odswenValue.Value = false;
                            }

                            odrdyValue.Value = value;
                        })
                    .WithFlag(17, out odswenValue, name: "ODSWEN", writeCallback: (_, value) => { odswrdyValue.Value = value; })
                    .WithEnumField<DoubleWordRegister, UnderDriveEnableInStopMode>(18, 2, name: "UDEN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.PowerControlStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out wufValue, FieldMode.Read, name: "WUF")
                    .WithFlag(1, out sbfValue, FieldMode.Read, name: "SBF")
                    .WithFlag(2, FieldMode.Read, name: "PVDO")
                    .WithFlag(3, FieldMode.Read, name: "BRR")
                    .WithReservedBits(4, 4)
                    .WithFlag(8, name: "EWUP")
                    .WithFlag(9, name: "BER")
                    .WithReservedBits(10, 4)
                    .WithFlag(14, FieldMode.Read, name: "VOSRDY")
                    .WithReservedBits(15, 1)
                    .WithFlag(16, out odrdyValue, FieldMode.Read, name: "ODRDY")
                    .WithFlag(17, out odswrdyValue, FieldMode.Read, name: "ODSWRDY")
                    .WithEnumField<DoubleWordRegister, UnderDriveReady>(18, 2, FieldMode.Read | FieldMode.WriteOneToClear, name: "UDRDY")
                    .WithReservedBits(20, 12)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x400;

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
        }

        private readonly DoubleWordRegisterCollection registers;
        private IEnumRegisterField<RegulatorVoltageScalingOutputSelection> vosValue;
        private IFlagRegisterField odswenValue;
        private IFlagRegisterField wufValue;
        private IFlagRegisterField sbfValue;
        private IFlagRegisterField odrdyValue;
        private IFlagRegisterField odswrdyValue;

        private enum Registers : long
        {
            PowerControl = 0x0,
            PowerControlStatus = 0x4
        }

        private enum PvdLevelSelection
        {
            V2_0,
            V2_1,
            V2_3,
            V2_5,
            V2_6,
            V2_7,
            V2_8,
            V2_9
        }

        private enum RegulatorVoltageScalingOutputSelection
        {
            Reserved,
            ScaleMode3,
            ScaleMode2,
            ScaleMode1
        }

        private enum UnderDriveEnableInStopMode
        {
            UnderDriveDisable,
            Reserved1,
            Reserved2,
            UnderDriveEnable
        }

        private enum UnderDriveReady
        {
            UnderDriveDisabled,
            Reserved1,
            Reserved2,
            UnderDriveActivated
        }
    }
}