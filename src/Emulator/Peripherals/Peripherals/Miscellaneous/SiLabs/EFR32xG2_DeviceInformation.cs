//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EFR32xG2_DeviceInformation : DeviceInformation, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_DeviceInformation(DeviceFamily deviceFamily, ushort deviceNumber, MappedMemory flashDevice, MappedMemory sramDevice, byte productRevision = 0)
            : base(deviceFamily, deviceNumber, flashDevice, sramDevice, productRevision)
        {
            UID = ++count;
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

        private static uint count = 0;
        private ulong UID;
        public long Size => 0x400;
        public static readonly ulong OUI64 = 0xCCCCCC0000000000;
        public ulong EUI48 
        { 
            get
            {
                // TODO: for now we just return the UID
                return (ulong)UID;
            }
        }
        public ulong EUI64 
        { 
            get
            {
                return (ulong)(OUI64 + UID);
            } 
        }

        private DoubleWordRegisterCollection BuildRegisters()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ModuleInformation, new DoubleWordRegister(this, 0xFFFFFFFF)
                },
                {(long)Registers.ExtendedUniqueIdentifier48Low, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 24, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI48 & 0xFFFFFF), name: "UNIQUEID")
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => (uint)((EUI48 & 0xFFFFFFFF) >> 24), name: "OUI48L")
                },
                {(long)Registers.ExtendedUniqueIdentifier48High, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI48 >> 48), name: "OUI48H")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ExtendedUniqueIdentifier64Low, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI64 & 0xFFFFFFFF), name: "UNIQUEL")
                },
                {(long)Registers.ExtendedUniqueIdentifier64High, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (uint)((EUI64 >> 32) & 0xFF), name: "UNIQUEH")
                    .WithValueField(8, 24, FieldMode.Read, valueProviderCallback: _ => (uint)(EUI64 >> 40), name: "OUI64")
                },
                {(long)Registers.MemoryInformation, new DoubleWordRegister(this, 0x04000003)
                    .WithTag("FLASH_PAGE_SIZE", 0, 8)
                    .WithTag("USER_DATA_PAGE_SIZE", 8, 8)
                    .WithTag("DI_AREA_LENGTH", 16, 16)
                },
                {(long)Registers.SoftwareRestriction0, new DoubleWordRegister(this, 0x111112)
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
                {(long)Registers.SoftwareRestriction1, new DoubleWordRegister(this, 0x1F)
                    .WithTaggedFlag("RFMCUEN", 0)
                    .WithTaggedFlag("NCPEN", 1)
                    .WithTaggedFlag("GWEN", 2)
                    .WithTaggedFlag("XOUT", 3)
                    .WithTaggedFlag("FENOTCH", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.MemorySize, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => flashSize, name: "FLASH")
                    .WithValueField(16, 11, FieldMode.Read, valueProviderCallback: _ => sramSize, name: "SRAM")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.PartInformation, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => deviceNumber, name: "DEVICE_NUMBER")
                    .WithValueField(16, 6, FieldMode.Read, valueProviderCallback: _ => (uint)deviceFamily, name: "FAMILY_NUMBER")
                    .WithReservedBits(22, 2)
                    .WithValueField(24, 6, FieldMode.Read, valueProviderCallback: _ => productRevision, name: "DEVICE_FAMILY")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration0, new DoubleWordRegister(this, 0xB040_1E44)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration1, new DoubleWordRegister(this, 0xA041_1F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration2, new DoubleWordRegister(this, 0xA042_1F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration3, new DoubleWordRegister(this, 0xF443_A040)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration4, new DoubleWordRegister(this, 0xE444_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration5, new DoubleWordRegister(this, 0xD445_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration6, new DoubleWordRegister(this, 0xD466_A23F)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration7, new DoubleWordRegister(this, 0xD467_A03F)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration8, new DoubleWordRegister(this, 0xD868_9C3C)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration9, new DoubleWordRegister(this, 0xC889_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration10, new DoubleWordRegister(this, 0xC88A_A13E)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration11, new DoubleWordRegister(this, 0xC8AB_A042)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration12, new DoubleWordRegister(this, 0xC8CC_9D3D)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration13, new DoubleWordRegister(this, 0xDCED_9E3E)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration14, new DoubleWordRegister(this, 0xDCEE_9F3C)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration15, new DoubleWordRegister(this, 0xDCEF_A040)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration16, new DoubleWordRegister(this, 0xDCF0_A23B)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration17, new DoubleWordRegister(this, 0xDCF1_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration0, new DoubleWordRegister(this, 0xB040_1C45)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration1, new DoubleWordRegister(this, 0xA041_1E3C)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration2, new DoubleWordRegister(this, 0xA042_1F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration3, new DoubleWordRegister(this, 0xF443_A040)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration4, new DoubleWordRegister(this, 0xE444_A02F)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration5, new DoubleWordRegister(this, 0xD445_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration6, new DoubleWordRegister(this, 0xD466_9F40)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration7, new DoubleWordRegister(this, 0xD467_9D40)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration8, new DoubleWordRegister(this, 0xD868_A23C)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration9, new DoubleWordRegister(this, 0xC889_9C54)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration10, new DoubleWordRegister(this, 0xC88A_9C40)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration11, new DoubleWordRegister(this, 0xC8AB_A043)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration12, new DoubleWordRegister(this, 0xC8CC_A136)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration13, new DoubleWordRegister(this, 0xDCED_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration14, new DoubleWordRegister(this, 0xDCEE_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration15, new DoubleWordRegister(this, 0xDCEF_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration16, new DoubleWordRegister(this, 0xDCF0_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.HighFrequencyRcOscillatorEm23Calibration17, new DoubleWordRegister(this, 0xDCF1_9F3A)
                    .WithTag("TUNING", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("FINETUNING", 8, 6)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("LDOHP", 15)
                    .WithTag("FREQRANGE", 16, 5)
                    .WithTag("CMPBIAS", 21, 3)
                    .WithTag("CLKDIV", 24, 2)
                    .WithTag("CMPSEL", 26, 2)
                    .WithTag("IREFTC", 28, 4)
                },
                {(long)Registers.LegacyDeviceInformation, new DoubleWordRegister(this, 0x0080_0000)
                    .WithReservedBits(0, 16)
                    .WithTag("DEVICEFAMILY", 16, 8)
                    .WithReservedBits(24, 8)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            DeviceInformation                                               = 0x000,
            PartInformation                                                 = 0x004,
            MemoryInformation                                               = 0x008,
            MemorySize                                                      = 0x00C,
            MiscDeviceInformation                                           = 0x010,
            CustomPartInformation                                           = 0x014,
            SoftwareFix                                                     = 0x018,
            SoftwareRestriction0                                            = 0x01C,
            SoftwareRestriction1                                            = 0x020,
            ExternalComponentInformation                                    = 0x028,
            ExtendedUniqueIdentifier48Low                                   = 0x040,
            ExtendedUniqueIdentifier48High                                  = 0x044,
            ExtendedUniqueIdentifier64Low                                   = 0x048,
            ExtendedUniqueIdentifier64High                                  = 0x04C,
            CalibrationTemperature                                          = 0x050,
            EnergyManagementUnitTemperatureCalibration                      = 0x054,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration0    = 0x058,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration1    = 0x05C,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration2    = 0x060,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration3    = 0x064,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration4    = 0x068,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration5    = 0x06C,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration6    = 0x070,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration7    = 0x074,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration8    = 0x078,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration9    = 0x07C,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration10   = 0x080,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration11   = 0x084,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration12   = 0x088,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration13   = 0x08C,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration14   = 0x090,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration15   = 0x094,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration16   = 0x098,
            HighFrequencyRcOscillatorDigitalPhasedLockedLoopCalibration17   = 0x09C,
            HighFrequencyRcOscillatorEm23Calibration0                       = 0x0A0,
            HighFrequencyRcOscillatorEm23Calibration1                       = 0x0A4,
            HighFrequencyRcOscillatorEm23Calibration2                       = 0x0A8,
            HighFrequencyRcOscillatorEm23Calibration3                       = 0x0AC,
            HighFrequencyRcOscillatorEm23Calibration4                       = 0x0B0,
            HighFrequencyRcOscillatorEm23Calibration5                       = 0x0B4,
            HighFrequencyRcOscillatorEm23Calibration6                       = 0x0B8,
            HighFrequencyRcOscillatorEm23Calibration7                       = 0x0BC,
            HighFrequencyRcOscillatorEm23Calibration8                       = 0x0C0,
            HighFrequencyRcOscillatorEm23Calibration9                       = 0x0C4,
            HighFrequencyRcOscillatorEm23Calibration10                      = 0x0C8,
            HighFrequencyRcOscillatorEm23Calibration11                      = 0x0CC,
            HighFrequencyRcOscillatorEm23Calibration12                      = 0x0D0,
            HighFrequencyRcOscillatorEm23Calibration13                      = 0x0D4,
            HighFrequencyRcOscillatorEm23Calibration14                      = 0x0D8,
            HighFrequencyRcOscillatorEm23Calibration15                      = 0x0DC,
            HighFrequencyRcOscillatorEm23Calibration16                      = 0x0E0,
            HighFrequencyRcOscillatorEm23Calibration17                      = 0x0E4,
            ModuleName0Information                                          = 0x130,
            ModuleName1Information                                          = 0x134,
            ModuleName2Information                                          = 0x138,
            ModuleName3Information                                          = 0x13C,
            ModuleName4Information                                          = 0x140,
            ModuleName5Information                                          = 0x144,
            ModuleName6Information                                          = 0x148,
            ModuleInformation                                               = 0x14C,
            ModuleExternalOscillatorCalibrationInformation                  = 0x150,
            IncrementalAnalogDigitalConverterGain0Calibration               = 0x180,
            IncrementalAnalogDigitalConverterGain1Calibration               = 0x184,
            IncrementalAnalogDigitalConverterOffsetCalibration              = 0x188,
            IncrementalAnalogDigitalConverterNormalOffsetCalibrarion0       = 0x18C,
            IncrementalAnalogDigitalConverterNormalOffsetCalibrarion1       = 0x190,
            IncrementalAnalogDigitalConverterHighSpeedOffsetCalibration0    = 0x194,
            IncrementalAnalogDigitalConverterHighSpeedOffsetCalibration1    = 0x198,
            LegacyDeviceInformation                                         = 0x1FC,
            ThermistorCalibration                                           = 0x25C,
            FenotchCalibration                                              = 0x264,
        }
    }
}
