//
// Copyright (c) 2010-2019 Antmicro
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

            server = new SocketServerProvider(false);
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

        private void ResetBuffer()
        {
            additionalDataCount = 0;
            buffer.Clear();
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

                        if(urbHeader.Direction == URBDirection.Out)
                        {
                            if(packet.TransferBufferLength > 0)
                            {
                                additionalDataCount = (int)packet.TransferBufferLength;
                                this.Log(LogLevel.Debug, "Packet comes with {0} bytes of additional data. Waiting for it", additionalDataCount);
                                break;
                            }
                        }
                    }

                    this.Log(LogLevel.Noisy, "Handling URB request");

                    IUSBDevice device = null;

                    if(urbHeader.BusId != ExportedBusId)
                    {
                        this.Log(LogLevel.Warning, "URB command directed to a non-existing bus 0x{0:X}", urbHeader.BusId);
                        ResetBuffer();
                        SendResponse(GenerateURBReplyStall(urbHeader, packet));
                        break;
                    }
                    else if(!TryGetByAddress((int)urbHeader.DeviceId, out device))
                    {
                        this.Log(LogLevel.Warning, "URB command directed to a non-existing device 0x{0:X}", urbHeader.DeviceId);
                        ResetBuffer();
                        SendResponse(GenerateURBReplyStall(urbHeader, packet));
                        break;
                    }

                    var ep = device.USBCore.GetEndpoint((Direction)urbHeader.Direction, (int)urbHeader.EndpointNumber);
                    if(ep == null)
                    {
                        this.Log(LogLevel.Warning, "URB command directed to a non-existing '{0}' endpoint 0x{1:X}", (Direction)urbHeader.Direction, urbHeader.EndpointNumber);
                        ResetBuffer();
                        SendResponse(GenerateURBReplyStall(urbHeader, packet));
                        break;
                    }

                    // setup packet is passed in URB Request in a single `ulong` field
                    var setupPacket = buffer.Skip(Packet.CalculateOffset<URBRequest>(nameof(URBRequest.Setup))).Take(8);

                    var additionalData = (additionalDataCount > 0)
                        ? buffer.Skip(buffer.Count - additionalDataCount).Take(additionalDataCount)
                        : null;

                    var localHeader = urbHeader;

                    if(ep.TransferType == EndpointTransferType.Control)
                    {
                        this.Log(LogLevel.Noisy, "Handling SETUP transaction on endpoint #{0}", ep.Identifier);
                        HandleSetupTransaction(ep, setupPacket, (int)packet.TransferBufferLength, additionalData: additionalData != null ? additionalData.ToArray() : null, callback: result =>
                        {
                            if(result == null)
                            {
                                this.Log(LogLevel.Warning, "Could not send SETUP to endpoint #{0}", ep.Identifier);
                                SendResponse(GenerateURBReplyStall(localHeader, packet));
                            }
                            else
                            {
                                SendResponse(GenerateURBReplyOK(localHeader, packet, result));
                            }
                        });
                    }
                    else // non-setup transaction
                    {
                        if(ep.Direction == Direction.DeviceToHost)
                        {
                            this.Log(LogLevel.Noisy, "Reading from endpoint #{0}", ep.Identifier);
                            ReceiveDataFromEndpoint(ep, (int)packet.TransferBufferLength, result =>
                            {
                                if(result == null)
                                {
                                    this.Log(LogLevel.Warning, "Could not read data from endpoint #{0}", ep.Identifier);
                                    SendResponse(GenerateURBReplyStall(localHeader, packet));
                                }
                                else
                                {
                                    if(result.Length > packet.TransferBufferLength)
                                    {
                                        this.Log(LogLevel.Warning, "Received more data from the device than the Transfer Buffer Length. Truncating the result - some data will be lost!");
                                        result = result.Take((int)packet.TransferBufferLength).ToArray();
                                    }

                                    SendResponse(GenerateURBReplyOK(localHeader, packet, result));
                                }
                            });
                        }
                        else // Direction.HostToDevice
                        {
                            if(additionalData == null)
                            {
                                this.Log(LogLevel.Noisy, "There is no data to write to the endpoint #{0}", ep.Identifier);
                                ResetBuffer();
                                SendResponse(GenerateURBReplyStall(urbHeader, packet));
                                break;
                            }
                            else
                            {
                                this.Log(LogLevel.Noisy, "Writing to endpoint #{0}", ep.Identifier);

                                SendDataToEndpoint(ep, additionalData, result =>
                                {
                                    if(!result)
                                    {
                                        this.Log(LogLevel.Warning, "Could not write data to endpoint #{0}", ep.Identifier);
                                        SendResponse(GenerateURBReplyStall(localHeader, packet));
                                    }
                                    else
                                    {
                                        SendResponse(GenerateURBReplyOK(localHeader, packet));
                                    }
                                });
                            }
                        }
                    }
                    state = State.WaitForURBHeader;

                    ResetBuffer();
                    break;
                }

                case State.HandleUnlinkCommand:
                {
                    DebugHelper.Assert(buffer.Count <= Packet.CalculateLength<URBRequest>());
                    if(buffer.Count == Packet.CalculateLength<URBRequest>())
                    {
                        buffer.Clear();
                        state = State.WaitForURBHeader;

                        var header = new USBIP.URBHeader
                        {
                            Command = URBCommand.UnlinkReply,
                            SequenceNumber = urbHeader.SequenceNumber,
                            BusId = urbHeader.BusId,
                            DeviceId = urbHeader.DeviceId,
                            Direction = urbHeader.Direction,
                            EndpointNumber = urbHeader.EndpointNumber
                        };

                        // we probably should interrupt some waiting reads here
                        SendResponse(Packet.Encode(header));
                        // Padding to 48 bytes!
                        SendResponse(Packet.Encode(new USBIP.URBHeader()));
                    }
                    break;
                }

                default:
                    throw new ArgumentException(string.Format("Unexpected state: {0}", state));
            }
        }

        private IEnumerable<byte> GenerateURBReplyOK(URBHeader hdr, URBRequest req, IEnumerable<byte> data = null)
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

        private IEnumerable<byte> GenerateURBReplyStall(URBHeader hdr, URBRequest req)
        {
            var header = new URBHeader
            {
                Command = URBCommand.URBReply,
                SequenceNumber = hdr.SequenceNumber,
                BusId = hdr.BusId,
                DeviceId = hdr.DeviceId,
                Direction = hdr.Direction,
                EndpointNumber = hdr.EndpointNumber,
                FlagsOrStatus = -32// -EPIPE
            };

            return Packet.Encode(header).Concat(Packet.Encode(new URBReply())).AsEnumerable();
        }

        // using this blocking helper method simplifies the logic of other methods
        // + it seems to be harmless, as this logic is not executed as a result of
        // intra-emulation communication (where it could lead to deadlocks)
        private byte[] HandleSetupPacketSync(IUSBDevice device, SetupPacket setupPacket)
        {
            byte[] result = null;

            var mre = new ManualResetEvent(false);

            HandleSetupTransaction(device.USBCore.ControlEndpoint, Packet.Encode(setupPacket), int.MaxValue, callback: received =>
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

            var deviceDescriptorBytes = HandleSetupPacketSync(device, setupPacket);
            if(!Packet.TryDecode<USB.DeviceDescriptor>(deviceDescriptorBytes, out var result))
            {
                this.Log(LogLevel.Error, "Could not read Device Descriptor from the slave");
                result = default(USB.DeviceDescriptor);
            }
            return result;
        }

        private USB.ConfigurationDescriptor ReadConfigurationDescriptor(IUSBDevice device, byte configurationId, out USB.InterfaceDescriptor[] interfaceDescriptors)
        {
            interfaceDescriptors = new USB.InterfaceDescriptor[0];

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
            if(!Packet.TryDecode<USB.ConfigurationDescriptor>(configurationDescriptorBytes, out var result))
            {
                this.Log(LogLevel.Error, "Could not read Configuration Descriptor from the slave");
                return default(USB.ConfigurationDescriptor);
            }

            var innerInterfaceDescriptors = new USB.InterfaceDescriptor[result.NumberOfInterfaces];
            // ... read the total length of a recursive structure ...
            setupPacket.Count = result.TotalLength;
            // ... and only then read the whole structure again.
            var recursiveBytes = HandleSetupPacketSync(device, setupPacket);

            var currentOffset = Packet.CalculateLength<USB.ConfigurationDescriptor>();
            for(var i = 0; i < innerInterfaceDescriptors.Length; i++)
            {
                if(!Packet.TryDecode<USB.InterfaceDescriptor>(recursiveBytes, out innerInterfaceDescriptors[i], currentOffset))
                {
                    this.Log(LogLevel.Error, "Could not read Interface Descriptor #{0} from the slave", i);
                    return default(USB.ConfigurationDescriptor);
                }

                // skip next interface descriptor with associated endpoint descriptors (each of size 7)
                currentOffset += Packet.CalculateLength<USBIP.InterfaceDescriptor>()
                    + innerInterfaceDescriptors[i].NumberOfEndpoints * 7;
            }

            interfaceDescriptors = innerInterfaceDescriptors;
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

            void SetText(byte[] destination, string format, params object[] param)
            {
                var textBuffer = System.Text.Encoding.ASCII.GetBytes(string.Format(format, param));
                Array.Copy(textBuffer, destination, textBuffer.Length);
            }
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

        private void ReceiveDataFromEndpoint(USBEndpoint endpoint, int limit, Action<byte[]> callback)
        {
            var buffer = new Queue<byte>();

            ReceiveChunk();

            void ReceiveChunk()
            {

                if(EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(endpoint.Core.Device, out var machine))
                {
                    machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => ReceiveChunkInner());
                }
                else
                {
                    // this can happen when a device is attached directly to the USBIPServer
                    ReceiveChunkInner();
                }
            }

            void ReceiveChunkInner()
            {
                endpoint.HandleTransaction(USBTransactionStage.In(), deviceStage: inHandshake =>
                {
                    this.Log(LogLevel.Noisy, "IN transaction finished with {0} on endpoint #{1}", inHandshake.PacketID, endpoint.Identifier);
                    switch(inHandshake.PacketID)
                    {
                        case USBPacketId.NakHandshake:
                        {
                            // let's try again
                            ReceiveChunk();
                            return;
                        }

                        case USBPacketId.StallHandshake:
                        {
                            callback(null);
                            return;
                        }

                        // TODO: add Data1/Data0 verification
                        case USBPacketId.Data0:
                        case USBPacketId.Data1:
                        {
                            buffer.EnqueueRange(inHandshake.Payload);

                            if(inHandshake.Payload.Length < endpoint.MaximumPacketSize || buffer.Count >= limit)
                            {
                                // it was the last one OR we reached the limit
                                callback(buffer.ToArray());
                            }
                            else
                            {
                                ReceiveChunk();
                            }
                            return;
                        }

                        default:
                            this.Log(LogLevel.Warning, "Unexpected IN transaction result {0} on endpoint #{1}", inHandshake.PacketID, endpoint.Identifier);
                            callback(null);
                            return;
                    }            
                });
            }
        }

        private void SendDataToEndpoint(USBEndpoint endpoint, IEnumerable<byte> data, Action<bool> callback)
        {
            var outTransactions = new List<USBTransactionStage>();

            var currentToggle = false;
            var baseIndex = 0; 

            while(true)
            {
                var chunk = data.Skip(baseIndex).Take(endpoint.MaximumPacketSize).ToArray();
                baseIndex += chunk.Length;

                if(chunk.Length == 0 && outTransactions.Count > 0)
                {
                    break;
                }

                this.Log(LogLevel.Noisy, "Splitted into chunk of size {0} (baseIndex is now {1})", chunk.Length, baseIndex);
                outTransactions.Add(USBTransactionStage.Out(chunk, currentToggle));
                currentToggle = !currentToggle;
            }

            SendChunk(0);
           
            void SendChunk(int id)
            {
                if(EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(endpoint.Core.Device, out var machine))
                {
                    machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => SendChunkInner(id));
                }
                else
                {
                    // this can happen when a device is attached directly to the USBIPServer
                    SendChunkInner(id);
                }
            }

            void SendChunkInner(int id)
            {
                this.Log(LogLevel.Noisy, "Sending chunk #{0} out of {1}", id, outTransactions.Count);

                endpoint.HandleTransaction(outTransactions[id], deviceStage: outHandshake =>
                {
                    this.Log(LogLevel.Noisy, "OUT transaction finished with {0} on endpoint #{1}", outHandshake.PacketID, endpoint.Identifier);
                    switch(outHandshake.PacketID)
                    {
                        case USBPacketId.NakHandshake:
                        {
                            // let's try again the same chunk
                            // TODO: add some delay!?
                            SendChunk(id);
                            return;
                        }

                        case USBPacketId.StallHandshake:
                        {
                            callback(false);
                            return;
                        }

                        case USBPacketId.AckHandshake:
                        {
                            if(id == outTransactions.Count - 1)
                            {
                                // it was the last one
                                callback(true);
                            }
                            else
                            {
                                SendChunk(id + 1);
                            }
                            return;
                        }

                        default:
                            this.Log(LogLevel.Warning, "Unexpected OUT transaction result {0} on endpoint #{1}", outHandshake.PacketID, endpoint.Identifier);
                            callback(false);
                            return;
                    }
                });
            }
        }

        private void HandleSetupTransaction(USBEndpoint endpoint, IEnumerable<byte> setupPacket, int limit, Action<byte[]> callback, IEnumerable<byte> additionalData = null)
        {
            var setupStage = USBTransactionStage.Setup(setupPacket.ToArray());

            endpoint.HandleTransaction(setupStage, deviceStage: setupHandshake =>
            {
                this.Log(LogLevel.Noisy, "SETUP transaction finished with {0} on endpoint #{1}", setupHandshake.PacketID, endpoint.Identifier);
                switch(setupHandshake.PacketID)
                {
                    case USBPacketId.StallHandshake:
                    {
                        callback(null);
                        break;
                    }

                    case USBPacketId.AckHandshake:
                    {
                        if(additionalData != null)
                        {
                            this.Log(LogLevel.Noisy, "SETUP - sending additional data");
                            SendDataToEndpoint(endpoint, additionalData, callback: result =>
                            {
                                if(!result)
                                {
                                    callback(null);
                                }
                                else
                                {
                                    this.Log(LogLevel.Noisy, "SETUP STATUS PHASE - sending an empty IN packet");
                                    // status phase
                                    // limit 64 here is just something non-zero
                                    ReceiveDataFromEndpoint(endpoint, 64, callback: statusResult =>
                                    {
                                        if(statusResult == null)
                                        {
                                            callback(null);
                                        }
                                        else if(statusResult.Length != 0)
                                        {
                                            this.Log(LogLevel.Warning, "Unexpected DATA package with payload. There should be just an empty IN status response");
                                            callback(null);
                                        }
                                        else
                                        {
                                            callback(statusResult);
                                        }
                                    });
                                }
                            });
                        }
                        else
                        {
                            // no data to send - it's time for the IN token - it's either a status or just reading the data
                            this.Log(LogLevel.Noisy, "No additional data to send, sending IN token");
                            ReceiveDataFromEndpoint(endpoint, limit, callback: receiveResult =>
                            {
                                if(receiveResult == null)
                                {
                                    callback(null);
                                }
                                else if(receiveResult.Length == 0)
                                {
                                    this.Log(LogLevel.Noisy, "Received an empty response - it must have been a status stage");
                                    callback(receiveResult);
                                }
                                else
                                {
                                    // it was a read phase -> generate OUT status
                                    this.Log(LogLevel.Noisy, "OUT status stage");
                                    SendDataToEndpoint(endpoint, Enumerable.Empty<byte>(), callback: statusResult =>
                                    {
                                        if(!statusResult)
                                        {
                                            callback(null);
                                        }
                                        else
                                        {
                                            callback(receiveResult);
                                        }
                                    });
                                }
                            });
                        }
                        break;
                    }

                    default:
                    {
                        this.Log(LogLevel.Error, "Received unexpected response {0} (0x{0:X}) to a setup token packet", setupHandshake.PacketID);
                        callback(null);
                        break;
                    }
                }
            });
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
            Low = 0,
            Full = 1,
            High = 2,
            Super = 3
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
