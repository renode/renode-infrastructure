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
    public struct InterfaceDescriptor : IDescriptor
    {
        [PacketField]
        public byte Length;
        [PacketField]
        public byte Type;
        [PacketField]
        public byte Number;
        [PacketField]
        public byte AlternateSetting;
        [PacketField]
        public byte NumberOfEndpoints;
        [PacketField]
        public byte Class;
        [PacketField]
        public byte Subclass;
        [PacketField]
        public byte Protocol;
        [PacketField]
        public byte DescriptionStringIndex;

        byte IDescriptor.Type => Type;

        public byte[] AsBytes => Packet.Encode(this);

        public override string ToString()
        {
            return $"[InterfaceDescriptor Length = {Length}, Type = {Type}, Number = {Number}, AlternateSetting = {AlternateSetting}, NumberOfEndpoints = {NumberOfEndpoints}, Class = {Class}, Subclass = {Subclass}, Protocol = {Protocol}, DescriptionStringIndex = {DescriptionStringIndex}]";
        }
    }
}