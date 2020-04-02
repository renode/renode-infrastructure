//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.USB
{
    public class USBDeviceCore : DescriptorProvider
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
                             ushort productId = 0) : base(18, (byte)DescriptorType.Device)
        {
            if(maximalPacketSize != PacketSize.Size8
                && maximalPacketSize != PacketSize.Size16
                && maximalPacketSize != PacketSize.Size32
                && maximalPacketSize != PacketSize.Size64)
            {
                throw new ConstructionException("Unsupported maximal packet size.");
            }

            Device = device;
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

            controlEndpoints = new Dictionary<int, USBEndpoint>();
            hostToDeviceEndpoints = new Dictionary<int, USBEndpoint>();
            deviceToHostEndpoints = new Dictionary<int, USBEndpoint>();

            controlEndpoints[0] = new USBEndpoint(this,
                    identifier: 0,
                    direction: Direction.DeviceToHost, // direction information is ignored for control endpoints
                    transferType: EndpointTransferType.Control,
                    maximumPacketSize: 64, //?!
                    interval: 0); //?!

            controlEndpoints[0].SetupPacketHandler = HandleSetupPacketAutomatic;
        }

        public void Reset()
        {
            Address = 0;

            if(SelectedConfiguration == null)
            {
                return;
            }

            foreach(var iface in SelectedConfiguration.Interfaces)
            {
                foreach(var epoint in iface.Endpoints)
                {
                    epoint.Reset();
                }
            }

            SelectedConfiguration = null;
        }

        public USBEndpoint GetEndpoint(Direction direction, int endpointNumber)
        {
            if(controlEndpoints.TryGetValue(endpointNumber, out var ctrl))
            {
                return ctrl;
            }

            if(direction == Direction.DeviceToHost)
            {
                return deviceToHostEndpoints.TryGetValue(endpointNumber, out var dth)
                    ? dth
                    : null;
            }

            return hostToDeviceEndpoints.TryGetValue(endpointNumber, out var htd)
                ? htd
                : null;
        }

        public USBEndpoint ControlEndpoint => controlEndpoints[0];

        private void HandleSetupPacketAutomatic(USBEndpoint endpoint, USBTransactionStage setupStage, Action<USBTransactionStage> deviceStage)
        {
            BitStream result = null;
            SetupPacket setupPacket;

            if(Packet.TryDecode<SetupPacket>(setupStage.Payload, out setupPacket))
            {
                Device.Log(LogLevel.Noisy, "Handling setup packet: {0}", setupPacket);

                switch(setupPacket.Recipient)
                {
                    case PacketRecipient.Device:
                        result = HandleRequest(endpoint, setupPacket);
                        break;
                    case PacketRecipient.Interface:
                        if(SelectedConfiguration == null)
                        {
                            Device.Log(LogLevel.Warning, "Trying to access interface before selecting a configuration");
                            break;
                        }
                        var iface = SelectedConfiguration.Interfaces.FirstOrDefault(x => x.Identifier == setupPacket.Index);
                        if(iface == null)
                        {
                            Device.Log(LogLevel.Warning, "Trying to access a non-existing interface #{0}", setupPacket.Index);
                        }
                        result = iface.HandleRequest(setupPacket);
                        break;
                    default:
                        Device.Log(LogLevel.Warning, "Unsupported recipient type: 0x{0:X}", setupPacket.Recipient);
                        // TODO: this is a hack - shouldn't we stall here?
                        result = BitStream.Empty;
                        break;
                }
            }
            else
            {
                Device.Log(LogLevel.Warning, "Received a broken SETUP packet with {0} bytes of data", setupStage.Payload.Length);
#if DEBUG_PACKETS
                Device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(setupStage.Payload));
#endif
            }          

            if(result == null)
            {
                deviceStage(USBTransactionStage.Stall());
            }
            else
            {
                // put the data to the endpoint's fifo
                endpoint.WriteDataToHost(result.AsByteArray(setupPacket.Count * 8u));
                deviceStage(USBTransactionStage.Ack());
            }
        }

        private BitStream HandleRequest(USBEndpoint endpoint, SetupPacket packet)
        {
            if(packet.Type != PacketType.Standard)
            {
                Device.Log(LogLevel.Warning, "Non standard requests are not supported");
                return null;
            }
            
            switch((StandardRequest)packet.Request)
            {
                case StandardRequest.SetAddress:
                    endpoint.AfterReadOneShot += delegate { Address = checked((byte)packet.Value); };
                    return BitStream.Empty;

                case StandardRequest.GetDescriptor:
                    if(packet.Direction != Direction.DeviceToHost)
                    {
                        Device.Log(LogLevel.Warning, "Wrong direction of Get Descriptor Standard Request");
                    }
                    return HandleGetDescriptor(packet.Value);

                case StandardRequest.SetConfiguration:
                    SelectedConfiguration = Configurations.SingleOrDefault(x => x.Identifier == packet.Value);
                    if(SelectedConfiguration == null)
                    {
                        Device.Log(LogLevel.Warning, "Tried to select a non-existing configuration #{0}", packet.Value);
                        return null;
                    }
                    return BitStream.Empty;

                default:
                    Device.Log(LogLevel.Warning, "Unsupported standard request: 0x{0:X}", packet.Request);
                    return null;
            }
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
                        Device.Log(LogLevel.Warning, "Tried to access a non-existing configuration #{0}", descriptorIndex);
                        return null;
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
                            Device.Log(LogLevel.Warning, "Tried to get non-existing string #{0}", descriptorIndex);
                            return null;
                        }

                        return usbString.GetDescriptor(false);
                    }
                }
                default:
                    Device.Log(LogLevel.Warning, "Unsupported descriptor type: 0x{0:X}", descriptorType);
                    return null;
            }
        }

        public USBDeviceCore WithConfiguration(string description = null, bool selfPowered = false, bool remoteWakeup = false, short maximalPower = 0, Action<USBDeviceCore, USBConfiguration> configure = null)
        {
            var newConfiguration = new USBConfiguration(this, (byte)(configurations.Count + 1), description, selfPowered, remoteWakeup, maximalPower);
            configurations.Add(newConfiguration);
            configure?.Invoke(this, newConfiguration);
            return this;
        }

        public USBDeviceCore AddEndpoint(USBEndpoint endpoint)
        {
            if(controlEndpoints.ContainsKey(endpoint.Identifier))
            {
                throw new ConstructionException($"Control endpoint #{endpoint.Identifier} already defined");
            }

            if(endpoint.TransferType == EndpointTransferType.Control)
            {
                if(deviceToHostEndpoints.ContainsKey(endpoint.Identifier) || hostToDeviceEndpoints.ContainsKey(endpoint.Identifier))
                {
                    throw new ConstructionException($"Endpoint #{endpoint.Identifier} already defined");
                }

                controlEndpoints[endpoint.Identifier] = endpoint;
            }
            else if(endpoint.Direction == Direction.DeviceToHost)
            {
                if(deviceToHostEndpoints.ContainsKey(endpoint.Identifier))
                {
                    throw new ConstructionException($"IN endpoint #{endpoint.Identifier} already defined");
                }

                deviceToHostEndpoints[endpoint.Identifier] = endpoint;
            }
            else // Direction.HostToDevice
            {
                if(hostToDeviceEndpoints.ContainsKey(endpoint.Identifier))
                {
                    throw new ConstructionException($"OUT endpoint #{endpoint.Identifier} already defined");
                }

                hostToDeviceEndpoints[endpoint.Identifier] = endpoint;
            }

            return this;
        }

        public IReadOnlyCollection<USBConfiguration> Configurations => configurations;

        public USBConfiguration SelectedConfiguration { get; private set; }
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

        public IUSBDevice Device { get; }

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

        // control endpoints are special because they are both in and out at the same time
        private readonly Dictionary<int, USBEndpoint> controlEndpoints;
        private readonly Dictionary<int, USBEndpoint> deviceToHostEndpoints;
        private readonly Dictionary<int, USBEndpoint> hostToDeviceEndpoints;

        private readonly List<USBConfiguration> configurations;
    }
}
