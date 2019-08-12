//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    // the actual packet
    // is prepended with
    // the USBIP.Header
    public struct AttachDeviceCommandDescriptor
    {
        [PacketField, Width(32)]
        public byte[] BusId;
    }
}
