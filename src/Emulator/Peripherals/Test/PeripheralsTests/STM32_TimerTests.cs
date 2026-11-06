//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    // RM0468 (STM32H72x/73x) 44.3.2 "Counter modes": counting up, the counter counts from 0 to the
    // auto-reload value (TIMx_ARR), then restarts from 0 and generates an overflow event; counting
    // down, from ARR to 0, then restarts from ARR and generates an underflow event. The counter clock
    // is the timer clock / (PSC + 1) (44.4.14), so the update period is (ARR + 1) * (PSC + 1) clocks.
    [TestFixture]
    public class STM32_TimerTests
    {
        [OneTimeSetUp]
        public void CreatePeripheral()
        {
            machine = new Machine();
            // One count per microsecond with PSC = 0
            timer = new STM32_Timer(machine, 1000000, 0xFFFF);
        }

        [SetUp]
        public void PreparePeripheral()
        {
            timer.Reset();
        }

        [Test]
        public void ShouldRaiseUpdateEventEveryArrPlusOneCounts()
        {
            Start(prescaler: 0, autoReload: 99);

            AdvanceByMicroseconds(99);
            Assert.AreEqual(99, Read(Registers.Counter));
            Assert.AreEqual(0, Read(Registers.Status) & UpdateInterruptFlag);

            AdvanceByMicroseconds(1);
            Assert.AreEqual(0, Read(Registers.Counter));
            Assert.AreEqual(UpdateInterruptFlag, Read(Registers.Status) & UpdateInterruptFlag);

            for(var i = 0; i < 3; i++)
            {
                Write(Registers.Status, 0);
                AdvanceByMicroseconds(99);
                Assert.AreEqual(0, Read(Registers.Status) & UpdateInterruptFlag);
                AdvanceByMicroseconds(1);
                Assert.AreEqual(UpdateInterruptFlag, Read(Registers.Status) & UpdateInterruptFlag);
            }
        }

        [Test]
        public void ShouldDivideTheCounterClockByPrescalerPlusOne()
        {
            // Two microseconds per count, 50 counts: 100 us
            Start(prescaler: 1, autoReload: 49);

            AdvanceByMicroseconds(99);
            Assert.AreEqual(49, Read(Registers.Counter));
            Assert.AreEqual(0, Read(Registers.Status) & UpdateInterruptFlag);

            AdvanceByMicroseconds(1);
            Assert.AreEqual(0, Read(Registers.Counter));
            Assert.AreEqual(UpdateInterruptFlag, Read(Registers.Status) & UpdateInterruptFlag);
        }

        [Test]
        public void ShouldUnderflowAfterZeroWhenCountingDown()
        {
            Start(prescaler: 0, autoReload: 99, down: true);
            Assert.AreEqual(99, Read(Registers.Counter));

            AdvanceByMicroseconds(99);
            Assert.AreEqual(0, Read(Registers.Counter));
            Assert.AreEqual(0, Read(Registers.Status) & UpdateInterruptFlag);

            AdvanceByMicroseconds(1);
            Assert.AreEqual(99, Read(Registers.Counter));
            Assert.AreEqual(UpdateInterruptFlag, Read(Registers.Status) & UpdateInterruptFlag);
        }

        [Test]
        public void ShouldKeepAPwmPeriodOfArrPlusOneCounts()
        {
            // PWM mode 1 on channel 1 (44.3.9): high while CNT < CCR1, so high for CCR1 counts of ARR + 1
            Write(Registers.CaptureOrCompareMode1, PwmMode1 << 4);
            Write(Registers.CaptureOrCompareEnable, 1);
            Write(Registers.CaptureOrCompare1, 25);
            Start(prescaler: 0, autoReload: 99);

            // The first update event, at 100 us, starts a period
            AdvanceByMicroseconds(100);
            Assert.True(timer.Connections[0].IsSet);
            AdvanceByMicroseconds(24);
            Assert.True(timer.Connections[0].IsSet);
            AdvanceByMicroseconds(1);
            Assert.False(timer.Connections[0].IsSet);
            AdvanceByMicroseconds(74);
            Assert.False(timer.Connections[0].IsSet);
            AdvanceByMicroseconds(1);
            Assert.True(timer.Connections[0].IsSet);
        }

        private void Start(uint prescaler, uint autoReload, bool down = false)
        {
            var direction = down ? CountDown : 0u;
            Write(Registers.Prescaler, prescaler);
            Write(Registers.AutoReload, autoReload);
            Write(Registers.Control1, direction);
            Write(Registers.EventGeneration, UpdateGeneration);
            Write(Registers.Status, 0);
            Write(Registers.Control1, direction | CounterEnable);
        }

        private void AdvanceByMicroseconds(ulong microseconds)
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMicroseconds(microseconds));
        }

        private void Write(Registers register, uint value)
        {
            timer.WriteDoubleWord((long)register, value);
        }

        private uint Read(Registers register)
        {
            return timer.ReadDoubleWord((long)register);
        }

        private IMachine machine;
        private STM32_Timer timer;

        private const uint CounterEnable = 1 << 0;
        private const uint CountDown = 1 << 4;
        private const uint UpdateInterruptFlag = 1 << 0;
        private const uint UpdateGeneration = 1 << 0;
        private const uint PwmMode1 = 6;

        private enum Registers : long
        {
            Control1 = 0x0,
            Status = 0x10,
            EventGeneration = 0x14,
            CaptureOrCompareMode1 = 0x18,
            CaptureOrCompareEnable = 0x20,
            Counter = 0x24,
            Prescaler = 0x28,
            AutoReload = 0x2C,
            CaptureOrCompare1 = 0x34,
        }
    }
}
