//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public struct Header
    {
        [PacketField]
        public ushort Version;
        [PacketField]
        public Command Command;
        [PacketField]
        public uint Status;

        public override string ToString()
        {
            return $"Version = 0x{Version:X}, Command = {Command} (0x{Command:X}), Status = 0x{Status:X}";
        }
    }

    public enum Command: ushort
    {
        ListDevices = 0x8005,
        ListDevicesReply = 0x5,
        AttachDevice = 0x8003,
        AttachDeviceReply = 0x3,
    }
}
