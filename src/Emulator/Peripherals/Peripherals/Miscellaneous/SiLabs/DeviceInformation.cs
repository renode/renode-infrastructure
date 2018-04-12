//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class DeviceInformation
    {
        public DeviceInformation(DeviceFamily deviceFamily, ushort deviceNumber, MappedMemory flashDevice, MappedMemory sramDevice, byte productRevision = 0)
        {
            flashSize = checked((ushort)(flashDevice.Size / 1024));
            sramSize = checked((ushort)(sramDevice.Size / 1024));
            this.productRevision = productRevision;
            this.deviceFamily = deviceFamily;
            this.deviceNumber = deviceNumber;
        }

        public ulong Unique { get; set; }

        protected readonly ushort flashSize;
        protected readonly ushort sramSize;
        protected readonly byte productRevision;
        protected readonly DeviceFamily deviceFamily;
        protected readonly ushort deviceNumber;
    }
}
