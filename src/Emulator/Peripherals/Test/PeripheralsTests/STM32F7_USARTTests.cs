//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class STM32F7_USARTTests
    {
        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
            uart = new STM32F7_USART(machine, Frequency);
            uart.WriteDoubleWord(BaudRate, Divisor);
            uart.WriteDoubleWord(ControlRegister1, UsartEnable | ReceiverEnable | IdleInterruptEnable);
        }

        [Test]
        public void ShouldReportIdleAfterReceivingCharacter()
        {
            uart.WriteChar(0x41);
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMilliseconds(1));

            Assert.IsTrue((uart.ReadDoubleWord(InterruptAndStatus) & IdleFlag) != 0);
            Assert.IsTrue(uart.IRQ.IsSet);
        }

        [Test]
        public void ShouldClearIdleFlag()
        {
            uart.WriteChar(0x41);
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMilliseconds(1));

            uart.WriteDoubleWord(InterruptFlagClear, IdleFlag);

            Assert.IsFalse((uart.ReadDoubleWord(InterruptAndStatus) & IdleFlag) != 0);
            Assert.IsFalse(uart.IRQ.IsSet);
        }

        private Machine machine;
        private STM32F7_USART uart;

        private const uint Frequency = 8_000_000;
        private const uint Divisor = 8;

        private const long ControlRegister1 = 0x0;
        private const long BaudRate = 0xC;
        private const long InterruptAndStatus = 0x1C;
        private const long InterruptFlagClear = 0x20;

        private const uint UsartEnable = 1u << 0;
        private const uint ReceiverEnable = 1u << 2;
        private const uint IdleInterruptEnable = 1u << 4;
        private const uint IdleFlag = 1u << 4;
    }
}
