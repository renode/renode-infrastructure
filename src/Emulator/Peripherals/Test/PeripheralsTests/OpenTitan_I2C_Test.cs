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

        [Test]
        public void ShouldNotAllowEnablingBothModesAtTheSameTime()
        {
            EnableTarget();
            EnableHost();
            Assert.AreEqual(1u, ReadFromRegister(I2C.Registers.Control));
        }

        [Test]
        public void ShouldNotAcceptCommandsWhenInTargetMode()
        {
            EnableTarget();
            EnqueueCommand(new I2C.FormatIndicator(data: Sensor1Address, start: true));
            Assert.AreEqual(FmtFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & FmtFifoEmptyMask);
        }

        [Test]
        public void ShouldQueueTransmitBytesWhenInTargetMode()
        {
            EnableTarget();
            EnqueueTransmission(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            Assert.AreEqual(9u, GetTxFifoLevel());
        }

        [Test]
        public void ShouldNotQueueTransmissionWhenInHostMode()
        {
            EnableHost();
            EnqueueTransmission(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            Assert.AreEqual(0u, GetTxFifoLevel());
        }

        [Test]
        public void TargetShouldInterpretTransmissionCompleteAsStop()
        {
            EnableTarget();
            peripheral.FinishTransmission();
            Assert.AreEqual(new I2C.AcquireFormatIndicator(0x0, false, true),
                            I2C.AcquireFormatIndicator.FromRegister(ReadFromRegister(I2C.Registers.AcquiredData)),
                            "Expected stop flag");
        }

        [Test]
        public void TargetShouldCorrectlyAssignRWflag()
        {
            EnableTarget();
            var someAddr = 0x7Fu;
            var readBit = 0x1u;
            peripheral.Write(new byte[] { (byte)((someAddr << 1) | readBit) });
            peripheral.FinishTransmission();
            readBit = 0x0u;
            peripheral.Write(new byte[] { (byte)((someAddr << 1) | readBit) });
            peripheral.FinishTransmission();
            var acqFormat = I2C.AcquireFormatIndicator.FromRegister(ReadFromRegister(I2C.Registers.AcquiredData));

            Assert.AreEqual((someAddr << 1) | 1u, acqFormat.Data, "Expected correct data in the 1st paket");
            Assert.AreEqual(true, acqFormat.ReadFlag, "Expected correct read flag in the 1st paket");
            Assert.AreEqual(true, acqFormat.StartFlag, "Expected correct start flag in the 1st paket");
            Assert.AreEqual(false, acqFormat.StopFlag, "Expected correct stop flag in the 1st paket");

            acqFormat = I2C.AcquireFormatIndicator.FromRegister(ReadFromRegister(I2C.Registers.AcquiredData));
            Assert.AreEqual(true, acqFormat.StopFlag, "Expected correct stop flag in the 2nd ");

            acqFormat = I2C.AcquireFormatIndicator.FromRegister(ReadFromRegister(I2C.Registers.AcquiredData));
            Assert.AreEqual((someAddr << 1) | 0u, acqFormat.Data, "Expected correct data in the 3rd paket");
            Assert.AreEqual(false, acqFormat.ReadFlag, "Expected correct read flag in the 3rd paket");
            Assert.AreEqual(true, acqFormat.StartFlag, "Expected correct start flag in the 3rd paket");
            Assert.AreEqual(false, acqFormat.StopFlag, "Expected correct stop flag in the 3rd paket");

            acqFormat = I2C.AcquireFormatIndicator.FromRegister(ReadFromRegister(I2C.Registers.AcquiredData));
            Assert.AreEqual(true, acqFormat.StopFlag, "Expected correct stop flag in the 4th packet");
        }

        [Test]
        public void TargetShouldProperlyHandleRead()
        {
            EnableTarget();
            var data = new byte[] { 0x1, 0x2, 0xFF, 0x3, 0x4, 0xFE };
            foreach(var b in data)
            {
                WriteToRegister(I2C.Registers.TransmitData, b);
            }
            var readData = peripheral.Read(6);

            Assert.AreEqual(6, readData.Length, "Received data length mismatch");
            Assert.AreEqual(data, readData, "Received data mismatch");
        }

        [Test]
        public void TargetShouldNotReturnMoreThanQueued()
        {
            EnableTarget();
            WriteToRegister(I2C.Registers.TransmitData, 0x1);
            Assert.AreEqual(1, peripheral.Read(10).Length);
        }

        [Test]
        public void ShouldCorrectlyShowStatusOfTheAcqFifo()
        {
            EnableTarget();
            Assert.AreEqual(AcqFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & AcqFifoEmptyMask, "Fifo not empty after init");
            for(var x = 0; x < 4; x++)
            {
                peripheral.Write(new byte[] { 0x1 });
            }
            Assert.AreEqual(0, ReadFromRegister(I2C.Registers.Status) & AcqFifoEmptyMask, "Fifo empty after peripheral write?");
            Assert.AreEqual(4, ((int)ReadFromRegister(I2C.Registers.FifoStatus) >> (int)AcqFifoLevelOffset) & FifoLevelMask, "Wrong fifo level returned");

            for(var x = 0; x < 60; x++)
            {
                peripheral.Write(new byte[] { 0x1 });
            }
            Assert.AreEqual(AcqFifoFullMask, ReadFromRegister(I2C.Registers.Status) & AcqFifoFullMask, "Fifo not full after 64 writes?");
            Assert.AreEqual(64, ReadFromRegister(I2C.Registers.FifoStatus) >> (int)AcqFifoLevelOffset, "Wrong fifo level returned");
            WriteToRegister(I2C.Registers.FifoControl, AcqFifoResetMask);
            Assert.AreEqual(AcqFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & AcqFifoEmptyMask, "Fifo not empty after reset");
        }

        [Test]
        public void ShouldCorrectlyShowStatusOfTheTxFifo()
        {
            EnableTarget();
            Assert.AreEqual(TxFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & TxFifoEmptyMask, "Fifo not empty after init");
            for(var x = 0; x < 4; x++)
            {
                WriteToRegister(I2C.Registers.TransmitData, 0x1);
            }
            Assert.AreEqual(0, ReadFromRegister(I2C.Registers.Status) & TxFifoEmptyMask, "Fifo empty?");
            Assert.AreEqual(4, ReadFromRegister(I2C.Registers.FifoStatus) >> 8, "Wrong fifo level returned");

            for(var x = 0; x < 60; x++)
            {
                WriteToRegister(I2C.Registers.TransmitData, 0x1);
            }
            Assert.AreEqual(TxFifoFullMask, ReadFromRegister(I2C.Registers.Status) & TxFifoFullMask, "Fifo not full after 64 writes?");
            Assert.AreEqual(64, ReadFromRegister(I2C.Registers.FifoStatus) >> (int)TxFifoLevelOffset, "Wrong fifo level returned");
            WriteToRegister(I2C.Registers.FifoControl, TxFifoResetMask);
            Assert.AreEqual(TxFifoEmptyMask, ReadFromRegister(I2C.Registers.Status) & TxFifoEmptyMask, "Fifo not empty after reset");
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

        private void EnableTarget()
        {
            WriteToRegister(I2C.Registers.Control, 0x2);
        }

        private void EnqueueTransmission(byte[] bytes)
        {
            foreach(var b in bytes)
            {
                WriteToRegister(I2C.Registers.TransmitData, b);
            }
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

        private uint GetTxFifoLevel()
        {
            return (ReadFromRegister(I2C.Registers.FifoStatus) >> 8) & 0x7F;
        }

        private IMachine machine;
        private II2CPeripheral sensor1;
        private II2CPeripheral sensor2;
        private OpenTitan_I2C peripheral;

        private const byte Sensor1Address = 0x77;
        private const byte Sensor1Id = 0x55;
        private const byte Sensor1IdOffset = 0xD0;
        private const byte Sensor2Address = 0x78;
        private const byte Sensor2Id = 0x5B;
        private const byte Sensor2IdOffset = 0xFD;

        private const uint FifoLevelMask = 0x7F;
        private const uint AcqFifoLevelOffset = 24;
        private const uint TxFifoLevelOffset = 8;

        private const uint AcqFifoResetMask = 0x80;
        private const uint TxFifoResetMask = 0x100;

        private const uint AcqFifoEmptyMask = 0x200;
        private const uint AcqFifoFullMask = 0x80;
        private const uint FmtFifoEmptyMask = 0x04;
        private const uint FmtOverflowInterruptMask = 0x4;
        private const uint FmtWatermarkInterruptMask = 0x1;
        private const uint RxQueueEmptyMask = 0x20;
        private const uint RxWatermarkInterruptMask = 0x2;
        private const uint TxFifoEmptyMask = 0x100;
        private const uint TxFifoFullMask = 0x40;
    }
}
