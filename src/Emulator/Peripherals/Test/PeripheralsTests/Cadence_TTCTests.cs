//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class Cadence_TTCTests
    {
        [OneTimeSetUp]
        public void CreatePeripheral()
        {
            machine = new Machine();
            timer = new Cadence_TTC(machine, TimerFrequency);
        }

        [SetUp]
        public void PreparePeripheral()
        {
            timer.Reset();
        }

        [Test]
        public void ShouldResetCounter()
        {
            var index = 0;
            timer.SetCounterValue(0, TicksPerSecond);
            WriteTimerRegister(index, Registers.InterruptEnable, InterruptMaskAll);
            Assert.AreEqual(TicksPerSecond, ReadTimerRegister(index, Registers.CounterValue));
            Assert.False(timer.Connections[0].IsSet);

            WriteTimerRegister(index, Registers.CounterControl, CounterControlResetFlag);
            Assert.AreEqual(0, ReadTimerRegister(index, Registers.CounterValue));
            Assert.False(timer.Connections[0].IsSet);
        }

        [Test]
        public void ShouldCountIndependently()
        {
            WriteTimerRegister(0, Registers.ClockControl, ClockControlPrescaler1024);
            WriteTimerRegister(1, Registers.ClockControl, ClockControlPrescaler2048);
            WriteTimerRegister(2, Registers.ClockControl, ClockControlPrescaler4096);
            for(var index = 0; index < TimersCount; index++)
            {
                WriteTimerRegister(index, Registers.CounterControl, CounterControlCountUpOverflow);
                Assert.AreEqual(0, ReadTimerRegister(index, Registers.CounterValue));
            }
            AdvanceBySeconds(1);

            Assert.AreEqual(TicksPerSecond / 1, ReadTimerRegister(0, Registers.CounterValue));
            Assert.AreEqual(TicksPerSecond / 2, ReadTimerRegister(1, Registers.CounterValue));
            Assert.AreEqual(TicksPerSecond / 4, ReadTimerRegister(2, Registers.CounterValue));
        }

        [TestCase(CounterControlCountUpOverflow, InterruptMaskOverflow, UInt32.MaxValue - TicksPerSecond - 1, 0u, 0u,
            TestName = "ShouldCountUpInOverflowMode")]
        [TestCase(CounterControlCountDownOverflow, InterruptMaskOverflow, TicksPerSecond + 1, UInt32.MaxValue, 0u,
            TestName = "ShouldCountDownInOverflowMode")]
        [TestCase(CounterControlCountUpInterval, InterruptMaskInterval, 2 * TicksPerSecond - 1, 0u, 3 * TicksPerSecond,
            TestName = "ShouldCountUpInIntervalMode")]
        [TestCase(CounterControlCountDownInterval, InterruptMaskInterval, TicksPerSecond + 1, 3 * TicksPerSecond, 3 * TicksPerSecond,
            TestName = "ShouldCountDownInIntervalMode")]
        public void ShouldOverflowInOneSecond(uint counterControl, uint interruptMask, uint initValue, uint overflowValue, uint interval = 0)
        {
            var index = 0;
            WriteTimerRegister(index, Registers.ClockControl, ClockControlPrescaler);
            WriteTimerRegister(index, Registers.CounterControl, counterControl);
            WriteTimerRegister(index, Registers.CounterInterval, interval);
            WriteTimerRegister(index, Registers.InterruptEnable, interruptMask);
            ReadTimerRegister(index, Registers.InterruptStatus);

            timer.SetCounterValue(0, initValue);
            AdvanceBySeconds(1);

            Assert.False(timer.Connections[index].IsSet);
            AdvanceBySeconds(1 / (double)TicksPerSecond);

            Assert.AreEqual(overflowValue, ReadTimerRegister(index, Registers.CounterValue));
            Assert.True(timer.Connections[index].IsSet);

            Assert.AreEqual(interruptMask, ReadTimerRegister(index, Registers.InterruptStatus));
            Assert.False(timer.Connections[index].IsSet);
        }

        private void AdvanceBySeconds(double seconds)
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromSeconds(seconds));
        }

        private void WriteTimerRegister(int index, Registers register, uint value)
        {
            timer.WriteDoubleWord(index * RegisterSize + (long)register, value);
        }

        private uint ReadTimerRegister(int index, Registers register)
        {
            return timer.ReadDoubleWord(index * RegisterSize + (long)register);
        }

        private Cadence_TTC timer;
        private IMachine machine;

        private const int TimersCount = 3;
        private const int RegisterSize = 0x4;

        private const uint TicksPerSecond = 0x100;
        private const uint ClockControlPrescaler1024 = (9 << 1) | 0x1;
        private const uint ClockControlPrescaler2048 = (10 << 1) | 0x1;
        private const uint ClockControlPrescaler4096 = (11 << 1) | 0x1;
        private const uint ClockControlPrescaler = ClockControlPrescaler1024;
        private const uint TimerFrequency = TicksPerSecond * 1024;
        private const double TimeDelta = 0.1;

        private const uint CounterControlResetFlag = 0x10;
        private const uint CounterControlCountUpOverflow = 0x0;
        private const uint CounterControlCountUpInterval = 0x02;
        private const uint CounterControlCountDownOverflow = 0x4;
        private const uint CounterControlCountDownInterval = 0x4 | 0x2;
        private const uint InterruptMaskOverflow = 0x10;
        private const uint InterruptMaskInterval = 0x1;
        private const uint InterruptMaskAll = 0xff;

        private enum Registers : long
        {
            ClockControl = 0x00,
            CounterControl = 0x0C,
            CounterValue = 0x18,
            CounterInterval = 0x24,
            Match1Counter = 0x30,
            Match2Counter = 0x3C,
            Match3Counter = 0x48,
            InterruptStatus = 0x54,
            InterruptEnable = 0x60,
            EventControlTimer = 0x6C,
            EventRegister = 0x78,
        }
    }
}
