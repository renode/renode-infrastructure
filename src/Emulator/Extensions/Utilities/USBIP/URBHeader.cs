//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public struct URBHeader
    {
        [PacketField]
        public URBCommand Command;
        [PacketField]
        public uint SequenceNumber;
        [PacketField]
        public ushort BusId; // in the documentation those two fields are called 'DeviceId'
        [PacketField]
        public ushort DeviceId;
        [PacketField]
        public URBDirection Direction;
        [PacketField]
        public uint EndpointNumber;
        [PacketField]
        public uint FlagsOrStatus;

        public override string ToString()
        {
            return $"Command = {Command}, SequenceNumber = 0x{SequenceNumber:X}, BusId = 0x{BusId:X}, DeviceId = 0x{DeviceId:X}, Direction = {Direction}, EndpointNumber = 0x{EndpointNumber:X}, FlagsOrStatus = 0x{FlagsOrStatus:X}";
        }
    }

    public enum URBDirection : uint
    {
        Out = 0x0,
        In = 0x1
    }

    public enum URBCommand: uint
    {
        URBRequest = 0x1,
        Unlink = 0x2,
        URBReply = 0x3,
        UnlinkReply = 0x4,
    }
}
