//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USB
{
    [LeastSignificantByteFirst]
    public struct DeviceDescriptor
    {
        [PacketField]
        public byte Length;
        [PacketField]
        public byte Type;
        [PacketField]
        public ushort ProtocolVersion;
        [PacketField]
        public byte Class;
        [PacketField]
        public byte Subclass;
        [PacketField]
        public byte Protocol;
        [PacketField]
        public byte MaximumPacketSize;
        [PacketField]
        public ushort VendorId;
        [PacketField]
        public ushort ProductId;
        [PacketField]
        public ushort DeviceReleaseNumber;
        [PacketField]
        public byte ManufacturerNameIndex;
        [PacketField]
        public byte ProductNameIndex;
        [PacketField]
        public byte SerialNumberIndex;
        [PacketField]
        public byte NumberOfConfigurations;

        public override string ToString()
        {
            return $" Length = {Length}, Type = {Type}, ProtocolVersion = {ProtocolVersion}, Class = {Class}, Subclass = {Subclass}, Protocol = {Protocol}, MaximumPacketSize = {MaximumPacketSize}, VendorId = {VendorId}, ProductId = {ProductId}, DeviceReleaseNumber = {DeviceReleaseNumber}, ManufacturerNameIndex = {ManufacturerNameIndex}, ProductNameIndex = {ProductNameIndex}, SerialNumberIndex = {SerialNumberIndex}, NumberOfConfigurations = {NumberOfConfigurations}";
        }
    }
}
