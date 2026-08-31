//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Peripherals;
using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class USBDeviceCoreTests
    {
        [Test]
        public void ShouldHandleStallSetupPacketWithoutException()
        {
            var dummyDevice = new DummyUSBDevice((packet, data, callback) =>
            {
                // Simulate a STALL response by passing null to the result callback
                callback(null);
            });

            byte[] receivedResult = new byte[] { 0xDE, 0xAD };
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Vendor,
                Direction = Direction.HostToDevice,
                Request = 0x42
            };

            Assert.DoesNotThrow(() =>
            {
                dummyDevice.USBCore.HandleSetupPacket(setupPacket, result =>
                {
                    receivedResult = result;
                });
            });

            Assert.IsNull(receivedResult);
        }

        private class DummyUSBDevice : IUSBDevice
        {
            public DummyUSBDevice(Action<SetupPacket, byte[], Action<byte[]>> customSetupHandler = null)
            {
                USBCore = new USBDeviceCore(this, customSetupPacketHandler: customSetupHandler);
            }

            public void Reset()
            {
                USBCore.Reset();
            }

            public USBDeviceCore USBCore { get; }
        }
    }
}
