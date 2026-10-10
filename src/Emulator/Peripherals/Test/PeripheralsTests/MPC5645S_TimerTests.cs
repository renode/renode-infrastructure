//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
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
    public class MPC5645S_TimerTests
    {
        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
        }

        [TearDown]
        public void TearDown()
        {
            machine.Dispose();
        }

        [Test]
        public void ShouldMergePitLoadByteAndWordLanes()
        {
            var pit = new MPC5645S_PIT(machine, 1000);
            pit.WriteDoubleWord(0x100, 0x12345678);
            pit.WriteByte(0x101, 0xAB);
            pit.WriteWord(0x102, 0xCDEF);
            Assert.AreEqual(0x12ABCDEF, pit.ReadDoubleWord(0x100));
            Assert.AreEqual(0x12AB, pit.ReadWord(0x100));
            Assert.AreEqual(0xEF, pit.ReadByte(0x103));
            Assert.AreEqual(0, pit.ReadDoubleWord(0x110));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShouldLatchPitExpiryAndClearOnlyLowFlagLane(bool interruptEnabled)
        {
            var pit = new MPC5645S_PIT(machine, 1000);
            pit.WriteDoubleWord(0, 0);
            pit.WriteDoubleWord(0x100, 9);
            pit.WriteByte(0x10B, (byte)(interruptEnabled ? 3 : 1));
            AdvanceMilliseconds(9);
            Assert.AreEqual(0, pit.ReadDoubleWord(0x104));
            Assert.AreEqual(0, pit.ReadDoubleWord(0x10C));
            AdvanceMilliseconds(1);
            Assert.AreEqual(9, pit.ReadDoubleWord(0x104));
            Assert.AreEqual(1, pit.ReadDoubleWord(0x10C));
            Assert.AreEqual(interruptEnabled, pit.IRQ0.IsSet);
            pit.WriteByte(0x10C, 1);
            Assert.AreEqual(1, pit.ReadDoubleWord(0x10C));
            pit.WriteByte(0x10F, 1);
            Assert.AreEqual(0, pit.ReadDoubleWord(0x10C));
            Assert.False(pit.IRQ0.IsSet);
        }

        [Test]
        public void ShouldFreezePitWhileModuleDisabled()
        {
            var pit = new MPC5645S_PIT(machine, 1000);
            pit.WriteDoubleWord(0, 0);
            pit.WriteDoubleWord(0x100, 9);
            pit.WriteDoubleWord(0x108, 1);
            AdvanceMilliseconds(3);
            Assert.AreEqual(6, pit.ReadDoubleWord(0x104));
            pit.WriteByte(3, 2);
            AdvanceMilliseconds(100);
            Assert.AreEqual(6, pit.ReadDoubleWord(0x104));
            pit.WriteByte(3, 0);
            AdvanceMilliseconds(2);
            Assert.AreEqual(4, pit.ReadDoubleWord(0x104));
        }

        [Test]
        public void ShouldMergeStmCounterLanes()
        {
            var stm = new MPC5645S_STM(machine, 1000);
            stm.WriteDoubleWord(4, 0x12345678);
            stm.WriteWord(4, 0xABCD);
            stm.WriteByte(7, 0xEF);
            Assert.AreEqual(0xABCD56EF, stm.ReadDoubleWord(4));
            Assert.AreEqual(0xABCD, stm.ReadWord(4));
            Assert.AreEqual(0x56, stm.ReadByte(6));
            AdvanceMilliseconds(20);
            Assert.AreEqual(0xABCD56EF, stm.ReadDoubleWord(4));
        }

        [Test]
        public void ShouldPrescaleStmAndStopWithoutClearingCounter()
        {
            var stm = new MPC5645S_STM(machine, 1000);
            stm.WriteWord(2, 0x0301);
            AdvanceMilliseconds(20);
            Assert.AreEqual(5, stm.ReadDoubleWord(4));
            stm.WriteByte(3, 0);
            AdvanceMilliseconds(20);
            Assert.AreEqual(5, stm.ReadDoubleWord(4));
            Assert.AreEqual(0x300, stm.ReadDoubleWord(0));
            stm.Reset();
            Assert.AreEqual(0, stm.ReadDoubleWord(4));
            Assert.AreEqual(0, stm.ReadDoubleWord(0));
        }

        private void AdvanceMilliseconds(ulong value)
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMilliseconds(value));
        }

        private Machine machine;
    }
}
