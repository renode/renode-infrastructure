//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.USB;
using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class NRF_USBDTests
    {
        [SetUp]
        public void Setup()
        {
            machine = new Machine();
            ram = new MappedMemory(machine, 0x10000);
            machine.SystemBus.Register(ram, new BusRangeRegistration(0x20000000, 0x10000));
            usbd = new NRF_USBD(machine);
        }

        [TearDown]
        public void TearDown()
        {
            machine.Dispose();
        }

        [Test]
        public void ShouldInitAndHandleRegisters()
        {
            usbd.Reset();

            // ENABLE register (offset 0x500): write 1 to enable
            usbd.WriteDoubleWord(0x500, 1);
            Assert.AreEqual(1u, usbd.ReadDoubleWord(0x500));

            // USBADDRESS register (offset 0x470): write 0x2A
            usbd.WriteDoubleWord(0x470, 0x2A);
            Assert.AreEqual(0x2Au, usbd.ReadDoubleWord(0x470));
        }

        [Test]
        public void ShouldTriggerEp0SetupEventOnSetupPacket()
        {
            usbd.Reset();
            usbd.WriteDoubleWord(0x500, 1); // Enable

            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Direction.HostToDevice,
                Request = (byte)StandardRequest.SetAddress,
                Value = 0x0500,
                Index = 0x0000,
                Count = 0x0000
            };

            usbd.USBCore.HandleSetupPacket(setupPacket, result => {});

            // EVENTS_EP0SETUP offset 0x15C
            Assert.AreEqual(1u, usbd.ReadDoubleWord(0x15C));

            // BMREQUESTTYPE (0x480), BREQUEST (0x484), WVALUEL (0x488), WVALUEH (0x48C)
            Assert.AreEqual(0x00u, usbd.ReadDoubleWord(0x480));
            Assert.AreEqual((uint)StandardRequest.SetAddress, usbd.ReadDoubleWord(0x484));
            Assert.AreEqual(0x00u, usbd.ReadDoubleWord(0x488));
            Assert.AreEqual(0x05u, usbd.ReadDoubleWord(0x48C));
        }

        [Test]
        public void ShouldStallSetupPacketOnTasksEp0Stall()
        {
            usbd.Reset();
            usbd.WriteDoubleWord(0x500, 1); // Enable

            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Direction.DeviceToHost,
                Request = (byte)StandardRequest.GetDescriptor,
                Value = 0x0100,
                Count = 64
            };

            byte[] callbackResult = new byte[] { 0xFF };
            bool callbackInvoked = false;

            usbd.USBCore.HandleSetupPacket(setupPacket, result =>
            {
                callbackResult = result;
                callbackInvoked = true;
            });

            // TASKS_EP0STALL offset 0x054
            usbd.WriteDoubleWord(0x054, 1);

            Assert.IsTrue(callbackInvoked);
            Assert.IsNull(callbackResult);
        }

        [Test]
        public void ShouldPerformEasyDmaInTransfer()
        {
            usbd.Reset();
            usbd.WriteDoubleWord(0x500, 1); // Enable

            // Write 4 bytes to RAM at 0x20000000
            ram.WriteBytes(0, new byte[] { 0x11, 0x22, 0x33, 0x44 });

            // EPIN0_PTR offset 0x600 = 0x20000000
            usbd.WriteDoubleWord(0x600, 0x20000000);
            // EPIN0_MAXCNT offset 0x604 = 4
            usbd.WriteDoubleWord(0x604, 4);

            // TASKS_STARTEPIN0 offset 0x004: trigger DMA transfer
            usbd.WriteDoubleWord(0x004, 1);

            // EVENTS_ENDEPIN0 offset 0x108 should be 1
            Assert.AreEqual(1u, usbd.ReadDoubleWord(0x108));

            // EPIN0_AMOUNT offset 0x608 should be 4
            Assert.AreEqual(4u, usbd.ReadDoubleWord(0x608));
        }

        private Machine machine;
        private MappedMemory ram;
        private NRF_USBD usbd;
    }
}
