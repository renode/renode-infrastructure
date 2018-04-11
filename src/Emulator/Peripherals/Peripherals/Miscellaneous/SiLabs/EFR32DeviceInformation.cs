﻿//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2017 Bas Stottelaar <basstottelaar@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EFR32DeviceInformation : DeviceInformation, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32DeviceInformation(DeviceFamily deviceFamily, ushort deviceNumber, MappedMemory flashDevice, MappedMemory sramDevice, byte productRevision = 0)
            : base(deviceFamily, deviceNumber, flashDevice, sramDevice, productRevision)
        {
        }

        public EFR32DeviceInformation(int deviceFamily, ushort deviceNumber, MappedMemory flashDevice, MappedMemory sramDevice, byte productRevision = 0)
            : this((DeviceFamily)deviceFamily, deviceNumber, flashDevice, sramDevice, productRevision)
        {
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.EUI48L:
                return (uint)(EUI >> 32);
            case Registers.EUI48H:
                return (uint)(EUI & 0xFFFFFFFF);
            case Registers.UNIQUEL:
                return (uint)(Unique >> 32);
            case Registers.UNIQUEH:
                return (uint)(Unique & 0xFFFFFFFF);
            case Registers.MSIZE:
                return (uint)((sramSize << 16) | (flashSize & 0xFFFF));
            case Registers.PART:
                return (uint)((productRevision << 24) | ((byte)deviceFamily << 16) | deviceNumber);
            case Registers.DEVINFOREV:
                return 0x01;
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
                return 0x200;
            }
        }

        public ulong EUI { get; set; }

        // This enum contains only used registers.
        // The rest is platform-dependent.
        private enum Registers
        {
            EUI48L      = 0x024, // EUI48 OUI and Unique identifier
            EUI48H      = 0x028, // OUI
            UNIQUEL     = 0x03c, // Low 32 bits of device unique number
            UNIQUEH     = 0x040, // High 32 bits of device unique number
            MSIZE       = 0x044, // Flash and SRAM Memory size in kB
            PART        = 0x048, // Part description
            DEVINFOREV  = 0x04c, // Device information page revision
        }
    }
}
