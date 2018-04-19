//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class EFM32DeviceInformation: IDoubleWordPeripheral, IKnownSize
    {
        public EFM32DeviceInformation(Family deviceFamily, ushort deviceNumber, ushort flashSize, ushort sramSize, byte productRevision = 0)
        {
            this.deviceFamily = deviceFamily;
            this.flashSize = flashSize;
            this.sramSize = sramSize;
            this.productRevision = productRevision;
            this.deviceNumber = deviceNumber;
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((DeviceInformationOffset)offset)
            {
            case DeviceInformationOffset.EUI48L:
                return (uint)(EUI >> 32);
            case DeviceInformationOffset.EUI48H:
                return (uint)(EUI & 0xFFFFFFFF);
            case DeviceInformationOffset.UNIQUEL:
                return (uint)(Unique >> 32);
            case DeviceInformationOffset.UNIQUEH:
                return (uint)(Unique & 0xFFFFFFFF);
            case DeviceInformationOffset.MSIZE:
                return (uint)((sramSize << 16) | (flashSize & 0xFFFF));
            case DeviceInformationOffset.PART:
                return (uint)((productRevision << 24) | ((byte)deviceFamily << 16) | deviceNumber);
            case DeviceInformationOffset.DEVINFOREV:
                return 0xFFFFFF00 | 0x01;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.LogUnhandledWrite(offset, value);
        }

        public long Size
        {
            get
            {
                return 0x184;
            }
        }

        public ulong EUI { get; set; }
        public ulong Unique { get; set; }

        private readonly Family deviceFamily;
        private readonly ushort flashSize;
        private readonly ushort sramSize;
        private readonly byte productRevision;
        private readonly ushort deviceNumber;

        [AllowAnyNumericalValue]
        public enum Family : byte
        {
            Gecko = 0x47,
            GiantGecko = 0x48,
            TinyGecko = 0x49,
            LeopardGecko = 0x4A
        }

        // more info can be found in efr32bg1b_devinfo.h
        private enum DeviceInformationOffset : long
        {
            CAL              = 0x000, // CRC of DI-page and calibration temperature
            EUI48L           = 0x028, // EUI48 OUI and Unique identifier
            EUI48H           = 0x02C, // OUI
            CUSTOMINFO       = 0x030, // Custom information
            MEMINFO          = 0x034, // Flash page size and misc. chip information
            UNIQUEL          = 0x040, // Low 32 bits of device unique number
            UNIQUEH          = 0x044, // High 32 bits of device unique number
            MSIZE            = 0x048, // Flash and SRAM Memory size in kB
            PART             = 0x04C, // Part description
            DEVINFOREV       = 0x050, // Device information page revision
            EMUTEMP          = 0x054, // EMU Temperature Calibration Information
            ADC0CAL0         = 0x060, // ADC0 calibration register 0
            ADC0CAL1         = 0x064, // ADC0 calibration register 1
            ADC0CAL2         = 0x068, // ADC0 calibration register 2
            ADC0CAL3         = 0x06C, // ADC0 calibration register 3
            HFRCOCAL0        = 0x080, // HFRCO Calibration Register (4 MHz)
            HFRCOCAL3        = 0x08C, // HFRCO Calibration Register (7 MHz)
            HFRCOCAL6        = 0x098, // HFRCO Calibration Register (13 MHz)
            HFRCOCAL7        = 0x09C, // HFRCO Calibration Register (16 MHz)
            HFRCOCAL8        = 0x0A0, // HFRCO Calibration Register (19 MHz)
            HFRCOCAL10       = 0x0A8, // HFRCO Calibration Register (26 MHz)
            HFRCOCAL11       = 0x0AC, // HFRCO Calibration Register (32 MHz)
            HFRCOCAL12       = 0x0B0, // HFRCO Calibration Register (38 MHz)
            AUXHFRCOCAL0     = 0x0E0, // AUXHFRCO Calibration Register (4 MHz)
            AUXHFRCOCAL3     = 0x0EC, // AUXHFRCO Calibration Register (7 MHz)
            AUXHFRCOCAL6     = 0x0F8, // AUXHFRCO Calibration Register (13 MHz)
            AUXHFRCOCAL7     = 0x0FC, // AUXHFRCO Calibration Register (16 MHz)
            AUXHFRCOCAL8     = 0x100, // AUXHFRCO Calibration Register (19 MHz)
            AUXHFRCOCAL10    = 0x108, // AUXHFRCO Calibration Register (26 MHz)
            AUXHFRCOCAL11    = 0x10C, // AUXHFRCO Calibration Register (32 MHz)
            AUXHFRCOCAL12    = 0x110, // AUXHFRCO Calibration Register (38 MHz)
            VMONCAL0         = 0x140, // VMON Calibration Register 0
            VMONCAL1         = 0x144, // VMON Calibration Register 1
            VMONCAL2         = 0x148, // VMON Calibration Register 2
            IDAC0CAL0        = 0x158, // IDAC0 Calibration Register 0
            IDAC0CAL1        = 0x15C, // IDAC0 Calibration Register 1
            DCDCLNVCTRL0     = 0x168, // DCDC Low-noise VREF Trim Register 0
            DCDCLPVCTRL0     = 0x16C, // DCDC Low-power VREF Trim Register 0
            DCDCLPVCTRL1     = 0x170, // DCDC Low-power VREF Trim Register 1
            DCDCLPVCTRL2     = 0x174, // DCDC Low-power VREF Trim Register 2
            DCDCLPVCTRL3     = 0x178, // DCDC Low-power VREF Trim Register 3
            DCDCLPCMPHYSSEL0 = 0x17C, // DCDC LPCMPHYSSEL Trim Register 0
            DCDCLPCMPHYSSEL1 = 0x180, // DCDC LPCMPHYSSEL Trim Register 1
        }
    }
}
