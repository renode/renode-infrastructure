//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public struct DeviceDescriptor
    {
        [PacketField, Width(256)]
        public byte[] Path;
        [PacketField, Width(32)]
        public byte[] BusId;
        [PacketField]
        public uint BusNumber;
        [PacketField]
        public uint DeviceNumber;
        [PacketField]
        public uint Speed;
        [PacketField]
        public ushort IdVendor;
        [PacketField]
        public ushort IdProduct;
        [PacketField]
        public ushort BcdDevice;
        [PacketField]
        public byte DeviceClass;
        [PacketField]
        public byte DeviceSubClass;
        [PacketField]
        public byte DeviceProtocol;
        [PacketField]
        public byte ConfigurationValue;
        [PacketField]
        public byte NumberOfConfigurations;
        [PacketField]
        public byte NumberOfInterfaces;

        public override string ToString()
        {
            return $"BusNumber = {BusNumber}, DeviceNumber = {DeviceNumber}, Speed = {Speed}, IdVendor = {IdVendor}, IdProduct = {IdProduct}, BcdDevice = {BcdDevice}, DeviceClass = {DeviceClass}, DeviceSubClass = {DeviceSubClass}, DeviceProtocol = {DeviceProtocol}, ConfigurationValue = {ConfigurationValue}, NumberOfConfigurations = {NumberOfConfigurations}, NumberOfInterfaces = {NumberOfInterfaces}";
        }
    }
}
