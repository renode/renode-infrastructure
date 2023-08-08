//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Extensions.Mocks;
using NUnit.Framework;
using SPI = Antmicro.Renode.Peripherals.SPI.OpenTitan_SpiDevice;
using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class OpenTitan_SpiDeviceTest
    {
        [SetUp]
        public void Setup()
        {
            machine = new Machine();
            peripheral = new SPI(machine);
            spiHost = new SpiHost(peripheral);
            hostMachine = new HostMachine();
            hostMachine.AddHostMachineElement(spiHost, "spiHostMock");
            machine.SystemBus.Register(peripheral, new Peripherals.Bus.BusRangeRegistration(new Range(PeripheralRegistrationPoint, 0x2000)));
        }

        [Test]
        public void ShouldHaveCorrectFifoConfigurationAfterBoot()
        {
            SetGenericMode(clockEnable: false);
            ReadFifoAddresses(out var readBase, out var readLimit, out var writeBase, out var writeLimit);
            Assert.AreEqual(0, readBase, "Incorrect readBase");
            Assert.AreEqual(2047, readLimit, "Incorrect readLimit");
            Assert.AreEqual(2048, writeBase, "Incorrect writeBase");
            Assert.AreEqual(4095, writeLimit, "Incorrect writeLimit");
        }

        [Test]
        public void ShouldReceiveDataToFifo()
        {
            var fifoPointerResponse = ReadFromPeripheral(SPI.Registers.ReceiverFifoSramPointers);
            Assert.AreEqual(0, fifoPointerResponse);
            SetGenericMode(clockEnable: true);
            spiHost.WriteBytes("deadbeef");

            SplitValIntoWords(ReadFromPeripheral(SPI.Registers.ReceiverFifoSramPointers), out _, out var rxFifoPointer);
            Assert.AreEqual(4, rxFifoPointer, "Incorrect fifo pointer");

            var fifoLevelResponse = ReadFromPeripheral(SPI.Registers.AsyncFifoLevel);
            Assert.AreEqual(4, fifoLevelResponse, "Incorrect fifo level");
        }

        [Test]
        public void ShouldBeAbleToTransmitData()
        {
            var hexstring = "deadbeef";
            AppendPeripheralFifo(hexstring);

            SplitValIntoWords(ReadFromPeripheral(SPI.Registers.AsyncFifoLevel), out _, out var fifoLevel);
            Assert.AreEqual(4, fifoLevel);
            var output = spiHost.ReadBytes(4);
            Assert.AreEqual(new byte[] { 0xde, 0xad, 0xbe, 0xef }, output);
        }

        [Test]
        public void ShouldBeAbleToResetFifos()
        {
            var hexstring = "deadbeef";

            spiHost.WriteBytes(hexstring);
            AppendPeripheralFifo(hexstring);

            SplitValIntoWords(ReadFromPeripheral(SPI.Registers.AsyncFifoLevel), out var txPointer, out var rxPointer);
            Assert.AreEqual(4, txPointer, "Incorrect tx fifo pointer");
            Assert.AreEqual(4, rxPointer, "Incorrect rx fifo pointer");

            this.WriteToPeripheral(SPI.Registers.Control, 3 << 16); // reset tx and rx fifo
            SplitValIntoWords(ReadFromPeripheral(SPI.Registers.AsyncFifoLevel), out txPointer, out rxPointer);
            Assert.AreEqual(0, txPointer, "Failed to reset the txFifo");
            Assert.AreEqual(0, rxPointer, "Failed to reset the rxFifo");
        }

        [Test]
        public void ShouldSetInterruptOnRxFifoFull()
        {
            // Enable this interrupt
            WriteToPeripheral(SPI.Registers.InterruptEnable, RxFullInterruptMask);
            for(var i = 0; i < (MaxFifoCapacity - 1); i++)
            {
                spiHost.WriteBytes("FF");
            }
            Assert.False(peripheral.GenericRxFull.IsSet, "Interrupt should not be set yet");
            spiHost.WriteBytes("FF");
            Assert.True(peripheral.GenericRxFull.IsSet, "Interrupt not set when expected");
        }

        [Test]
        public void ShouldSetInterruptOnRxOverflow()
        {
            // Enable this interrupt
            WriteToPeripheral(SPI.Registers.InterruptEnable, RxFifoOverflowMask);
            // by default the fifo can fit 2048 elements
            for(var i = 0; i < MaxFifoCapacity; i++)
            {
                spiHost.WriteBytes("FF");
            }
            Assert.False(peripheral.GenericRxOverflow.IsSet, "Overflow happend too fast");
            spiHost.WriteBytes("FF");
            Assert.True(peripheral.GenericRxOverflow.IsSet, "Interrupt not set when expected");
        }

        [Test]
        public void ShouldSetInterruptOnRxFifoWatermark()
        {
            // Enable this interrupt
            WriteToPeripheral(SPI.Registers.InterruptEnable, RxWatermarkInterruptMask);
            for(var i = 0; i < 0x80; i++)
            {
                spiHost.WriteBytes("FF");
            }
            Assert.False(peripheral.GenericRxWatermark.IsSet, "Interrupt should not be set yet");
            spiHost.WriteBytes("FF");
            Assert.True(peripheral.GenericRxWatermark.IsSet, "Interrupt not set when expected");
        }

        [Test]
        public void ShouldSetInterruptOnTxFifoWatermark()
        {
            // Enable this interrupt
            WriteToPeripheral(SPI.Registers.InterruptEnable, TxWatermarkInterruptMask);
            // Set the rxWatermark
            WriteToPeripheral(SPI.Registers.FifoLevel, 0x80 << 16);
            WriteToPeripheral(SPI.Registers.TransmitterFifoSramPointers, 0x80 << 16);

            SplitValIntoWords(ReadFromPeripheral(SPI.Registers.AsyncFifoLevel), out _, out var txFifoLevel);
            Assert.AreEqual(0x80, txFifoLevel, "Wrong fifo level");
            Assert.False(peripheral.GenericTxWatermark.IsSet, "Interrupt should not be set yet");
            peripheral.Transmit(0);
            Assert.True(peripheral.GenericTxWatermark.IsSet, "Interrupt not set when expected");
        }

        [Test]
        public void ShouldSetInterruptOnTxUnderflow()
        {
            // Enable this interrupt
            WriteToPeripheral(SPI.Registers.InterruptEnable, TxFifoUnderflowMask);
            peripheral.Transmit(0);
            Assert.True(peripheral.GenericTxUnderflow.IsSet, "Interrupt not set when expected");
        }

        private void SetGenericMode(bool clockEnable)
        {
            var register = SPI.Registers.Control;
            var valueToWrite = (0x0 << 4) | ((clockEnable ? 1 : 0) << 31);
            WriteToPeripheral(register, (uint)valueToWrite);
        }

        private void WriteToPeripheralTransmitFifo(byte[] bytes)
        {
            // software is responsible for handling the pointers
            ReadFifoAddresses(out _, out _, out var transmitWriteBase, out var transmitWriteLimit);
            ReadFifoPointers(out _, out _, out _, out var transmitWritePointer);
            var writeAddress = (ulong)SPI.Registers.Buffer + transmitWriteBase + transmitWritePointer;
            foreach(var b in bytes)
            {
                machine.SystemBus.WriteByte(PeripheralRegistrationPoint + writeAddress, b);
                writeAddress += 1;
                if(writeAddress == transmitWriteLimit)
                {
                    writeAddress = transmitWriteBase;
                }
            }
            IncrementWritePointerBy(bytes.Length);
        }

        private void IncrementWritePointerBy(int length)
        {
            ReadFifoPointers(out _, out _, out var txReadPointer, out var txWritePointer);
            txWritePointer += (uint)length;
            WriteTxFifoPointers(txReadPointer, txWritePointer);
        }

        private void AppendPeripheralFifo(string hexstring)
        {
            var dataToSend = Misc.HexStringToByteArray(hexstring);

            WriteToPeripheralTransmitFifo(dataToSend);
        }

        private void WriteTxFifoPointers(uint txReadPointer, uint txWritePointer)
        {
            WriteToPeripheral(SPI.Registers.TransmitterFifoSramPointers, txReadPointer | (txWritePointer << 16));
        }

        private void WriteToPeripheral(SPI.Registers register, uint value)
        {
            machine.SystemBus.WriteDoubleWord(PeripheralRegistrationPoint + (ulong)register, value);
        }

        private void WriteToPeripheral(SPI.Registers register, byte value)
        {
            machine.SystemBus.WriteByte(PeripheralRegistrationPoint + (ulong)register, value);
        }

        private uint ReadFromPeripheral(SPI.Registers register)
        {
            return machine.SystemBus.ReadDoubleWord(PeripheralRegistrationPoint + (ulong)register);
        }

        private void ReadFifoAddresses(out uint readBase, out uint readLimit, out uint writeBase, out uint writeLimit)
        {
            var rawReceiverVal = ReadFromPeripheral(SPI.Registers.ReceiverFifoSramAddresses);
            var rawTransmitterVal = ReadFromPeripheral(SPI.Registers.TransmitterFifoSramAddresses);

            SplitValIntoWords(rawReceiverVal, out var baseRaw, out var limitRaw);
            readBase = baseRaw * 4;
            // weird conversion as this is the last doubleword addr
            readLimit = (limitRaw + 4) * 4 - 1;

            SplitValIntoWords(rawTransmitterVal, out baseRaw, out limitRaw);
            writeBase = baseRaw * 4;
            // weird conversion as this is the last doubleword addr
            writeLimit = (limitRaw + 4) * 4 - 1;
        }

        private void ReadFifoPointers(out uint receiveReadPointer, out uint receiveWritePointer, out uint transmitReadPointer, out uint transmitWritePointer)
        {
            var receiverRawPointers = ReadFromPeripheral(SPI.Registers.ReceiverFifoSramPointers);
            var transmitRawPointers = ReadFromPeripheral(SPI.Registers.TransmitterFifoSramPointers);

            SplitValIntoWords(receiverRawPointers, out receiveReadPointer, out receiveWritePointer);
            SplitValIntoWords(transmitRawPointers, out transmitReadPointer, out transmitWritePointer);
        }

        private void SplitValIntoWords(uint value, out uint firstWord, out uint secondWord)
        {
            firstWord = value & 0xFFFF;
            secondWord = value >> 16;
        }

        private IMachine machine;
        private SPI peripheral;
        private SpiHost spiHost;
        private HostMachine hostMachine;

        private const uint PeripheralRegistrationPoint = 0x1000;
        private const uint RxFullInterruptMask      = 1u << 0;
        private const uint RxWatermarkInterruptMask = 1u << 1;
        private const uint TxWatermarkInterruptMask = 1u << 2;
        private const uint RxFifoOverflowMask       = 1u << 4;
        private const uint TxFifoUnderflowMask      = 1u << 5;
        private const uint MaxFifoCapacity = 2048;
    }
}
