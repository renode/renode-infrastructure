//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Threading;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Mocks;
using Antmicro.Renode.Peripherals.Sensors;

using NUnit.Framework;

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
            controller.Register(readTestPeripheral, new NumberRegistrationPoint<int>(ReadTestPeripheralAddress));
            controller.Register(writeTestPeripheral, new NumberRegistrationPoint<int>(WriteTestPeripheralAddress));
        }

        [Test]
        public void WriteToSlaveUsingSingleByteTransfer()
        {
            BeginTransmission(WriteTestPeripheralAddress, TransmissionType.Write);
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

            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);
            var bytes = ReadBytes(testMagicSequence.Length);
            EndTransmission();

            Assert.AreEqual(testMagicSequence, bytes);
        }

        [Test]
        public void PeekSingleByte()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);
            Assert.AreEqual(testMagicSequence[0], PeekRxByte());
            EndTransmission();
        }

        [Test]
        public void WriteToSlaveUsingDoubleByteTransfer()
        {
            BeginTransmission(WriteTestPeripheralAddress, TransmissionType.Write);
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

            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);
            var bytes = ReadBytesDouble(testMagicSequence.Length);
            EndTransmission();

            Assert.AreEqual(testMagicSequence, bytes);
        }

        [Test]
        public void PeekDoubleByte()
        {
            // Write some data that will be mirrored back to the controller
            EnqueueDummyBytes(testMagicSequence);

            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);

            var value = PeekRxDoubleByte();
            Assert.AreEqual(testMagicSequence[0], (byte)value);
            Assert.AreEqual(testMagicSequence[1], (byte)(value >> 8));

            EndTransmission();
        }

        [Test]
        public void InterruptReadUnderflow()
        {
            ReadRxByte(); // we only care about the IRQ flags afterwards

            AssertInterrupt(IrqRxUnderflow);
        }

        [Test]
        public void InterruptMasterStop()
        {
            // Send stop command
            controller.WriteDoubleWord(0x04, 0x2);

            AssertInterrupt(IrqMasterStop);
        }

        [Test]
        public void InterruptStart()
        {
            // Send start command
            controller.WriteDoubleWord(0x4, 0x1);
            // Start should have fired
            AssertInterrupt(IrqStart);
        }

        [Test]
        public void InterruptAck()
        {
            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);
            // As there is a peripheral connected at that address, this should be ACK'd
            AssertInterrupt(IrqAck);
        }

        [Test]
        public void InterruptNack()
        {
            BeginTransmission(0x40, TransmissionType.Read);
            // No peripheral at that address, this should be NACK'd
            AssertInterrupt(IrqNack);
        }

        [Test]
        public void InterruptBusHold()
        {
            // On HW, the controller can lose bus arbitration at any time during the transmission
            // For simulation purposes, the bus is "virtually" held by the controller for the entire duration of the transmission
            BeginTransmission(ReadTestPeripheralAddress, TransmissionType.Read);
            AssertInterrupt(IrqBusHold);
            EndTransmission();
        }

        [Test]
        public void InterruptTransmitBufferLevel()
        {
            BeginTransmission(WriteTestPeripheralAddress, TransmissionType.Write);
            // Write test byte
            controller.WriteDoubleWord(0x2C, 0x42);
            EndTransmission();

            // TXBL should have fired
            AssertInterrupt(IrqTxBufferLevel);

            // Write a test byte to the transmit buffer
            controller.WriteDoubleWord(0x2C, 0x42);
            // This should have cleared the TXBL condition
            AssertInterruptCleared(IrqTxBufferLevel);
        }

        [Test]
        public void InterruptTransferCompleted()
        {
            BeginTransmission(WriteTestPeripheralAddress, TransmissionType.Write);
            // Write test byte
            controller.WriteDoubleWord(0x2C, 0x42);
            EndTransmission();

            // TXC should have fired
            AssertInterrupt(IrqTransferCompleted);
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

        // The length of the below sequence needs to be a multiple of 2 for double-width transfer tests to work
        private static readonly byte[] testMagicSequence = { 0x11, 0x22, 0x33, 0x44, 0xFF, 0xEE, 0xDD, 0xCC, 0x11, 0x22, 0x33, 0x44, 0xFF, 0xEE, 0xDD, 0xCC };

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
            foreach(var @byte in response)
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
                Assert.AreEqual(IrqMasterStop, ReadStatus() & IrqMasterStop);
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

        private IMachine machine;
        private EFR32_I2CController controller;
        private EchoI2CDevice writeTestPeripheral;
        private DummyI2CSlave readTestPeripheral;
        private const int ReadTestPeripheralAddress = 0x10;
        private const int WriteTestPeripheralAddress = 0x20;
        private const int IrqRxUnderflow = 1 << 13;
        private const int IrqMasterStop = 1 << 8;
        private const int IrqStart = 1 << 0;
        private const int IrqAck = 1 << 6;
        private const int IrqNack = 1 << 7;
        private const int IrqBusHold = 1 << 11;
        private const int IrqTxBufferLevel = 1 << 4;
        private const int IrqTransferCompleted = 1 << 3;

        private enum TransmissionType
        {
            Read,
            Write
        }
    }
}