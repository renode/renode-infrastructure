//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Extensions.Utilities.USBIP;
using Antmicro.Renode.Utilities.Packets;
using NUnit.Framework;
using USB = Antmicro.Renode.Extensions.Utilities.USB;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class USBIPServerTests
    {
        [SetUp]
        public void Setup()
        {
            server = new USBIPServer(3240);
        }

        [TearDown]
        public void TearDown()
        {
            server.Dispose();
        }

        [Test]
        public void ShouldReadFromDeviceToHostEndpointAsynchronously()
        {
            var device = new BlockingEndpointUSBDevice();
            device.USBCore.SelectedConfiguration = device.USBCore.Configurations.First();
            server.Register(device, 1);

            // 1. Attach device (Command.AttachDevice)
            var attachHeader = new Header
            {
                Version = 0x0111,
                Command = Command.AttachDevice,
                Status = 0
            };
            var busIdBytes = new byte[32];
            Encoding.ASCII.GetBytes("1-1").CopyTo(busIdBytes, 0);
            var attachDesc = new AttachDeviceCommandDescriptor
            {
                BusId = busIdBytes
            };
            FeedBytes(Packet.Encode(attachHeader).Concat(Packet.Encode(attachDesc)));

            // 2. Send URBRequest for endpoint 1 (Direction: DeviceToHost / IN)
            var urbHeader = new URBHeader
            {
                Command = URBCommand.URBRequest,
                SequenceNumber = 1,
                BusId = 1,
                DeviceId = 1,
                Direction = URBDirection.In,
                EndpointNumber = 1
            };
            var urbRequest = new URBRequest
            {
                TransferBufferLength = 64
            };

            // Without asynchronous read on ThreadPool, calling FeedBytes blocks indefinitely waiting on ep.Read!
            var feedTask = Task.Run(() => FeedBytes(Packet.Encode(urbHeader).Concat(Packet.Encode(urbRequest))));
            var completedInTime = feedTask.Wait(TimeSpan.FromMilliseconds(500));

            Assert.IsTrue(completedInTime, "FeedBytes blocked on endpoint read instead of queuing asynchronously.");
        }

        [Test]
        public void ShouldReportErrorStatusAndZeroLengthOnStallReply()
        {
            var hdr = new URBHeader
            {
                Command = URBCommand.URBRequest,
                SequenceNumber = 123,
                DeviceId = 1,
                Direction = URBDirection.In,
                EndpointNumber = 0
            };
            var req = new URBRequest
            {
                TransferBufferLength = 64
            };

            var replyBytes = InvokeGenerateURBReply(hdr, req, null, status: -32).ToArray();
            var replyHeader = Packet.Decode<URBHeader>(replyBytes);
            var replyBody = Packet.Decode<URBReply>(replyBytes, Packet.CalculateLength<URBHeader>());

            Assert.AreEqual(unchecked((uint)-32), replyHeader.FlagsOrStatus);
            Assert.AreEqual(0u, replyBody.ActualLength);
        }

        [Test]
        public void ShouldCalculateCorrectLengthForInTransferWithZeroOrEmptyData()
        {
            var hdr = new URBHeader
            {
                Command = URBCommand.URBRequest,
                SequenceNumber = 124,
                DeviceId = 1,
                Direction = URBDirection.In,
                EndpointNumber = 1
            };
            var req = new URBRequest
            {
                TransferBufferLength = 64
            };

            var replyBytes = InvokeGenerateURBReply(hdr, req, Array.Empty<byte>(), status: 0).ToArray();
            var replyBody = Packet.Decode<URBReply>(replyBytes, Packet.CalculateLength<URBHeader>());

            Assert.AreEqual(0u, replyBody.ActualLength);
        }

        [Test]
        public void ShouldCalculateCorrectLengthForOutTransfer()
        {
            var hdr = new URBHeader
            {
                Command = URBCommand.URBRequest,
                SequenceNumber = 125,
                DeviceId = 1,
                Direction = URBDirection.Out,
                EndpointNumber = 1
            };
            var req = new URBRequest
            {
                TransferBufferLength = 64
            };

            var replyBytes = InvokeGenerateURBReply(hdr, req, null, status: 0).ToArray();
            var replyBody = Packet.Decode<URBReply>(replyBytes, Packet.CalculateLength<URBHeader>());

            Assert.AreEqual(64u, replyBody.ActualLength);
        }

        [Test]
        public void ShouldReportFullSpeedByDefaultInDeviceDescriptor()
        {
            var dummyDevice = new DummyUSBDevice();
            var descBytes = InvokeGenerateDeviceDescriptor(dummyDevice, 1, false).ToArray();
            var devDescriptor = Packet.Decode<DeviceDescriptor>(descBytes);

            // USBSpeed.Full is 2; USBSpeed.High is 3
            Assert.AreEqual((int)USBSpeed.Full, devDescriptor.Speed);
        }

        [Test]
        public void ShouldReportHighSpeedWhenRequestedViaUSBCore()
        {
            var dummyDevice = new DummyUSBDevice(USBSpeed.High);
            var descBytes = InvokeGenerateDeviceDescriptor(dummyDevice, 1, false).ToArray();
            var devDescriptor = Packet.Decode<DeviceDescriptor>(descBytes);

            Assert.AreEqual((int)USBSpeed.High, devDescriptor.Speed);
        }

        [Test]
        public void ShouldReportSpeedWhenOverriddenOnServer()
        {
            var dummyDevice = new DummyUSBDevice();
            server.SetDeviceSpeed(dummyDevice, USBSpeed.High);
            var descBytes = InvokeGenerateDeviceDescriptor(dummyDevice, 1, false).ToArray();
            var devDescriptor = Packet.Decode<DeviceDescriptor>(descBytes);

            Assert.AreEqual((int)USBSpeed.High, devDescriptor.Speed);
        }

        [Test]
        public void ShouldTimeoutOnUnresponsiveSetupPacket()
        {
            var unresponsiveDevice = new UnresponsiveUSBDevice();
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Direction.DeviceToHost,
                Request = (byte)StandardRequest.GetDescriptor
            };

            byte[] result = null;
            Assert.DoesNotThrow(() =>
            {
                result = InvokeHandleSetupPacketSync(unresponsiveDevice, setupPacket, 50);
            });

            Assert.IsNull(result);
        }

        [Test]
        public void ShouldHandleTruncatedConfigurationDescriptorSafely()
        {
            var truncatedDevice = new TruncatedDescriptorUSBDevice();
            var result = InvokeReadConfigurationDescriptor(truncatedDevice, 0, out var ifaces);

            Assert.IsEmpty(ifaces);
        }

        private IEnumerable<byte> InvokeGenerateURBReply(URBHeader hdr, URBRequest req, IEnumerable<byte> data, int status)
        {
            var method = typeof(USBIPServer).GetMethod("GenerateURBReply", BindingFlags.NonPublic | BindingFlags.Instance);
            return (IEnumerable<byte>)method.Invoke(server, new object[] { hdr, req, data, status });
        }

        private IEnumerable<byte> InvokeGenerateDeviceDescriptor(IUSBDevice device, uint deviceNumber, bool includeInterfaces)
        {
            var method = typeof(USBIPServer).GetMethod("GenerateDeviceDescriptor", BindingFlags.NonPublic | BindingFlags.Instance);
            return (IEnumerable<byte>)method.Invoke(server, new object[] { device, deviceNumber, includeInterfaces });
        }

        private byte[] InvokeHandleSetupPacketSync(IUSBDevice device, SetupPacket setupPacket, int timeoutMs)
        {
            var method = typeof(USBIPServer).GetMethod("HandleSetupPacketSync", BindingFlags.NonPublic | BindingFlags.Instance);
            return (byte[])method.Invoke(server, new object[] { device, setupPacket, timeoutMs });
        }

        private USB.ConfigurationDescriptor InvokeReadConfigurationDescriptor(IUSBDevice device, byte configId, out USB.InterfaceDescriptor[] ifaces)
        {
            var method = typeof(USBIPServer).GetMethod("ReadConfigurationDescriptor", BindingFlags.NonPublic | BindingFlags.Instance);
            var parameters = new object[] { device, configId, null };
            var result = (USB.ConfigurationDescriptor)method.Invoke(server, parameters);
            ifaces = (USB.InterfaceDescriptor[])parameters[2];
            return result;
        }

        private void FeedBytes(IEnumerable<byte> bytes)
        {
            var method = typeof(USBIPServer).GetMethod("HandleIncomingData", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach(var b in bytes)
            {
                method.Invoke(server, new object[] { (int)b });
            }
        }

        private USBIPServer server;

        private class DummyUSBDevice : IUSBDevice
        {
            public DummyUSBDevice(USBSpeed speed = USBSpeed.Full)
            {
                USBCore = new USBDeviceCore(this, speed: speed);
            }

            public void Reset()
            {
                USBCore.Reset();
            }

            public USBDeviceCore USBCore { get; }
        }

        private class BlockingEndpointUSBDevice : IUSBDevice
        {
            public BlockingEndpointUSBDevice()
            {
                USBCore = new USBDeviceCore(this)
                    .WithConfiguration(configure: c =>
                        c.WithInterface(new Core.USB.HID.Interface(this, 0),
                            configure: i =>
                                i.WithEndpoint(
                                    Direction.DeviceToHost,
                                    EndpointTransferType.Interrupt,
                                    maximumPacketSize: 0x4,
                                    interval: 0xa,
                                    createdEndpoint: out endpoint)));
            }

            public void Reset()
            {
                USBCore.Reset();
            }

            public USBDeviceCore USBCore { get; }
            private USBEndpoint endpoint;
        }

        private class UnresponsiveUSBDevice : IUSBDevice
        {
            public UnresponsiveUSBDevice()
            {
                USBCore = new USBDeviceCore(this, customSetupPacketHandler: (packet, data, cb) =>
                {
                    // Never invokes cb to simulate hang/unresponsive firmware
                });
            }

            public void Reset()
            {
                USBCore.Reset();
            }

            public USBDeviceCore USBCore { get; }
        }

        private class TruncatedDescriptorUSBDevice : IUSBDevice
        {
            public TruncatedDescriptorUSBDevice()
            {
                USBCore = new USBDeviceCore(this, customSetupPacketHandler: (packet, data, cb) =>
                {
                    // Return truncated 2 bytes
                    cb(new byte[] { 0x02, 0x02 });
                });
            }

            public void Reset()
            {
                USBCore.Reset();
            }

            public USBDeviceCore USBCore { get; }
        }
    }
}
