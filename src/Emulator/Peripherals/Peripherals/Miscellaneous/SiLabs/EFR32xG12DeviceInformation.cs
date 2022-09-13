//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EFR32xG12DeviceInformation : DeviceInformation, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG12DeviceInformation(DeviceFamily deviceFamily, ushort deviceNumber, MappedMemory flashDevice, MappedMemory sramDevice, byte productRevision = 0)
            : base(deviceFamily, deviceNumber, flashDevice, sramDevice, productRevision)
        {
            registers = BuildRegisters();
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x200;
        public ulong EUI48 { get; set; }

        private DoubleWordRegisterCollection BuildRegisters()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ModuleInfo, new DoubleWordRegister(this, 0xffffffff)
                },
                {(long)Registers.SoftwareFix, new DoubleWordRegister(this, 0x1)
                    .WithTaggedFlag("PARANGE", 0)
                    .WithTag("SPARE", 1, 31)
                },
                {(long)Registers.ExtendedUniqueIdentifier48Low, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 24, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI48 >> 24), name: "UNIQUEID")
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI48 >> 16), name: "OUI48L")
                },
                {(long)Registers.ExtendedUniqueIdentifier48High, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)EUI48, name: "OUI48H")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.MemoryInfo, new DoubleWordRegister(this, 0x17D4C00)
                    .WithTag("TEMPGRADE", 0, 8)
                    .WithTag("PKGTYPE", 8, 8)
                    .WithTag("PINCOUNT", 16, 8)
                    .WithTag("FLASH_PAGE_SIZE", 24, 8)
                },
                {(long)Registers.SoftwareCapabilityVector0, new DoubleWordRegister(this, 0x111112)
                    .WithTag("ZIGBEE", 0, 2)
                    .WithReservedBits(2, 2)
                    .WithTag("THREAD", 4, 2)
                    .WithReservedBits(6, 2)
                    .WithTag("RF4CE", 8, 2)
                    .WithReservedBits(10, 2)
                    .WithTag("BTSMART", 12, 2)
                    .WithReservedBits(14, 2)
                    .WithTag("CONNECT", 16, 2)
                    .WithReservedBits(18, 2)
                    .WithTag("SRI", 20, 2)
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.SoftwareCapabilityVector1, new DoubleWordRegister(this, 0x7)
                    .WithTaggedFlag("RFMCUEN", 0)
                    .WithTaggedFlag("NCPEN", 1)
                    .WithTaggedFlag("GWEN", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.DeviceUniqueNumberLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(Unique >> 32), name: "UNIQUEL")
                },
                {(long)Registers.DeviceUniqueNumberHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)Unique, name: "UNIQUEH")
                },
                {(long)Registers.MemorySize, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => flashSize, name: "FLASH")
                    .WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => sramSize, name: "SRAM")
                },
                {(long)Registers.PartDescription, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => deviceNumber, name: "DEVICE_NUMBER")
                    .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => (uint)deviceFamily, name: "DEVICE_FAMILY")
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => productRevision, name: "PROD_REV")
                },
                {(long)Registers.DeviceInformationPageRevision, new DoubleWordRegister(this, 0x1)
                    .WithTag("DEVINFOREV", 0, 8)
                    .WithReservedBits(8, 24)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            CRCAndTemperatureCalibration                                                            = 0x000,
            ModuleInfo                                                                              = 0x004,
            IntermediateFrequencyProgrammableGainAmplifierCalibration0                              = 0x010,
            IntermediateFrequencyProgrammableGainAmplifierCalibration1                              = 0x014,
            IntermediateFrequencyProgrammableGainAmplifierCalibration2                              = 0x018,
            IntermediateFrequencyProgrammableGainAmplifierCalibration3                              = 0x01C,
            ExternalComponentDescription                                                            = 0x020,
            SoftwareFix                                                                             = 0x024,
            ExtendedUniqueIdentifier48Low                                                           = 0x028,
            ExtendedUniqueIdentifier48High                                                          = 0x02C,
            CustomInformation                                                                       = 0x030,
            MemoryInfo                                                                              = 0x034,
            SoftwareCapabilityVector0                                                               = 0x038,
            SoftwareCapabilityVector1                                                               = 0x03C,
            DeviceUniqueNumberLow                                                                   = 0x040,
            DeviceUniqueNumberHigh                                                                  = 0x044,
            MemorySize                                                                              = 0x048,
            PartDescription                                                                         = 0x04C,
            DeviceInformationPageRevision                                                           = 0x050,
            EnergyManagmentUnitTemperatureCalibration                                               = 0x054,
            AnalogToDigitalConverter0Calibration0                                                   = 0x060,
            AnalogToDigitalConverter0Calibration1                                                   = 0x064,
            AnalogToDigitalConverter0Calibration2                                                   = 0x068,
            AnalogToDigitalConverter0Calibration3                                                   = 0x06C,
            MediumAccuracyResistorCapacitorOscillatorCalibration0                                   = 0x080,
            MediumAccuracyResistorCapacitorOscillatorCalibration3                                   = 0x08C,
            MediumAccuracyResistorCapacitorOscillatorCalibration6                                   = 0x098,
            MediumAccuracyResistorCapacitorOscillatorCalibration7                                   = 0x09C,
            MediumAccuracyResistorCapacitorOscillatorCalibration8                                   = 0x0A0,
            MediumAccuracyResistorCapacitorOscillatorCalibration10                                  = 0x0A8,
            MediumAccuracyResistorCapacitorOscillatorCalibration11                                  = 0x0AC,
            MediumAccuracyResistorCapacitorOscillatorCalibration12                                  = 0x0B0,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration0                          = 0x0E0,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration3                          = 0x0EC,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration6                          = 0x0F8,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration7                          = 0x0FC,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration8                          = 0x100,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration10                         = 0x108,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration11                         = 0x10C,
            AuxiliaryMediumAccuracyResistorCapacitorOscillatorCalibration12                         = 0x110,
            VoltageMonitorCalibration0                                                              = 0x140,
            VoltageMonitorCalibration1                                                              = 0x144,
            VoltageMonitorCalibration2                                                              = 0x148,
            VoltageMonitorCalibration3                                                              = 0x14C,
            CurrentDigitalToAnalogConverter0Calibration0                                            = 0x158,
            CurrentDigitalToAnalogConverter0Calibration1                                            = 0x15C,
            DirectCurrentToDirectCurrentConverterLowNoiseReferenceVoltageCalibration0               = 0x168,
            DirectCurrentToDirectCurrentConverterLowPowerReferenceVoltageCalibration0               = 0x16C,
            DirectCurrentToDirectCurrentConverterLowPowerReferenceVoltageCalibration1               = 0x170,
            DirectCurrentToDirectCurrentConverterLowPowerReferenceVoltageCalibration2               = 0x174,
            DirectCurrentToDirectCurrentConverterLowPowerReferenceVoltageCalibration3               = 0x178,
            DirectCurrentToDirectCurrentConverterLowPowerComparatorHysteresisSelectionCalibration0  = 0x17C,
            DirectCurrentToDirectCurrentConverterLowPowerComparatorHysteresisSelectionCalibration1  = 0x180,
            DigitalToAnalogConverter0MainPathCalibration                                            = 0x184,
            DigitalToAnalogConverter0AlternatePathCalibration                                       = 0x188,
            DigitalToAnalogConverter0Channel1Calibration                                            = 0x18C,
            OperationalAmplifier0Calibration0                                                       = 0x190,
            OperationalAmplifier0Calibration1                                                       = 0x194,
            OperationalAmplifier0Calibration2                                                       = 0x198,
            OperationalAmplifier0Calibration3                                                       = 0x19C,
            OperationalAmplifier1Calibration0                                                       = 0x1A0,
            OperationalAmplifier1Calibration1                                                       = 0x1A4,
            OperationalAmplifier1Calibration2                                                       = 0x1A8,
            OperationalAmplifier1Calibration3                                                       = 0x1AC,
            OperationalAmplifier2Calibration0                                                       = 0x1B0,
            OperationalAmplifier2Calibration1                                                       = 0x1B4,
            OperationalAmplifier2Calibration2                                                       = 0x1B8,
            OperationalAmplifier2Calibration3                                                       = 0x1BC,
            CapacitiveSensorGainAdjustment                                                          = 0x1C0,
        }
    }
}
