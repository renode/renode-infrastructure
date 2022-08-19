//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensors;
using NUnit.Framework;
using I2C = Antmicro.Renode.Peripherals.I2C.OpenTitan_I2C;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class OpenTitan_I2C_Test
    {
        [SetUp]
        public void Setup()
        {
            this.machine = new Machine();
            this.sensor1 = new BMP180();
            this.sensor2 = new PAC1934();
            this.peripheral = new OpenTitan_I2C(machine);
            machine.SystemBus.Register(peripheral, new Peripherals.Bus.BusRangeRegistration(new Range(0x1000, 0x1000)));
            peripheral.Register(sensor1, new NumberRegistrationPoint<int>(Sensor1Address));
            peripheral.Register(sensor2, new NumberRegistrationPoint<int>(Sensor2Address));
        }

        [Test]
        public void ShouldPerformSimpleTransaction()
        {
            EnableHost();
            Assert.AreEqual(Sensor1Id, PerformReadFromSlave(Sensor1Address, Sensor1IdOffset), "Incorrect ID for sensor");
        }

        [Test]
        public void ShouldBeAbleToDoTwoConsecutiveTransmissions()
        {
            EnableHost();
            Assert.AreEqual(Sensor1Id, PerformReadFromSlave(Sensor1Address, Sensor1IdOffset), "Incorrect ID for sensor1");
            Assert.AreEqual(Sensor2Id, PerformReadFromSlave(Sensor2Address, Sensor2IdOffset), "Incorrect ID for sensor2");
        }

        [Test]
        public void ShouldNotStartTransactionsUntilHostIsEnabled()
        {
            Assert.AreEqual(0u, PerformReadFromSlave(Sensor1Address, Sensor1IdOffset), "Read some non-zero data");
            // Assert that the fmt queue is not empty
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.Status) & 0x04, "Fmt queue empty");
            // Assert that the rx queue is empty
            Assert.AreEqual(RxQueueEmptyMask, ReadFromRegister(I2C.Registers.Status) & RxQueueEmptyMask, "Rx queue not empty");

            EnableHost();
            // Assert that the rx queue is not empty
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.Status) & RxQueueEmptyMask, "Rx queue empty");
            Assert.AreEqual(Sensor1Id, ReadFromRegister(I2C.Registers.ReadData), "Incorrect data read");
        }

        [Test]
        public void ShouldSetExceptionOnFmtWatermark()
        {
            // Enable the fmt overflow interrupt
            WriteToRegister(I2C.Registers.InterruptEnable, 0x1);
            // Set format fmt watermark level to 4
            WriteToRegister(I2C.Registers.FifoControl, 1 << 5);
            var command = new I2C.FormatIndicator(data: Sensor1Address, start: true);
            for(var i = 0; i < 3; i++)
            {
                EnqueueCommand(command);
            }
            // Interrupt not set yet
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.InterruptState), "Expected no interrupts at this point");
            EnqueueCommand(command);
            // Interrupt set
            Assert.AreEqual(FmtWatermarkInterruptMask, ReadFromRegister(I2C.Registers.InterruptState) & FmtWatermarkInterruptMask, "Interrupt not set");
            Assert.AreEqual(true, peripheral.FormatWatermarkIRQ.IsSet, "IRQ not set");
        }

        [Test]
        public void ShouldSetExceptionOnRxWatermark()
        {
            // Enable the rx overflow interrupt
            WriteToRegister(I2C.Registers.InterruptEnable, 0x2);

            EnableHost();
            EnqueueCommand(new I2C.FormatIndicator(data: Sensor1Address, start: true));
            // Select read address
            EnqueueCommand(new I2C.FormatIndicator(data: Sensor1IdOffset));
            // Interrupt not set yet
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.InterruptState) & RxWatermarkInterruptMask, "Expected no interrupt at this point");
            EnqueueCommand(new I2C.FormatIndicator(data: 1, read: true, stop: true));
            // Interrupt set
            Assert.AreEqual(RxWatermarkInterruptMask, ReadFromRegister(I2C.Registers.InterruptState) & RxWatermarkInterruptMask, "Interrupt not set");
            Assert.AreEqual(true, peripheral.RxWatermarkIRQ.IsSet, "IRQ not set");
        }

        [Test]
        public void ShouldSetExceptionOnOverflow()
        {
            // Enable the fmt overflow interrupt
            WriteToRegister(I2C.Registers.InterruptEnable, 0x1 << 2);
            var command = new I2C.FormatIndicator(data: Sensor1Address, start: true);
            for(var i = 0; i < 64; i++)
            {
                EnqueueCommand(command);
            }
            // Interrupt not set yet
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.InterruptState) & FmtOverflowInterruptMask, "Expected no interrupt at this point");
            EnqueueCommand(command);
            // Interrupt set
            Assert.AreEqual(FmtOverflowInterruptMask, ReadFromRegister(I2C.Registers.InterruptState) & FmtOverflowInterruptMask, "Interrupt not set");
            Assert.AreEqual(true, peripheral.FormatOverflowIRQ.IsSet, "IRQ not set");
        }

        [Test]
        public void ShouldBeAbleToResetFmtFifo()
        {
            EnqueueReadFromSlave(Sensor1Address, Sensor1IdOffset);

            // Assert that the format fifo is not empty
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.Status) & FmtFifoEmptyMask);
            // Write one to the FMTRST
            WriteToRegister(I2C.Registers.FifoControl, 0x01 << 1);
            // Assert that the format fifo is not empty
            Assert.AreEqual(FmtFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & FmtFifoEmptyMask);
        }

        [Test]
        public void ShouldBeAbleToResetRxFifo()
        {
            EnableHost();
            EnqueueReadFromSlave(Sensor1Address, Sensor1IdOffset);

            // Assert that the rx queue is not empty
            Assert.AreEqual(0u, ReadFromRegister(I2C.Registers.Status) & RxQueueEmptyMask, "RxQueue Empty");
            // Clear Rx Fifo
            WriteToRegister(I2C.Registers.FifoControl, 0x01 << 0);
            Assert.AreEqual(RxQueueEmptyMask, ReadFromRegister(I2C.Registers.Status) & RxQueueEmptyMask);
        }

        private byte PerformReadFromSlave(byte address, byte offset)
        {
            EnqueueReadFromSlave(address, offset);
            return (byte)ReadFromRegister(I2C.Registers.ReadData);
        }

        private void EnqueueReadFromSlave(byte address, byte offset)
        {
            // Select slave address
            EnqueueCommand(new I2C.FormatIndicator(data: address, start: true));
            // Select read address
            EnqueueCommand(new I2C.FormatIndicator(data: offset));
            // Read one byte from selected address
            EnqueueCommand(new I2C.FormatIndicator(data: 1, read: true, stop: true));
        }

        private void EnableHost()
        {
            WriteToRegister(I2C.Registers.Control, 0x1);
        }

        private void EnqueueCommand(I2C.FormatIndicator command)
        {
            WriteToRegister(I2C.Registers.FormatData, command.ToRegisterFormat());
        }

        private void WriteToRegister(I2C.Registers register, uint value)
        {
            peripheral.WriteDoubleWord((long)register, value);
        }

        private uint ReadFromRegister(I2C.Registers register)
        {
            return peripheral.ReadDoubleWord((long)register);
        }

        private Machine machine;
        private II2CPeripheral sensor1;
        private II2CPeripheral sensor2;
        private OpenTitan_I2C peripheral;

        private const byte Sensor1Address = 0x77;
        private const byte Sensor1Id = 0x55;
        private const byte Sensor1IdOffset = 0xD0;
        private const byte Sensor2Address = 0x78;
        private const byte Sensor2Id = 0x5B;
        private const byte Sensor2IdOffset = 0xFD;

        private const uint FmtFifoEmptyMask = 0x04;
        private const uint FmtOverflowInterruptMask = 0x4;
        private const uint FmtWatermarkInterruptMask = 0x1;
        private const uint RxQueueEmptyMask = 0x20;
        private const uint RxWatermarkInterruptMask = 0x2;
    }
}
