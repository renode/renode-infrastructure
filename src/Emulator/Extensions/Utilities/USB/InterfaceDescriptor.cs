//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USB
{
    public struct InterfaceDescriptor
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

        public override string ToString()
        {
            return $" Length = {Length}, Type = {Type}, Number = {Number}, AlternateSetting = {AlternateSetting}, NumberOfEndpoints = {NumberOfEndpoints}, Class = {Class}, Subclass = {Subclass}, Protocol = {Protocol}, DescriptionStringIndex = {DescriptionStringIndex}";
        }
    }
}
