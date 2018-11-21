//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.USB
{
    public struct SetupPacket
    {
        [PacketField, Width(5)]
        public PacketRecipient Recipient;
        [PacketField, Offset(bytes: 0, bits: 5), Width(2)]
        public PacketType Type;
        [PacketField, Offset(bytes: 0, bits: 7), Width(1)]
        public Direction Direction;
        [PacketField]
        public byte Request;
        [PacketField, LeastSignificantByteFirst]
        public short Value;
        [PacketField, LeastSignificantByteFirst]
        public short Index;
        [PacketField, LeastSignificantByteFirst]
        public ushort Count;
    }
}
