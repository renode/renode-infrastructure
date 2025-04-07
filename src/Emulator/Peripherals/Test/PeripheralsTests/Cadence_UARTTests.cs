//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class Cadence_UARTTests
    {
        [OneTimeSetUp]
        public void CreatePeripheral()
        {
            machine = new Machine();
        }

        [Test]
        public void ShouldClearInterruptStatusOnWriteOne()
        {
            var uart = new Cadence_UART(machine, clearInterruptStatusOnRead: false);
            uart.Reset();
            EnableRx(uart);

            Assert.AreEqual(FlagsInitial, ReadInterruptStatus(uart));

            uart.WriteChar(0);
            Assert.AreEqual(FlagsAfterCharWrite, ReadInterruptStatus(uart));
            Assert.AreEqual(FlagsAfterCharWrite, ReadInterruptStatus(uart));

            WriteInterruptStatus(uart, FlagsAfterCharWrite);
            Assert.AreEqual(InterruptFlag.TxFifoEmpty, ReadInterruptStatus(uart));
        }

        [Test]
        public void ShouldClearInterruptStatusOnRead()
        {
            var uart = new Cadence_UART(machine, clearInterruptStatusOnRead: true);
            uart.Reset();
            EnableRx(uart);

            Assert.AreEqual(FlagsInitial, ReadInterruptStatus(uart));

            uart.WriteChar(0);
            Assert.AreEqual(FlagsAfterCharWrite, ReadInterruptStatus(uart));
            Assert.AreEqual(InterruptFlag.TxFifoEmpty, ReadInterruptStatus(uart));
        }

        [Test]
        public void ShouldGenerateTxAlmostFullInterruptWhenTxDisabled()
        {
            var uart = new Cadence_UART(machine, clearInterruptStatusOnRead: true, fifoCapacity: TxFifoCapacity);
            uart.Reset();

            for(var i = 0; i < TxFifoCapacity - 2; ++i)
            {
                uart.TransmitCharacter((byte)0);
            }

            Assert.False(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoNearlyFull));

            uart.TransmitCharacter((byte)0);
            Assert.True(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoNearlyFull));
        }

        [Test]
        public void ShouldGenerateTxFullInterruptWhenTxDisabled()
        {
            var uart = new Cadence_UART(machine, clearInterruptStatusOnRead: true, fifoCapacity: TxFifoCapacity);
            uart.Reset();

            for(var i = 0; i < TxFifoCapacity - 1; ++i)
            {
                uart.TransmitCharacter((byte)0);
            }

            Assert.False(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoFull));

            uart.TransmitCharacter((byte)0);
            Assert.True(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoFull));
        }

        [Test]
        public void ShouldGenerateTxOverflowInterruptWhenTxDisabled()
        {
            var uart = new Cadence_UART(machine, clearInterruptStatusOnRead: true, fifoCapacity: TxFifoCapacity);
            uart.Reset();

            for(var i = 0; i < TxFifoCapacity; ++i)
            {
                uart.TransmitCharacter((byte)0);
            }

            Assert.False(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoOverflow));

            uart.TransmitCharacter((byte)0);
            Assert.True(ReadInterruptStatus(uart).HasFlag(InterruptFlag.TxFifoOverflow));
        }

        private void EnableRx(Cadence_UART uart)
        {
            uart.WriteDoubleWord((long)Registers.Control, FlagEnableRx);
        }

        private void WriteInterruptStatus(Cadence_UART uart, InterruptFlag statusFlags)
        {
            uart.WriteDoubleWord((long)Registers.ChannelInterruptStatus, (uint)statusFlags);
        }

        private InterruptFlag ReadInterruptStatus(Cadence_UART uart)
        {
            return (InterruptFlag)uart.ReadDoubleWord((long)Registers.ChannelInterruptStatus);
        }

        private IMachine machine;

        private const int TxFifoCapacity = 64;
        private const uint FlagEnableRx = 1 << 2;
        private const InterruptFlag FlagsInitial = InterruptFlag.TxFifoEmpty | InterruptFlag.RxFifoEmpty;
        private const InterruptFlag FlagsAfterCharWrite = InterruptFlag.RxTimeoutError | FlagsInitial;

        [Flags]
        private enum InterruptFlag : uint
        {
            TxFifoOverflow = 1 << 12,
            TxFifoNearlyFull = 1 << 11,
            RxTimeoutError = 1 << 8,
            TxFifoFull = 1 << 4,
            TxFifoEmpty = 1 << 3,
            RxFifoEmpty = 1 << 1
        }

        private enum Registers
        {
            Control = 0x00,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0c,
            InterruptMask = 0x10,
            ChannelInterruptStatus = 0x14
        }
    }
}