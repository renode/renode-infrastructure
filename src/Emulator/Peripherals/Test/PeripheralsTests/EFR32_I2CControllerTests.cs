//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.I2C;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Mocks;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensors;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class EFR32_I2CControllerTests
    {
        [SetUp]
        public void SetupController()
        {
            machine = new Machine();
            controller = new EFR32_I2CController(machine);
            readTestPeripheral = new DummyI2CSlave();
            writeTestPeripheral = new EchoI2CDevice();

            controller.Reset();
            readTestPeripheral.Reset();
            // Enable the controller
            // Also enable autoack, which will automatically acknowledge all incoming bytes
            // so we don't have to do it manually.
            controller.WriteDoubleWord(0x0, 0x1 | (1 << 2));
            // Enable all interrupts
            controller.WriteDoubleWord(0x40, 0x7FFFF);

            machine.SystemBus.Register(controller, new BusRangeRegistration(0x4A010000, 0x400));
            controller.Register(readTestPeripheral, new NumberRegistrationPoint<int>(readTestPeripheralAddress));
            controller.Register(writeTestPeripheral, new NumberRegistrationPoint<int>(writeTestPeripheralAddress));
        }

        [Test]
        public void WriteToSlaveUsingSingleByteTransfer()
        {
            BeginTransmission(writeTestPeripheralAddress, TransmissionType.Write);
            // Send some magic bytes to the device
            foreach(var b in testMagicSequence)
            {
                // Write the byte to send
                controller.WriteDoubleWord(0x2C, b);
            }
            EndTransmission();

            Assert.AreEqual(testMagicSequence, writeTestPeripheral.Read(testMagicSequence.Length));
        }

        [Test, Timeout(2000)]
        public void ReadFromSlaveUsingSingleByteTransfer()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);
            var bytes = ReadBytes(testMagicSequence.Length);
            EndTransmission();

            Assert.AreEqual(testMagicSequence, bytes);
        }

        [Test]
        public void PeekSingleByte()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);
            Assert.AreEqual(testMagicSequence[0], PeekRxByte());
            EndTransmission();
        }

        [Test]
        public void WriteToSlaveUsingDoubleByteTransfer()
        {
            BeginTransmission(writeTestPeripheralAddress, TransmissionType.Write);
            // Send some magic bytes to the device, two bytes at a time
            for(var i = 0; i < testMagicSequence.Length; i += 2)
            {
                uint value = (uint)testMagicSequence[i] | ((uint)testMagicSequence[i + 1] << 8);
                controller.WriteDoubleWord(0x30, value);
            }
            EndTransmission();

            Assert.AreEqual(testMagicSequence, writeTestPeripheral.Read(testMagicSequence.Length));
        }

        [Test, Timeout(2000)]
        public void ReadFromSlaveUsingDoubleByteTransfer()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);
            var bytes = ReadBytesDouble(testMagicSequence.Length);
            EndTransmission();

            Assert.AreEqual(testMagicSequence, bytes);
        }

        [Test]
        public void PeekDoubleByte()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);

            var value = PeekRxDoubleByte();
            Assert.AreEqual(testMagicSequence[0], (byte)value);
            Assert.AreEqual(testMagicSequence[1], (byte)(value >> 8));

            EndTransmission();
        }

        [Test]
        public void InterruptReadUnderflow()
        {
            ReadRxByte(); // we only care about the IRQ flags afterwards

            AssertInterrupt(irqRxUnderflow);
        }

        [Test]
        public void InterruptMasterStop()
        {
            // Send stop command
            controller.WriteDoubleWord(0x04, 0x2);

            AssertInterrupt(irqMasterStop);
        }

        [Test]
        public void InterruptStart()
        {
            // Send start command
            controller.WriteDoubleWord(0x4, 0x1);
            // Start should have fired
            AssertInterrupt(irqStart);
        }

        [Test]
        public void InterruptAck()
        {
            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);
            // As there is a peripheral connected at that address, this should be ACK'd
            AssertInterrupt(irqAck);
        }

        [Test]
        public void InterruptNack()
        {
            BeginTransmission(0x40, TransmissionType.Read);
            // No peripheral at that address, this should be NACK'd
            AssertInterrupt(irqNack);
        }

        [Test]
        public void InterruptBusHold()
        {
            // On HW, the controller can lose bus arbitration at any time during the transmission
            // For simulation purposes, the bus is "virtually" held by the controller for the entire duration of the transmission
            BeginTransmission(readTestPeripheralAddress, TransmissionType.Read);
            AssertInterrupt(irqBusHold);
            EndTransmission();
        }

        [Test]
        public void InterruptTransmitBufferLevel()
        {
            BeginTransmission(writeTestPeripheralAddress, TransmissionType.Write);
            // Write test byte
            controller.WriteDoubleWord(0x2C, 0x42);
            EndTransmission();

            // TXBL should have fired
            AssertInterrupt(irqTxBufferLevel);

            // Write a test byte to the transmit buffer
            controller.WriteDoubleWord(0x2C, 0x42);
            // This should have cleared the TXBL condition
            AssertInterruptCleared(irqTxBufferLevel);
        }

        [Test]
        public void InterruptTransferCompleted()
        {
            BeginTransmission(writeTestPeripheralAddress, TransmissionType.Write);
            // Write test byte
            controller.WriteDoubleWord(0x2C, 0x42);
            EndTransmission();

            // TXC should have fired
            AssertInterrupt(irqTransferCompleted);
        }

        [Test]
        public void TestWithBMP180()
        {
            var sensor = new BMP180();
            sensor.Reset();
            controller.Register(sensor, new NumberRegistrationPoint<int>(0x77));

            // Send the register address to start reading from (0xF6 - OutMSB)
            BeginTransmission(0x77, TransmissionType.Write);
            controller.WriteDoubleWord(0x2c, 0xF6);
            EndTransmission();

            // Now, read from the sensor
            BeginTransmission(0x77, TransmissionType.Read);
            var bytes = ReadBytes(8);
            EndTransmission();

            // The reset value for OutMSB is 0x80
            Assert.NotZero(bytes.Length);
            Assert.AreEqual(0x80, bytes[0]);
        }

        private void BeginTransmission(int address, TransmissionType type)
        {
            // Start transmission
            controller.WriteDoubleWord(0x4, 0x1);

            // Write address of peripheral, with LSB cleared (direction: write)
            var data = (address << 1) | (type == TransmissionType.Read ? 0x1 : 0x0);
            controller.WriteDoubleWord(0x2C, (uint)data);
        }

        private void EndTransmission()
        {
            // Stop transmission
            controller.WriteDoubleWord(0x4, 0x2);
        }

        private void EnqueueDummyBytes(IEnumerable<byte> response)
        {
            foreach (var @byte in response)
            {
                readTestPeripheral.EnqueueResponseByte(@byte);
            }
        }

        private uint ReadStatus()
        {
            return controller.ReadDoubleWord(0xC);
        }

        private uint ReadInterruptStatus()
        {
            return controller.ReadDoubleWord(0x34);
        }

        private byte ReadRxByte()
        {
            return (byte)controller.ReadDoubleWord(0x1C);
        }

        private ushort ReadRxDoubleByte()
        {
            return (ushort)controller.ReadDoubleWord(0x20);
        }

        private byte PeekRxByte()
        {
            return (byte)controller.ReadDoubleWord(0x24);
        }

        private ushort PeekRxDoubleByte()
        {
            return (ushort)controller.ReadDoubleWord(0x28);
        }

        private byte[] ReadBytes(int count)
        {
            var bytes = new Queue<byte>();

            // Read bytes while data is still available in the Rx buffer
            var received = 0;
            while(received < count && (ReadStatus() & 0x100) != 0)
            {
                // Make sure there's actually a byte waiting in the Rx buffer
                Assert.AreEqual(irqMasterStop, ReadStatus() & irqMasterStop);
                bytes.Enqueue(ReadRxByte());
                received += 1;
            }

            return bytes.ToArray();
        }

        private byte[] ReadBytesDouble(int count)
        {
            var bytes = new Queue<byte>();

            var received = 0;
            // Read bytes while data is still available in the Rx buffer
            while(received < count && (ReadStatus() & 0x100) != 0)
            {
                // Make sure the rx buffer actually contains two bytes to read;
                // reading from it is otherwise undefined behavior
                Assert.AreEqual((1 << 9), ReadStatus() & (1 << 9));
                // Reading is done two bytes at a time (RXDOUBLE)
                var value = ReadRxDoubleByte();
                bytes.Enqueue((byte)value);
                bytes.Enqueue((byte)(value >> 8));
                received += 2;
            }

            return bytes.ToArray();
        }

        private void AssertInterrupt(uint irqMask)
        {
            Assert.AreEqual(irqMask, ReadInterruptStatus() & irqMask);
        }

        private void AssertInterruptCleared(uint irqMask)
        {
            Assert.AreEqual(0, ReadInterruptStatus() & irqMask);
        }

        private enum TransmissionType
        {
            Read,
            Write
        }

        private IMachine machine;
        private EFR32_I2CController controller;
        private EchoI2CDevice writeTestPeripheral;
        private DummyI2CSlave readTestPeripheral;
        private const int readTestPeripheralAddress = 0x10;
        private const int writeTestPeripheralAddress = 0x20;
        private const int irqRxUnderflow = 1 << 13;
        private const int irqMasterStop = 1 << 8;
        private const int irqStart = 1 << 0;
        private const int irqAck = 1 << 6;
        private const int irqNack = 1 << 7;
        private const int irqBusHold = 1 << 11;
        private const int irqTxBufferLevel = 1 << 4;
        private const int irqTransferCompleted = 1 << 3;
        // The length of the below sequence needs to be a multiple of 2 for double-width transfer tests to work
        private static byte[] testMagicSequence = { 0x11, 0x22, 0x33, 0x44, 0xFF, 0xEE, 0xDD, 0xCC, 0x11, 0x22, 0x33, 0x44, 0xFF, 0xEE, 0xDD, 0xCC };
    }
}