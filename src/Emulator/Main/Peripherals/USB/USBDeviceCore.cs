//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    // NOTE: Do not use this class for USB controller peripherals a CPU will talk to. Implement `IUSBConnection` yourself. This class is only for USB devices that exist entirely within C# - `USBPendrive` and the like
    public class USBDeviceCore : DescriptorProvider, IUSBConnection, IDisposable
    {
        public USBDeviceCore(IUSBDevice device,
                             USBClassCode classCode = USBClassCode.NotSpecified,
                             byte subClassCode = 0,
                             byte protocol = 0,
                             USBProtocol usbProtocolVersion = USBProtocol.USB_2_0,
                             short deviceReleaseNumber = 0,
                             PacketSize maximalPacketSize = PacketSize.Size64,
                             string manufacturerName = null,
                             string productName = null,
                             string serialNumber = null,
                             ushort vendorId = 0,
                             ushort productId = 0,
                             Action<SetupPacket, byte[], Action<byte[]>> customSetupPacketHandler = null) : base(18, (byte)DescriptorType.Device)
        {
            if(maximalPacketSize != PacketSize.Size8
                && maximalPacketSize != PacketSize.Size16
                && maximalPacketSize != PacketSize.Size32
                && maximalPacketSize != PacketSize.Size64)
            {
                throw new ConstructionException("Unsupported maximal packet size.");
            }

            this.customSetupPacketHandler = customSetupPacketHandler;
            this.device = device;
            ep0 = new USBPipeSetupEp0(this);
            configurations = new List<USBConfiguration>();

            CompatibleProtocolVersion = usbProtocolVersion;
            Class = classCode;
            SubClass = subClassCode;
            Protocol = protocol;
            DeviceReleaseNumber = deviceReleaseNumber;
            MaximalPacketSize = maximalPacketSize;
            ManufacturerName = manufacturerName;
            ProductName = productName;
            SerialNumber = serialNumber;
            VendorId = vendorId;
            ProductId = productId;

            RegisterSubdescriptors(configurations);
        }

        public USBDeviceCore WithConfiguration(string description = null, bool selfPowered = false, bool remoteWakeup = false, short maximalPower = 0, Action<USBConfiguration> configure = null)
        {
            var newConfiguration = new USBConfiguration(device, (byte)(configurations.Count + 1), description, selfPowered, remoteWakeup, maximalPower);
            configurations.Add(newConfiguration);
            configure?.Invoke(newConfiguration);
            return this;
        }

        public void Reset()
        {
            Address = 0;

            SelectedConfiguration = null;
        }

        public USBEndpoint GetEndpoint(int endpointNumber, Direction direction)
        {
            if(SelectedConfiguration == null)
            {
                return null;
            }

            foreach(var iface in SelectedConfiguration.Interfaces)
            {
                var ep = iface.Endpoints.FirstOrDefault(x => x.Identifier == endpointNumber && x.Direction == direction);
                if(ep != null)
                {
                    return ep;
                }
            }

            return null;
        }

        public IUSBPipeRead ConnectEndpointRead(byte endpoint)
        {
            if(endpoint == 0)
            {
                return ep0;
            }
            return GetEndpoint(endpoint, Direction.DeviceToHost);
        }

        public IUSBPipeWrite ConnectEndpointWrite(byte endpoint)
        {
            if(endpoint == 0)
            {
                return ep0;
            }
            return GetEndpoint(endpoint, Direction.HostToDevice);
        }

        public IUSBPipeSetup ConnectEndpointSetup(byte endpoint)
        {
            if(endpoint != 0)
            {
                device.WarningLog("Non-endpoint 0 control endpoint are unsupported");
                return null;
            }
            return ep0;
        }

        public void Dispose()
        {
        }

        public IReadOnlyCollection<USBConfiguration> Configurations => configurations;

        public USBConfiguration SelectedConfiguration { get; set; }

        public byte Address { get; set; }

        public USBProtocol CompatibleProtocolVersion { get; }

        public USBClassCode Class { get; }

        public byte SubClass { get; }

        public byte Protocol { get; }

        public PacketSize MaximalPacketSize { get; }

        public ushort VendorId { get; }

        public ushort ProductId { get; }

        public short DeviceReleaseNumber { get; }

        public string ManufacturerName { get; }

        public string ProductName { get; }

        public string SerialNumber { get; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((short)CompatibleProtocolVersion)
                .Append((byte)Class)
                .Append(SubClass)
                .Append(Protocol)
                .Append((byte)MaximalPacketSize)
                .Append(VendorId)
                .Append(ProductId)
                .Append(DeviceReleaseNumber)
                .Append(USBString.FromString(ManufacturerName).Index)
                .Append(USBString.FromString(ProductName).Index)
                .Append(USBString.FromString(SerialNumber).Index)
                .Append((byte)Configurations.Count);
        }

        private void HandleEp0SetupPacket(SetupPacket packet, byte[] data, Action<byte[]> responseCallback = null)
        {
            if(responseCallback == null)
            {
                responseCallback = _ => { };
            }
            var result = BitStream.Empty;

            device.Log(LogLevel.Debug, "Handling setup packet: {0}", packet);

            if(customSetupPacketHandler != null)
            {
                customSetupPacketHandler(packet, data, responseCallback);
                return;
            }

            switch(packet.Recipient)
            {
            case PacketRecipient.Device:
                result = HandleRequest(packet);
                break;
            case PacketRecipient.Interface:
                if(SelectedConfiguration == null)
                {
                    device.Log(LogLevel.Warning, "Trying to access interface before selecting a configuration");
                    responseCallback(new byte[] { });
                }
                var iface = SelectedConfiguration.Interfaces.FirstOrDefault(x => x.Identifier == packet.Index);
                if(iface == null)
                {
                    device.Log(LogLevel.Warning, "Trying to access a non-existing interface #{0}", packet.Index);
                }
                result = iface.HandleRequest(packet);
                break;
            default:
                device.Log(LogLevel.Warning, "Unsupported recipient type: 0x{0:X}", packet.Recipient);
                break;
            }

            var resultBytes = result.AsByteArray(packet.Count * 8u);

            device.Log(LogLevel.Noisy, "Sending setup packet response of length {0}", resultBytes.Length);
#if DEBUG_PACKETS
            device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(resultBytes));
#endif
            responseCallback(resultBytes);
        }

        private void SendSetupResult(Action<byte[]> resultCallback, byte[] result)
        {
            resultCallback(result);
        }

        private BitStream HandleRequest(SetupPacket packet)
        {
            if(packet.Type != PacketType.Standard)
            {
                device.Log(LogLevel.Warning, "Non standard requests are not supported");
            }
            else
            {
                switch((StandardRequest)packet.Request)
                {
                case StandardRequest.SetAddress:
                    Address = checked((byte)packet.Value);
                    break;
                case StandardRequest.GetDescriptor:
                    if(packet.Direction != Direction.DeviceToHost)
                    {
                        device.Log(LogLevel.Warning, "Wrong direction of Get Descriptor Standard Request");
                        break;
                    }
                    return HandleGetDescriptor(packet.Value);
                case StandardRequest.SetConfiguration:
                    SelectedConfiguration = Configurations.SingleOrDefault(x => x.Identifier == packet.Value);
                    if(SelectedConfiguration == null)
                    {
                        device.Log(LogLevel.Warning, "Tried to select a non-existing configuration #{0}", packet.Value);
                    }
                    break;
                default:
                    device.Log(LogLevel.Warning, "Unsupported standard request: 0x{0:X}", packet.Request);
                    break;
                }
            }

            return BitStream.Empty;
        }

        private BitStream HandleGetDescriptor(ushort value)
        {
            var descriptorType = (DescriptorType)(value >> 8);
            var descriptorIndex = (byte)value;

            switch(descriptorType)
            {
            case DescriptorType.Device:
                return GetDescriptor(false);
            case DescriptorType.Configuration:
                if(Configurations.Count < descriptorIndex)
                {
                    device.Log(LogLevel.Warning, "Tried to access a non-existing configuration #{0}", descriptorIndex);
                    return BitStream.Empty;
                }
                return Configurations.ElementAt(descriptorIndex).GetDescriptor(true);
            case DescriptorType.String:
            {
                if(descriptorIndex == 0)
                {
                    // special String Index returning a list of supported languages
                    return USBString.GetSupportedLanguagesDescriptor();
                }
                else
                {
                    var usbString = USBString.FromId(descriptorIndex);
                    if(usbString == null)
                    {
                        device.Log(LogLevel.Warning, "Tried to get non-existing string #{0}", descriptorIndex);
                        return BitStream.Empty;
                    }

                    return usbString.GetDescriptor(false);
                }
            }
            default:
                device.Log(LogLevel.Warning, "Unsupported descriptor type: 0x{0:X}", descriptorType);
                return BitStream.Empty;
            }
        }

        private readonly Action<SetupPacket, byte[], Action<byte[]>> customSetupPacketHandler;

        private readonly List<USBConfiguration> configurations;
        private readonly IUSBDevice device;

        private readonly USBPipeSetupEp0 ep0;

        private class USBPipeSetupEp0 : IUSBPipeSetup
        {
            public USBPipeSetupEp0(USBDeviceCore core)
            {
                this.core = core;
            }

            public bool TryRead(out byte[] data) => readBuffer.TryDequeue(out data);

            public void SetupPacketWrite(SetupPacket packet)
            {
                if(pendingPacket != null)
                {
                    core.device.WarningLog("Received setup packet twice in a row");
                }
                if(packet.Direction == Direction.DeviceToHost)
                {
                    core.HandleEp0SetupPacket(packet, null, res =>
                    {
                        readBuffer.Enqueue(res);
                        NewPacket?.Invoke();
                    });
                }
                else if(packet.Count == 0)
                {
                    core.HandleEp0SetupPacket(packet, null);
                }
                else
                {
                    pendingPacket = packet;
                }
            }

            public void Write(byte[] data)
            {
                if(pendingPacket == null)
                {
                    core.device.WarningLog("Recieved a write without a write setup packet");
                    return;
                }
                core.HandleEp0SetupPacket(pendingPacket.Value, data);
                pendingPacket = null;
            }

            public void SetupWrite(SetupPacket packet, byte[] data)
            {
                core.HandleEp0SetupPacket(packet, data);
            }

            public void SetupRead(SetupPacket packet, Action<byte[]> onRead)
            {
                core.HandleEp0SetupPacket(packet, null, onRead);
            }

            public event Action NewPacket;

            private SetupPacket? pendingPacket;

            private readonly ConcurrentQueue<byte[]> readBuffer = new();

            private readonly USBDeviceCore core;
        }
    }
}