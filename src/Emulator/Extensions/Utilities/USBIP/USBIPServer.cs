//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// uncomment the line below to get more detailed logs
// note: dumping packets may severely lower performance
// #define DEBUG_PACKETS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    public static class USBIPServerExtensions
    {
        public static void CreateUSBIPServer(this Emulation emulation, int port = 3240, string address = "127.0.0.1", string name = "usb")
        {
            var server = new USBIPServer(address, port);
            server.Run();

            emulation.HostMachine.AddHostMachineElement(server, name);
        }

        // this is just a simple wrapper method allowing to register devices from monitor
        public static void Register(this USBIPServer @this, IUSBDevice device, int? port = null)
        {
            if(!port.HasValue)
            {
                port = @this.Children.Any()
                    ? @this.Children.Max(x => x.RegistrationPoint.Address) + 1
                    : 0;
            }

            @this.Register(device, new NumberRegistrationPoint<int>(port.Value));
        }
    }

    public class USBIPServer : SimpleContainerBase<IUSBDevice>, IHostMachineElement, IDisposable
    {
        public USBIPServer(string address, int port)
        {
            this.port = port;

            server = new SocketServerProvider(false, serverName: "USBIP");
            server.DataReceived += HandleIncomingData;
            server.ConnectionClosed += Reset;

            // setting initial size is just an optimization
            buffer = new List<byte>(Packet.CalculateLength<URBRequest>());
            cancellationToken = new CancellationTokenSource();
        }

        public override void Dispose()
        {
            base.Dispose();
            Shutdown();
        }

        public void Run()
        {
            server.Start(port);
        }

        public void Reset()
        {
            state = State.WaitForCommand;
            urbHeader = default(URBHeader);
            additionalDataCount = 0;

            buffer.Clear();
            cancellationToken = new CancellationTokenSource();
        }

        private void SendResponse(IEnumerable<byte> bytes)
        {
            server.Send(bytes);

#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Count {0}: {1}", bytes.Count(), Misc.PrettyPrintCollectionHex(bytes));
#endif
        }

        private void Shutdown()
        {
            this.Log(LogLevel.Info, "Shutting down the server...");

            cancellationToken.Cancel();
            server.Stop();
            buffer.Clear();
        }

        private void HandleIncomingData(int b)
        {
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Incoming byte: 0x{0:X}; state = {1}; buffer size = {2}", b, state, buffer.Count);
#endif

            if(b < 0)
            {
                // the current connection is closed - reset the state and prepare for the next one
                Reset();
                return;
            }

            buffer.Add((byte)b);

            switch(state)
            {
                case State.WaitForCommand:
                {
                    DebugHelper.Assert(buffer.Count <= Packet.CalculateLength<USBIP.Header>());
                    if(buffer.Count == Packet.CalculateLength<USBIP.Header>())
                    {
                        var header = Packet.Decode<USBIP.Header>(buffer);
                        buffer.Clear();
                        this.Log(LogLevel.Debug, "Received USB/IP header: {0}", header.ToString());

                        switch(header.Command)
                        {
                            case USBIP.Command.ListDevices:
                            {
                                SendResponse(HandleListDevicesCommand());

                                state = State.WaitForCommand;
                                break;
                            }

                            case USBIP.Command.AttachDevice:
                            {
                                state = State.WaitForBusId;
                                break;
                            }

                            default:
                            {
                                this.Log(LogLevel.Error, "Unexpected packet command: 0x{0:X}", header.Command);
                                Shutdown();
                                break;
                            }
                        }
                    }
                    break;
                }

                case State.WaitForBusId:
                {
                    DebugHelper.Assert(buffer.Count <= Packet.CalculateLength<USBIP.AttachDeviceCommandDescriptor>());
                    if(buffer.Count == Packet.CalculateLength<USBIP.AttachDeviceCommandDescriptor>())
                    {
                        var busId = System.Text.Encoding.ASCII.GetString(buffer.ToArray());
                        buffer.Clear();

                        state = TryHandleDeviceAttachCommand(busId, out var response)
                            ? State.WaitForURBHeader
                            : State.WaitForCommand;

                        SendResponse(response);
                    }
                    break;
                }

                case State.WaitForURBHeader:
                {
                    DebugHelper.Assert(buffer.Count <= Packet.CalculateLength<URBHeader>());
                    if(buffer.Count == Packet.CalculateLength<URBHeader>())
                    {
                        urbHeader = Packet.Decode<URBHeader>(buffer);
                        this.Log(LogLevel.Debug, "Received URB header: {0}", urbHeader.ToString());
                        buffer.Clear();

                        switch(urbHeader.Command)
                        {
                            case URBCommand.URBRequest:
                                state = State.WaitForURBRequest;
                                break;
                            case URBCommand.Unlink:
                                state = State.HandleUnlinkCommand;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Unexpected URB command: 0x{0:X}", urbHeader.Command);
                                Shutdown();
                                break;
                        }
                    }
                    break;
                }

                case State.WaitForURBRequest:
                {
                    if(buffer.Count < Packet.CalculateLength<URBRequest>() + additionalDataCount)
                    {
                        break;
                    }

                    var packet = Packet.Decode<URBRequest>(buffer);
                    if(additionalDataCount == 0)
                    {
                        this.Log(LogLevel.Debug, "Received URB request: {0}", packet.ToString());

                        if(urbHeader.Direction == URBDirection.Out && packet.TransferBufferLength > 0)
                        {
                            additionalDataCount = (int)packet.TransferBufferLength;
                            this.Log(LogLevel.Debug, "Packet comes with {0} bytes of additional data. Waiting for it", additionalDataCount);
                            break;
                        }
                    }
                    else
                    {
                        if(urbHeader.Direction == URBDirection.Out && packet.TransferBufferLength > 0)
                        {
                            this.Log(LogLevel.Debug, "Collected {0} bytes of additional data - moving forward", additionalDataCount);
                        }
                    }

                    if(urbHeader.BusId != ExportedBusId)
                    {
                        this.Log(LogLevel.Warning, "URB command directed to a non-existing bus 0x{0:X}", urbHeader.BusId);
                    }
                    else if(!TryGetByAddress((int)urbHeader.DeviceId, out var device))
                    {
                        this.Log(LogLevel.Warning, "URB command directed to a non-existing device 0x{0:X}", urbHeader.DeviceId);
                    }
                    else if(urbHeader.EndpointNumber == 0)
                    {
                        // setup packet is passed in URB Request in a single `ulong` field
                        var setupPacket = Packet.Decode<SetupPacket>(buffer, Packet.CalculateOffset<URBRequest>(nameof(URBRequest.Setup)));
                        var additionalData = (additionalDataCount > 0)
                            ? buffer.Skip(buffer.Count - additionalDataCount).Take(additionalDataCount).ToArray()
                            : null;
                        var replyHeader = urbHeader;
                        device.USBCore.HandleSetupPacket(setupPacket, additionalData: additionalData, resultCallback: response =>
                        {
                            SendResponse(GenerateURBReply(replyHeader, packet, response));
                        });
                    }
                    else
                    {
                        var ep = device.USBCore.GetEndpoint((int)urbHeader.EndpointNumber, urbHeader.Direction == URBDirection.Out ? Direction.HostToDevice : Direction.DeviceToHost);
                        if(ep == null)
                        {
                            this.Log(LogLevel.Warning, "URB command directed to a non-existing endpoint 0x{0:X}", urbHeader.EndpointNumber);
                        }
                        else if(ep.Direction == Direction.DeviceToHost)
                        {
                            this.Log(LogLevel.Noisy, "Reading from endpoint #{0}", ep.Identifier);
                            var response = ep.Read(packet.TransferBufferLength, cancellationToken.Token);
#if DEBUG_PACKETS
                            this.Log(LogLevel.Noisy, "Count {0}: {1}", response.Length, Misc.PrettyPrintCollectionHex(response));
#endif
                            SendResponse(GenerateURBReply(urbHeader, packet, response));
                        }
                        else
                        {
                            var additionalData = buffer.Skip(buffer.Count - additionalDataCount).Take(additionalDataCount).ToArray();

                            ep.WriteData(additionalData);
                            SendResponse(GenerateURBReply(urbHeader, packet));
                        }
                    }

                    additionalDataCount = 0;
                    state = State.WaitForURBHeader;
                    buffer.Clear();

                    break;
                }

                case State.HandleUnlinkCommand:
                {
                    DebugHelper.Assert(buffer.Count <= Packet.CalculateLength<URBRequest>());
                    if(buffer.Count == Packet.CalculateLength<URBRequest>())
                    {
                        // this command is practically ignored
                        buffer.Clear();
                        state = State.WaitForURBHeader;
                    }
                    break;
                }

                default:
                    throw new ArgumentException(string.Format("Unexpected state: {0}", state));
            }
        }

        private IEnumerable<byte> GenerateURBReply(URBHeader hdr, URBRequest req, IEnumerable<byte> data = null)
        {
            var header = new URBHeader
            {
                Command = URBCommand.URBReply,
                SequenceNumber = hdr.SequenceNumber,
                BusId = hdr.BusId,
                DeviceId = hdr.DeviceId,
                Direction = hdr.Direction,
                EndpointNumber = hdr.EndpointNumber,
            };

            var reply = new URBReply
            {
                // this is intentional:
                // - report size of TransferBufferLength when returning no additional data,
                // - report additional data size (and not TransferBufferLenght) otherwise
                ActualLength = (data == null)
                    ? req.TransferBufferLength
                    : (uint)data.Count()
            };

            var result = Packet.Encode(header).Concat(Packet.Encode(reply)).AsEnumerable();
            if(data != null)
            {
                result = result.Concat(data);
            }

            return result;
        }

        // using this blocking helper method simplifies the logic of other methods
        // + it seems to be harmless, as this logic is not executed as a result of
        // intra-emulation communication (where it could lead to deadlocks)
        private byte[] HandleSetupPacketSync(IUSBDevice device, SetupPacket setupPacket)
        {
            byte[] result = null;

            var mre = new ManualResetEvent(false);
            device.USBCore.HandleSetupPacket(setupPacket, received =>
            {
                result = received;
                mre.Set();
            });

            mre.WaitOne();
            return result;
        }

        private USB.DeviceDescriptor ReadDeviceDescriptor(IUSBDevice device)
        {
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Direction.DeviceToHost,
                Request = (byte)StandardRequest.GetDescriptor,
                Value = ((int)DescriptorType.Device << 8),
                Count = (ushort)Packet.CalculateLength<USB.DeviceDescriptor>()
            };

            return Packet.Decode<USB.DeviceDescriptor>(HandleSetupPacketSync(device, setupPacket));
        }

        private USB.ConfigurationDescriptor ReadConfigurationDescriptor(IUSBDevice device, byte configurationId, out USB.InterfaceDescriptor[] interfaceDescriptors)
        {
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Direction.DeviceToHost,
                Request = (byte)StandardRequest.GetDescriptor,
                Value = (ushort)(((int)DescriptorType.Configuration << 8) | configurationId),
                Count = (ushort)Packet.CalculateLength<USB.ConfigurationDescriptor>()
            };
            // first ask for the configuration descriptor non-recursively ...
            var configurationDescriptorBytes = HandleSetupPacketSync(device, setupPacket);
            var result = Packet.Decode<USB.ConfigurationDescriptor>(configurationDescriptorBytes);

            interfaceDescriptors = new USB.InterfaceDescriptor[result.NumberOfInterfaces];
            // ... read the total length of a recursive structure ...
            setupPacket.Count = result.TotalLength;
            // ... and only then read the whole structure again.
            var recursiveBytes = HandleSetupPacketSync(device, setupPacket);

            var currentOffset = Packet.CalculateLength<USB.ConfigurationDescriptor>();
            for(var i = 0; i < interfaceDescriptors.Length; i++)
            {
                // the second byte of each descriptor contains the type
                while(recursiveBytes[currentOffset + 1] != (byte)DescriptorType.Interface)
                {
                    // the first byte of each descriptor contains the length in bytes
                    currentOffset += recursiveBytes[currentOffset];
                }

                interfaceDescriptors[i] = Packet.Decode<USB.InterfaceDescriptor>(recursiveBytes, currentOffset);
                // skip until the next interface descriptor
                currentOffset += Packet.CalculateLength<USB.InterfaceDescriptor>();
            }

            return result;
        }

        private IEnumerable<byte> GenerateDeviceDescriptor(IUSBDevice device, uint deviceNumber, bool includeInterfaces)
        {
            var deviceDescriptor = ReadDeviceDescriptor(device);
            var interfaceDescriptors = new List<USB.InterfaceDescriptor>();

            if(includeInterfaces)
            {
                for(var i = 0; i < deviceDescriptor.NumberOfConfigurations; i++)
                {
                    ReadConfigurationDescriptor(device, (byte)i, out var ifaces);
                    interfaceDescriptors.AddRange(ifaces);
                }
            }

            var devDescriptor = new USBIP.DeviceDescriptor
            {
                Path = new byte[256],
                BusId = new byte[32],

                BusNumber = ExportedBusId,
                DeviceNumber = deviceNumber,
                Speed = (int)USBSpeed.High, // this is hardcoded, but I don't know if that's good

                IdVendor = deviceDescriptor.VendorId,
                IdProduct = deviceDescriptor.ProductId,

                DeviceClass = deviceDescriptor.Class,
                DeviceSubClass = deviceDescriptor.Subclass,
                DeviceProtocol = deviceDescriptor.Protocol,

                NumberOfConfigurations = deviceDescriptor.NumberOfConfigurations,
                NumberOfInterfaces = (byte)interfaceDescriptors.Count
            };

            SetText(devDescriptor.BusId, "{0}-{1}", ExportedBusId, deviceNumber);
            SetText(devDescriptor.Path, "/renode/virtual/{0}-{1}", ExportedBusId, deviceNumber);

            var result = Packet.Encode(devDescriptor).AsEnumerable();

            foreach(var iface in interfaceDescriptors)
            {
                var intDescriptor = new USBIP.InterfaceDescriptor
                {
                    InterfaceClass = iface.Class,
                    InterfaceSubClass = iface.Subclass,
                    InterfaceProtocol = iface.Protocol
                };

                result = result.Concat(Packet.Encode(intDescriptor));
            }

            return result;
        }

        private void SetText(byte[] destination, string format, params object[] param)
        {
            var textBuffer = System.Text.Encoding.ASCII.GetBytes(string.Format(format, param));
            Array.Copy(textBuffer, destination, textBuffer.Length);
        }

        private IEnumerable<byte> HandleListDevicesCommand()
        {
            var header = new USBIP.Header
            {
                Version = ProtocolVersion,
                Command = USBIP.Command.ListDevicesReply,
            };

            var regCount = new USBIP.DeviceListCount
            {
                NumberOfExportedDevices = (uint)ChildCollection.Count
            };

            var result = Packet.Encode(header).Concat(Packet.Encode(regCount));

            foreach(var child in ChildCollection)
            {
                result = result.Concat(GenerateDeviceDescriptor(child.Value, (uint)child.Key, true));
            }

            return result;
        }

        private bool TryHandleDeviceAttachCommand(string deviceIdString, out IEnumerable<byte> response)
        {
            var success = true;
            var deviceId = 0;
            IUSBDevice device = null;

            var m = Regex.Match(deviceIdString, "([0-9]+)-([0-9]+)");
            if(m == null)
            {
                this.Log(LogLevel.Warning, "Unexpected device string when handling attach command: {0}. It should be in format '1-4'", deviceIdString);
                success = false;
            }
            else
            {
                var busId = int.Parse(m.Groups[1].Value);
                deviceId = int.Parse(m.Groups[2].Value);

                if(busId != ExportedBusId)
                {
                    this.Log(LogLevel.Warning, "Trying to attach to a non-existing bus 0x{0:X}", busId);
                    success = false;
                }
                else if(!TryGetByAddress(deviceId, out device))
                {
                    this.Log(LogLevel.Warning, "Trying to attach to a non-existing device 0x{0:X}", deviceId);
                    success = false;
                }
            }

            var header = new USBIP.Header
            {
                Version = ProtocolVersion,
                Command = USBIP.Command.AttachDeviceReply,
                Status = success ? 0 : 1u
            };

            response = Packet.Encode(header).AsEnumerable();
            if(success)
            {
                response = response.Concat(GenerateDeviceDescriptor(device, (uint)deviceId, false));
            }

            return success;
        }

        private State state;
        private URBHeader urbHeader;
        private int additionalDataCount;
        private CancellationTokenSource cancellationToken;

        private readonly int port;
        private readonly List<byte> buffer;
        private readonly SocketServerProvider server;

        private const uint ExportedBusId = 1;
        private const ushort ProtocolVersion = 0x0111;

        private enum USBSpeed
        {
            Unknown = 0,
            Low = 1,
            Full = 2,
            High = 3,
            Wireless = 4,
            Super = 5,
            SuperPlus = 6,
        }

        private enum State
        {
            WaitForCommand,
            WaitForBusId,
            WaitForURBHeader,
            HandleUnlinkCommand,
            WaitForURBRequest,
        }
    }
}
