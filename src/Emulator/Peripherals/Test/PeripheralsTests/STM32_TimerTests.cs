//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Bus;
using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class STM32_TimerTests
    {
        [SetUp]
        public void Setup()
        {
            machine = new Machine();
            timer = new STM32_Timer(machine, Frequency, 0xFFFF);  // 16-bit timer.
            sysbus = machine.GetSystemBus(timer);
        }

        [Test]
        public void ShouldResetCounter()
        {
            WriteRegister(Registers.Counter, 0x1234);
            Assert.AreEqual(0x1234, ReadRegister(Registers.Counter));
            
            timer.Reset();
            Assert.AreEqual(0, ReadRegister(Registers.Counter));
        }

        [Test]
        public void ShouldCountUp()
        {
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.Prescaler, 0);
            WriteRegister(Registers.Control1, 1);
            
            Assert.AreEqual(0, ReadRegister(Registers.Counter));
            AdvanceByTicks(500);
            Assert.AreEqual(500, ReadRegister(Registers.Counter));
            AdvanceByTicks(500);
            // On overflow, it should wrap (or stop if OPM, but default is periodic)
            Assert.AreEqual(0, ReadRegister(Registers.Counter));
        }

        [Test]
        public void ShouldRespectPrescaler()
        {
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.Prescaler, 9);
            WriteRegister(Registers.Control1, 1);
            
            AdvanceByTicks(100);
            Assert.AreEqual(10, ReadRegister(Registers.Counter));
        }

        [Test]
        public void ShouldRespectRepetitionCounter()
        {
            WriteRegister(Registers.AutoReload, 100);
            WriteRegister(Registers.RepetitionCounter, 2); // Update every 3 overflows
            
            // Generate update event to load RCR into repetitionsLeft
            WriteRegister(Registers.EventGeneration, 1); 
            
            WriteRegister(Registers.Control1, 1);
            
            var receiver = new DummyReceiver();
            timer.UpdateInterrupt.Connect(receiver, 0);
            WriteRegister(Registers.DmaOrInterruptEnable, 1);
            
            // The UG event already set the interrupt flag if UIE was high. 
            // In our case UIE was set AFTER UG, so it's clean (or we should clear it).
            WriteRegister(Registers.Status, 0);
            receiver.Reset();
            
            // 1st overflow
            AdvanceByTicks(99); // Total 99
            Assert.IsFalse(receiver.Received, "Should not update before 1st overflow");
            AdvanceByTicks(2); // Total 101
            Assert.IsFalse(receiver.Received, "Should not update after 1st overflow");
            
            // 2nd overflow
            AdvanceByTicks(98); // Total 199
            Assert.IsFalse(receiver.Received, "Should not update before 2nd overflow");
            AdvanceByTicks(2); // Total 201
            Assert.IsFalse(receiver.Received, "Should not update after 2nd overflow");
            
            // 3rd overflow
            AdvanceByTicks(98); // Total 299
            Assert.IsFalse(receiver.Received, "Should not update before 3rd overflow");
            AdvanceByTicks(2); // Total 301
            Assert.IsTrue(receiver.Received, "Should update after 3rd overflow");
        }

        [Test]
        public void ShouldTriggerTRGOOnEnable()
        {
            WriteRegister(Registers.Control2, 1 << 4); // MMS: Enable
            Assert.IsFalse(timer.TRGO.IsSet);
            
            WriteRegister(Registers.Control1, 1);
            Assert.IsTrue(timer.TRGO.IsSet);
            
            WriteRegister(Registers.Control1, 0);
            Assert.IsFalse(timer.TRGO.IsSet);
        }

        [Test]
        public void ShouldTriggerTRGOOnUpdatePulse()
        {
            var receiver = new DummyReceiver();
            timer.TRGO.Connect(receiver, 0);
            
            WriteRegister(Registers.Control2, 2 << 4); // MMS: Update
            WriteRegister(Registers.AutoReload, 100);
            WriteRegister(Registers.Control1, 1);
            
            AdvanceByTicks(99);
            Assert.IsFalse(receiver.Received, "Should not pulse TRGO before overflow");
            AdvanceByTicks(2);
            Assert.IsTrue(receiver.Received, "Should pulse TRGO on overflow");
        }

        [Test]
        public void ShouldTriggerTRGOOnCompareMatch()
        {
            var receiver = new DummyReceiver();
            timer.TRGO.Connect(receiver, 0);

            WriteRegister(Registers.Control2, 4 << 4); // MMS: Compare OC1REF
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.CaptureOrCompare1, 500);
            WriteRegister(Registers.CaptureOrCompareMode1, 6 << 4); // OC1M: PWM Mode 1
            WriteRegister(Registers.CaptureOrCompareEnable, 1);
            WriteRegister(Registers.Control1, 1);
            
            // In PWM Mode 1, OC1REF is high when CNT < CCR1
            Assert.IsTrue(timer.TRGO.IsSet, "TRGO should be high when CNT < CCR1");
            Assert.AreEqual(1, receiver.PulseCount, "TRGO should have pulsed/set high exactly once");
            
            AdvanceByTicks(499);
            Assert.IsTrue(timer.TRGO.IsSet, "TRGO should still be high at CNT=499");
            Assert.AreEqual(1, receiver.PulseCount, "TRGO should not have pulsed again during counting");
            
            AdvanceByTicks(2); // At 501 ticks
            Assert.IsFalse(timer.TRGO.IsSet, "TRGO should be low when CNT > CCR1");
            Assert.AreEqual(1, receiver.PulseCount, "TRGO should have changed level to low but NOT pulsed high again");
        }

        [Test]
        public void ShouldTriggerTRGOOnComparePulse()
        {
            var receiver = new DummyReceiver();
            timer.TRGO.Connect(receiver, 0);

            WriteRegister(Registers.Control2, 3 << 4); // MMS: Compare Pulse
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.CaptureOrCompare1, 500);
            WriteRegister(Registers.Control1, 1);
            
            AdvanceByTicks(498);
            Assert.IsFalse(receiver.Received, "TRGO should not pulse before CC1 match");
            
            AdvanceByTicks(3);
            Assert.IsTrue(receiver.Received, "TRGO should have pulsed on CC1 match");
        }

        [Test]
        public void ShouldStartOnSlaveTrigger()
        {
            var matrix = new STM32_TimerTriggerMatrix(machine);
            timer.TriggerMatrix = matrix;
            
            // Ensure ITR0 is Low before configuring slave mode
            matrix.OnGPIO(0, false);
            
            // Slave: Trigger, TS: ITR2
            WriteRegister(Registers.SlaveModeControl, 6 | (2 << 4)); 
            WriteRegister(Registers.AutoReload, 1000);
            
            Assert.IsFalse(timer.Enabled);
            
            // Negative check: ITR0 should do nothing
            matrix.OnGPIO(0, true);
            Assert.IsFalse(timer.Enabled, "Should not start on ITR0 when TS=ITR2");
            
            // Real trigger: ITR2 rising edge
            matrix.OnGPIO(2, true);
            Assert.IsTrue(timer.Enabled, "Timer should start on rising edge of ITR2");
            
            AdvanceByTicks(500);
            Assert.AreEqual(500, ReadRegister(Registers.Counter));
        }

        [Test]
        public void ShouldResetOnSlaveReset()
        {
            var matrix = new STM32_TimerTriggerMatrix(machine);
            timer.TriggerMatrix = matrix;
            
            matrix.OnGPIO(0, false);
            
            // Slave: Reset, TS: ITR1
            WriteRegister(Registers.SlaveModeControl, 4 | (1 << 4)); 
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.Control1, 1);
            
            AdvanceByTicks(500);
            Assert.AreEqual(500, ReadRegister(Registers.Counter));
            
            // Negative check: ITR0
            matrix.OnGPIO(0, true);
            Assert.AreEqual(500, ReadRegister(Registers.Counter), "Should not reset on ITR0 when TS=ITR1");

            // Real trigger: ITR1 rising edge
            matrix.OnGPIO(1, true);
            Assert.AreEqual(0, ReadRegister(Registers.Counter), "Timer should reset on rising edge of ITR1");
        }

        [Test]
        public void ShouldHandleCombinedResetTrigger()
        {
            var matrix = new STM32_TimerTriggerMatrix(machine);
            timer.TriggerMatrix = matrix;
            
            matrix.OnGPIO(0, false);
            
            // Slave: Combined Reset + Trigger, TS: ITR3
            WriteRegister(Registers.SlaveModeControl, (1 << 16) | (3 << 4)); 
            WriteRegister(Registers.AutoReload, 1000);
            
            Assert.IsFalse(timer.Enabled);
            WriteRegister(Registers.Counter, 500);
            
            // Negative check: ITR0
            matrix.OnGPIO(0, true);
            Assert.IsFalse(timer.Enabled, "Should not start on ITR0 when TS=ITR3");
            
            // Real trigger: ITR3 rising edge
            matrix.OnGPIO(3, true);
            Assert.IsTrue(timer.Enabled, "Timer should start on rising edge of ITR3");
            Assert.AreEqual(0, ReadRegister(Registers.Counter), "Timer should reset on rising edge of ITR3");
        }

        [Test]
        public void ShouldHandlePwmMode2()
        {
            var receiver = new DummyReceiver();
            timer.TRGO.Connect(receiver, 0);

            WriteRegister(Registers.Control2, 4 << 4); // MMS: Compare OC1REF
            WriteRegister(Registers.AutoReload, 1000);
            WriteRegister(Registers.CaptureOrCompare1, 500);
            WriteRegister(Registers.CaptureOrCompareMode1, 7 << 4); // OC1M: PWM Mode 2
            WriteRegister(Registers.CaptureOrCompareEnable, 1);
            WriteRegister(Registers.Control1, 1);
            
            // In PWM Mode 2, OC1REF is low when CNT < CCR1
            Assert.IsFalse(timer.TRGO.IsSet, "TRGO should be low when CNT < CCR1");
            
            AdvanceByTicks(499);
            Assert.IsFalse(timer.TRGO.IsSet, "TRGO should still be low at CNT=499");
            
            AdvanceByTicks(2); // At 501 ticks
            Assert.IsTrue(timer.TRGO.IsSet, "TRGO should be high when CNT > CCR1");
            Assert.AreEqual(1, receiver.PulseCount, "TRGO should have transitioned high exactly once");
        }

        [Test]
        public void ShouldHandleForceModes()
        {
            WriteRegister(Registers.Control2, 4 << 4); // MMS: Compare OC1REF
            WriteRegister(Registers.CaptureOrCompareEnable, 1);
            
            WriteRegister(Registers.CaptureOrCompareMode1, 5 << 4); // OC1M: Force Active
            Assert.IsTrue(timer.TRGO.IsSet, "TRGO should be high (Force Active)");
            
            WriteRegister(Registers.Control1, 1);
            AdvanceByTicks(1000);
            Assert.IsTrue(timer.TRGO.IsSet, "TRGO should remain high after advancement");
            
            WriteRegister(Registers.CaptureOrCompareMode1, 4 << 4); // OC1M: Force Inactive
            Assert.IsFalse(timer.TRGO.IsSet, "TRGO should be low (Force Inactive)");
            
            AdvanceByTicks(1000);
            Assert.IsFalse(timer.TRGO.IsSet, "TRGO should remain low after advancement");
        }

        [Test]
        public void ShouldPreserveLatchModesBetweenOverflows()
        {
            WriteRegister(Registers.Control2, 4 << 4); // MMS: Compare OC1REF
            WriteRegister(Registers.AutoReload, 100);
            WriteRegister(Registers.CaptureOrCompare1, 50);
            WriteRegister(Registers.CaptureOrCompareMode1, 1 << 4); // OC1M: Set Active On Match
            WriteRegister(Registers.CaptureOrCompareEnable, 1);
            WriteRegister(Registers.Control1, 1);

            Assert.IsFalse(timer.TRGO.IsSet, "Should start inactive");
            
            AdvanceByTicks(51); // Cross match (50)
            Assert.IsTrue(timer.TRGO.IsSet, "Should be active after match");
            
            AdvanceByTicks(100); // Cross overflow (at 100)
            Assert.IsTrue(timer.TRGO.IsSet, "Should STAY active after overflow (Latch behavior)");
            
            // Contrast with PWM Mode 1
            WriteRegister(Registers.CaptureOrCompareMode1, 6 << 4); // Switch to PWM Mode 1
            Assert.IsFalse(timer.TRGO.IsSet, "PWM Mode 1 should be low after overflow if CNT >= CCR");
        }

        private void WriteRegister(Registers reg, uint value)
        {
            timer.WriteDoubleWord((long)reg, value);
        }

        private uint ReadRegister(Registers reg)
        {
            return timer.ReadDoubleWord((long)reg);
        }

        private void AdvanceByTicks(ulong ticks)
        {
            var interval = TimeInterval.FromMicroseconds(ticks * 1000000 / Frequency);
            ((BaseClockSource)machine.ClockSource).Advance(interval);
        }

        private class DummyReceiver : IGPIOReceiver
        {
            public int PulseCount { get; private set; }
            public bool Received => PulseCount > 0;
            public void OnGPIO(int number, bool value)
            {
                if(value) PulseCount++;
            }
            public void Reset() { PulseCount = 0; }
        }

        private STM32_Timer timer;
        private IMachine machine;
        private IBusController sysbus;
        private const ulong Frequency = 1000000; // 1MHz

        private enum Registers : long
        {
            Control1 = 0x0,
            Control2 = 0x04,
            SlaveModeControl = 0x08,
            DmaOrInterruptEnable = 0x0C,
            Status = 0x10,
            EventGeneration = 0x14,
            CaptureOrCompareMode1 = 0x18,
            CaptureOrCompareMode2 = 0x1C,
            CaptureOrCompareEnable = 0x20,
            Counter = 0x24,
            Prescaler = 0x28,
            AutoReload = 0x2C,
            RepetitionCounter = 0x30,
            CaptureOrCompare1 = 0x34,
            CaptureOrCompare2 = 0x38,
            CaptureOrCompare3 = 0x3C,
            CaptureOrCompare4 = 0x40,
            BreakAndDeadTime = 0x44,
        }
    }
}
