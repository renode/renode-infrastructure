//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public struct DeviceListCount
    {
        [PacketField]
        public uint NumberOfExportedDevices;

        public override string ToString()
        {
            return $"NumberOfExportedDevices = {NumberOfExportedDevices}";
        }
    }
}
