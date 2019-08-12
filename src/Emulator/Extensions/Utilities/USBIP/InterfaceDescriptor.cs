//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public struct InterfaceDescriptor
    {
        [PacketField]
        public byte InterfaceClass;
        [PacketField]
        public byte InterfaceSubClass;
        [PacketField]
        public byte InterfaceProtocol;
        [PacketField]
        public byte Padding;

        public override string ToString()
        {
            return $" InterfaceClass = {InterfaceClass}, InterfaceSubClass = {InterfaceSubClass}, InterfaceProtocol = {InterfaceProtocol}, Padding = {Padding}";
        }
    }
}
