//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MPC56xx_I2CTests
    {
        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
            controller = new MPC56xx_I2C(machine, inputClockFrequency: 1000000, byteTransferTicks: 10);
            machine.SystemBus.Register(controller, new BusRangeRegistration(0x40000, 0x8000));
            eeprom = new AT24C32(machine);
            controller.Register(eeprom, new NumberRegistrationPoint<int>(0x50));
        }

        [TearDown]
        public void TearDown()
        {
            machine.Dispose();
        }

        [Test]
        public void ShouldDelayAddressCompletionAndInterrupt()
        {
            controller.WriteByte(2, 0x70);
            controller.WriteByte(4, 0xA0);
            Assert.True(controller.IsTransferPending);
            Assert.False(controller.IRQ.IsSet);
            AdvanceMicroseconds(9);
            Assert.False(controller.IRQ.IsSet);
            AdvanceMicroseconds(1);
            Assert.False(controller.IsTransferPending);
            Assert.True(controller.IRQ.IsSet);
            Assert.AreEqual(0, controller.ReadByte(3) & 1);
            Assert.AreEqual(1, controller.AcknowledgedAddressCount);
            controller.WriteByte(3, 2);
            Assert.False(controller.IRQ.IsSet);
        }

        [Test]
        public void ShouldNackMissingDevice()
        {
            controller.WriteByte(2, 0x70);
            Send(0xA2);
            Assert.AreEqual(1, controller.ReadByte(3) & 1);
            Assert.AreEqual(0, controller.AcknowledgedAddressCount);
        }

        [Test]
        public void ShouldProgramOnStopWrapPageAndAckAfterBusyCycle()
        {
            controller.WriteByte(2, 0x70);
            foreach(var value in new byte[] { 0xA0, 0, 0x3E, 0x11, 0x22, 0x33, 0x44 })
            {
                Send(value);
            }
            Assert.False(eeprom.IsBusy);
            Assert.AreEqual(0xFF, eeprom.ReadMemoryByte(0x3E));
            controller.WriteByte(2, 0x50);
            Assert.True(eeprom.IsBusy);
            controller.WriteByte(2, 0x70);
            Send(0xA0);
            Assert.AreEqual(1, controller.ReadByte(3) & 1);
            controller.WriteByte(2, 0x50);
            Assert.AreEqual(0xFF, eeprom.ReadMemoryByte(0x3E));
            AdvanceMicroseconds(5000);
            Assert.False(eeprom.IsBusy);
            controller.WriteByte(2, 0x70);
            Send(0xA0);
            Assert.AreEqual(0, controller.ReadByte(3) & 1);
            Assert.AreEqual(1, eeprom.BusyAddressNackCount);
            Assert.AreEqual(1, eeprom.ProgramCount);
            Assert.AreEqual(0x11, eeprom.ReadMemoryByte(0x3E));
            Assert.AreEqual(0x22, eeprom.ReadMemoryByte(0x3F));
            Assert.AreEqual(0x33, eeprom.ReadMemoryByte(0x20));
            Assert.AreEqual(0x44, eeprom.ReadMemoryByte(0x21));
            Assert.AreEqual(0xFF, eeprom.ReadMemoryByte(0x40));
        }

        [Test]
        public void ShouldReadAfterRepeatedStartAndStopBeforeFinalDrain()
        {
            eeprom.BeginTransmission(false);
            eeprom.Write(new byte[] { 0, 5, 0x5A });
            eeprom.EndTransmission(true);
            AdvanceMicroseconds(5000);
            controller.WriteByte(2, 0x70);
            Send(0xA0);
            Send(0);
            Send(5);
            controller.WriteByte(2, 0x74);
            Send(0xA1);
            Assert.False(eeprom.IsBusy, "Address-only repeated START must not program");
            controller.WriteByte(3, 2);
            controller.WriteByte(2, 0x68);
            controller.ReadByte(4); // Dummy read starts the receive shift register.
            Assert.True(controller.IsTransferPending);
            AdvanceMicroseconds(10);
            Assert.True(controller.IRQ.IsSet);
            controller.WriteByte(2, 0x48); // STOP before reading the final byte.
            controller.WriteByte(3, 2);
            Assert.AreEqual(0x5A, controller.ReadByte(4));
            Assert.False(controller.IsTransferPending);
            AdvanceMicroseconds(20);
            Assert.False(controller.IRQ.IsSet);
            Assert.AreEqual(1, controller.ReadByteCount);
        }

        [Test]
        public void ShouldReadSequentiallyAcrossEndOfMemory()
        {
            Assert.True(eeprom.BeginTransmission(false));
            eeprom.Write(new byte[] { 0x0F, 0xFF, 0x5A });
            eeprom.EndTransmission(true);
            AdvanceMicroseconds(5000);
            Assert.True(eeprom.BeginTransmission(false));
            eeprom.Write(new byte[] { 0x0F, 0xFF });
            eeprom.EndTransmission(false);
            Assert.False(eeprom.IsBusy);
            Assert.True(eeprom.BeginTransmission(true));
            CollectionAssert.AreEqual(new byte[] { 0x5A, 0xFF }, eeprom.Read(2));
            Assert.AreEqual(1, eeprom.CurrentAddress);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShouldDiscardUncommittedWriteOnReset(bool stop)
        {
            eeprom.BeginTransmission(false);
            eeprom.Write(new byte[] { 0, 1, 0x42 });
            if(stop)
            {
                eeprom.EndTransmission(true);
            }
            eeprom.Reset();
            AdvanceMicroseconds(10000);
            Assert.False(eeprom.IsBusy);
            Assert.AreEqual(0xFF, eeprom.ReadMemoryByte(1));
            Assert.AreEqual(0, eeprom.ProgramCount);
        }

        [Test]
        public void ShouldPreserveCommittedMemoryOnReset()
        {
            eeprom.BeginTransmission(false);
            eeprom.Write(new byte[] { 0, 1, 0x42 });
            eeprom.EndTransmission(true);
            AdvanceMicroseconds(5000);
            eeprom.Reset();
            Assert.AreEqual(0x42, eeprom.ReadMemoryByte(1));
        }

        [Test]
        public void ShouldHonorWriteProtection()
        {
            var protectedEeprom = new AT24C32(machine, writeProtected: true);
            Assert.True(protectedEeprom.BeginTransmission(false));
            protectedEeprom.Write(new byte[] { 0, 1, 0x42 });
            protectedEeprom.EndTransmission(true);
            Assert.False(protectedEeprom.IsBusy);
            AdvanceMicroseconds(5000);
            Assert.AreEqual(0xFF, protectedEeprom.ReadMemoryByte(1));
            Assert.AreEqual(0, protectedEeprom.ProgramCount);
        }

        private void Send(byte value)
        {
            controller.WriteByte(3, 2);
            controller.WriteByte(4, value);
            AdvanceMicroseconds(10);
            Assert.True(controller.IRQ.IsSet);
        }

        private void AdvanceMicroseconds(ulong value)
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMicroseconds(value));
        }

        private Machine machine;
        private MPC56xx_I2C controller;
        private AT24C32 eeprom;
    }
}
