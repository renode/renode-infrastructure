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

        // Signals that the setup transaction being handled was answered with a STALL handshake
        // (USB 2.0 SS 9.2.7) rather than a (possibly empty) successful status stage. The pipe
        // exposed to the host translates this into `IUSBPipeRead.Stalled`; controller peripherals
        // that build their own pipes on top of this core can subscribe here to drive whatever
        // wire-level stall mechanism they have.
        public event Action<SetupPacket> RequestStalled;

        // Raises `RequestStalled` on behalf of a controller peripheral. Firmware behind such a
        // peripheral answers with a STALL long after `customSetupPacketHandler` returned (it has
        // to write a STALL bit in its own registers first), so the stall cannot be reported as
        // part of handling the setup packet the way the built-in request handling does.
        public void StallRequest(SetupPacket packet)
        {
            RequestStalled?.Invoke(packet);
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
                    // USB 2.0 SS 9.2.7: a request to a non-existing recipient must be stalled
                    device.Log(LogLevel.Warning, "Trying to access a non-existing interface #{0}", packet.Index);
                    RequestStalled?.Invoke(packet);
                    responseCallback(new byte[] { });
                    return;
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
                    return HandleGetDescriptor(packet);
                case StandardRequest.SetConfiguration:
                    SelectedConfiguration = Configurations.SingleOrDefault(x => x.Identifier == packet.Value);
                    if(SelectedConfiguration == null)
                    {
                        device.Log(LogLevel.Warning, "Tried to select a non-existing configuration #{0}", packet.Value);
                    }
                    break;
                default:
                    // USB 2.0 SS 9.2.7: an unsupported request must be stalled, not completed as
                    // a zero-length success
                    device.Log(LogLevel.Warning, "Unsupported standard request: 0x{0:X}", packet.Request);
                    RequestStalled?.Invoke(packet);
                    break;
                }
            }

            return BitStream.Empty;
        }

        private BitStream HandleGetDescriptor(SetupPacket packet)
        {
            var value = packet.Value;
            var descriptorType = (DescriptorType)(value >> 8);
            var descriptorIndex = (byte)value;

            switch(descriptorType)
            {
            case DescriptorType.Device:
                return GetDescriptor(false);
            case DescriptorType.Configuration:
                if(descriptorIndex >= Configurations.Count)
                {
                    // USB 2.0 SS 9.4.3: a request for a descriptor that does not exist must be
                    // stalled. The bound was also off by one, so index == Count used to fall
                    // through to `ElementAt` and throw
                    device.Log(LogLevel.Warning, "Tried to access a non-existing configuration #{0}", descriptorIndex);
                    RequestStalled?.Invoke(packet);
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
                core.RequestStalled += _ =>
                {
                    stalled = true;
                    if(!handlingSetup)
                    {
                        // Asynchronous stall from a controller peripheral (see `StallRequest`):
                        // the `ReportStall` call that goes with this transfer has already run,
                        // so report it directly instead
                        ReportStall();
                    }
                };
            }

            public bool TryRead(out byte[] data) => readBuffer.TryDequeue(out data);

            public void SetupPacketWrite(SetupPacket packet)
            {
                if(pendingPacket != null)
                {
                    core.device.WarningLog("Received setup packet twice in a row");
                }
                stalled = false;
                if(packet.Direction == Direction.DeviceToHost)
                {
                    HandleSetupPacket(packet, null, res =>
                    {
                        if(stalled)
                        {
                            return;
                        }
                        Enqueue(res);
                    });
                    ReportStall();
                }
                else if(packet.Count == 0)
                {
                    HandleSetupPacket(packet, null);
                    if(!ReportStall())
                    {
                        // A request with no data stage is acknowledged by a zero-length IN packet;
                        // without it the host side of `SetupWrite` waits forever for a status stage
                        // that never arrives
                        Enqueue(new byte[] { });
                    }
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
                var packet = pendingPacket.Value;
                pendingPacket = null;
                stalled = false;
                HandleSetupPacket(packet, data);
                if(!ReportStall())
                {
                    // status stage of a host-to-device transfer - see above
                    Enqueue(new byte[] { });
                }
            }

            public void SetupWrite(SetupPacket packet, byte[] data)
            {
                HandleSetupPacket(packet, data);
            }

            // As with `SetupRead` below: this core answers inline, so a STALL is raised while
            // `HandleEp0SetupPacket` is still running - before the interface default would have
            // armed its `ReadPacket`, which would then wait forever for a status stage that is
            // never coming. Report the outcome to the caller directly instead.
            public void SetupWrite(SetupPacket packet, byte[] data, Action<bool> onDone)
            {
                stalled = false;
                HandleSetupPacket(packet, data);
                onDone(!ReportStall());
            }

            public void SetupRead(SetupPacket packet, Action<byte[]> onRead)
            {
                stalled = false;
                pendingRead = onRead;
                HandleSetupPacket(packet, null, res =>
                {
                    if(pendingRead == null)
                    {
                        return;
                    }
                    pendingRead = null;
                    onRead(res);
                });
                ReportStall();
            }

            public event Action NewPacket;

            public event Action Stalled;

            private void Enqueue(byte[] data)
            {
                readBuffer.Enqueue(data);
                NewPacket?.Invoke();
            }

            private void HandleSetupPacket(SetupPacket packet, byte[] data, Action<byte[]> responseCallback = null)
            {
                handlingSetup = true;
                try
                {
                    core.HandleEp0SetupPacket(packet, data, responseCallback);
                }
                finally
                {
                    handlingSetup = false;
                }
            }

            // Delivers a stall raised during the transfer just handled to whoever is waiting for
            // it, and reports whether there was one.
            private bool ReportStall()
            {
                if(!stalled)
                {
                    return false;
                }
                stalled = false;
                var onRead = pendingRead;
                pendingRead = null;
                Stalled?.Invoke();
                onRead?.Invoke(null);
                return true;
            }

            private SetupPacket? pendingPacket;
            private bool stalled;
            private bool handlingSetup;
            private Action<byte[]> pendingRead;

            private readonly ConcurrentQueue<byte[]> readBuffer = new();

            private readonly USBDeviceCore core;
        }
    }
}