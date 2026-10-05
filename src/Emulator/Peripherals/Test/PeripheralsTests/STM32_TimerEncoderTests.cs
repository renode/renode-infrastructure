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
    [TestFixture]
    public class STM32_TimerEncoderTests
    {
        [OneTimeSetUp]
        public void CreatePeripheral()
        {
            machine = new Machine();
            timer = new STM32_Timer(machine, TimerFrequency, initialLimit: 0xFFFF);
        }

        [SetUp]
        public void PreparePeripheral()
        {
            timer.Reset();
            // TI1 and TI2 as inputs (CC1S = CC2S = 01), both channels enabled, encoder mode 3, ARR = 0xFFFF, CEN
            timer.WriteDoubleWord((long)Registers.AutoReload, 0xFFFF);
            timer.WriteDoubleWord((long)Registers.CaptureOrCompareMode1, 0x0101);
            timer.WriteDoubleWord((long)Registers.CaptureOrCompareEnable, 0x0011);
            timer.WriteDoubleWord((long)Registers.SlaveModeControl, EncoderMode3);
            timer.WriteDoubleWord((long)Registers.Control1, ControlCounterEnable);
        }

        [Test]
        public void ShouldCountFourPerQuadratureCycle()
        {
            ForwardCycle();
            Assert.AreEqual(4, timer.ReadDoubleWord((long)Registers.Counter));
            Assert.AreEqual(ControlCounterEnable, timer.ReadDoubleWord((long)Registers.Control1) & (ControlCounterEnable | ControlDirection), "DIR must read 0 (up)");
        }

        [Test]
        public void ShouldCountDownWhenTI2Leads()
        {
            ForwardCycle();
            ReverseCycle();
            Assert.AreEqual(0, timer.ReadDoubleWord((long)Registers.Counter));
            Assert.AreEqual(ControlDirection, timer.ReadDoubleWord((long)Registers.Control1) & ControlDirection, "DIR must read 1 (down)");
        }

        [Test]
        public void ShouldWrapToAutoReloadWhenCountingBelowZero()
        {
            // RM0090 18.3.12: the counter counts between 0 and ARR in both directions.
            ReverseCycle();
            Assert.AreEqual(0xFFFC, timer.ReadDoubleWord((long)Registers.Counter));
        }

        [Test]
        public void ShouldWrapToZeroWhenCountingAboveAutoReload()
        {
            timer.WriteDoubleWord((long)Registers.AutoReload, 5);
            ForwardCycle();
            ForwardCycle();
            // 8 counts with ARR = 5: 0..5 then wrap → 6 counts land on 0, two more on 2
            Assert.AreEqual(2, timer.ReadDoubleWord((long)Registers.Counter));
        }

        [Test]
        public void ShouldCountArrPlusOneTicksPerPeriodWhenClocked()
        {
            // Not encoder mode: the clocked counter must also count 0..ARR inclusive (RM0090 18.3.1).
            timer.WriteDoubleWord((long)Registers.SlaveModeControl, 0);
            timer.WriteDoubleWord((long)Registers.AutoReload, 99);
            timer.WriteDoubleWord((long)Registers.Control1, ControlCounterEnable);
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromSeconds(10000.0 / TimerFrequency));
            Assert.AreEqual(0, timer.ReadDoubleWord((long)Registers.Counter));
        }

        private void ForwardCycle()
        {
            timer.OnGPIO(0, true);
            timer.OnGPIO(1, true);
            timer.OnGPIO(0, false);
            timer.OnGPIO(1, false);
        }

        private void ReverseCycle()
        {
            timer.OnGPIO(1, true);
            timer.OnGPIO(0, true);
            timer.OnGPIO(1, false);
            timer.OnGPIO(0, false);
        }

        private IMachine machine;
        private STM32_Timer timer;

        private const ulong TimerFrequency = 10000000;
        private const uint ControlCounterEnable = 1u << 0;
        private const uint ControlDirection = 1u << 4;
        private const uint EncoderMode3 = 3u;

        private enum Registers
        {
            Control1 = 0x00,
            SlaveModeControl = 0x08,
            CaptureOrCompareMode1 = 0x18,
            CaptureOrCompareEnable = 0x20,
            Counter = 0x24,
            AutoReload = 0x2C,
        }
    }
}
