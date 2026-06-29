//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USB
{
    [LeastSignificantByteFirst]
    public struct ConfigurationDescriptor : IDescriptor
    {
        [PacketField]
        public byte Length;
        [PacketField]
        public byte Type;
        [PacketField]
        public ushort TotalLength;
        [PacketField]
        public byte NumberOfInterfaces;
        [PacketField]
        public byte ConfigurationValue;
        [PacketField]
        public byte Configuration;
        [PacketField]
        public byte Attributes;
        [PacketField]
        public byte MaximumPower;

        byte IDescriptor.Type => Type;

        public byte[] AsBytes => Packet.Encode(this);

        public override string ToString()
        {
            return $"Length = {Length}, Type = {Type}, TotalLength = {TotalLength}, NumberOfInterfaces = {NumberOfInterfaces}, ConfigurationValue = {ConfigurationValue}, Configuration = {Configuration}, Attributes = {Attributes}, MaximumPower = {MaximumPower}";
        }
    }
}