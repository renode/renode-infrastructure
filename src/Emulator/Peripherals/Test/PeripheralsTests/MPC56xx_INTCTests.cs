//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.IRQControllers;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MPC56xx_INTCTests
    {
        [SetUp]
        public void SetUp()
        {
            intc = new MPC56xx_INTC();
        }

        [Test]
        public void ShouldMergeBigEndianPriorityLanes()
        {
            intc.WriteDoubleWord(0x40, 0x01020304);
            intc.WriteWord(0x41, 0x0506);
            intc.WriteByte(0x43, 0xFF);
            Assert.AreEqual(0x0105060F, intc.ReadDoubleWord(0x40));
            Assert.AreEqual(0x0506, intc.ReadWord(0x41));
            Assert.AreEqual(1, intc.ReadByte(0x40));
            intc.WriteByte(0x08, 0);
            Assert.AreEqual(15, intc.CurrentPriority);
            intc.WriteByte(0x0B, 0);
            Assert.AreEqual(0, intc.CurrentPriority);
        }

        [TestCase(0u, 0x12345800u, 4u)]
        [TestCase(0x80000000u, 0x12345000u, 8u)]
        public void ShouldReturnSoftwareVector(uint mode, uint vectorBase, uint stride)
        {
            intc.WriteDoubleWord(0, mode);
            intc.WriteDoubleWord(0x10, 0x12345FFF);
            intc.WriteByte(0x40 + 37, 5);
            intc.OnGPIO(37, true);
            Assert.False(intc.IRQ.IsSet, "Reset CPR masks all sources");
            intc.WriteDoubleWord(0x08, 0);
            Assert.True(intc.IRQ.IsSet);
            Assert.AreEqual(vectorBase + 37 * stride, intc.ReadDoubleWord(0x10));
            Assert.AreEqual(37, intc.LastAcknowledgedSource);
            Assert.AreEqual(1, intc.GetAcknowledgeCount(37));
            Assert.False(intc.IRQ.IsSet);
        }

        [Test]
        public void ShouldSelectHighestPriorityAndRestoreNestedPriority()
        {
            intc.WriteByte(0x41, 3);
            intc.WriteByte(0x42, 7);
            intc.OnGPIO(1, true);
            intc.OnGPIO(2, true);
            intc.WriteDoubleWord(0x08, 0);
            Assert.AreEqual(8, intc.ReadDoubleWord(0x10));
            Assert.AreEqual(7, intc.CurrentPriority);
            intc.WriteByte(0x43, 9);
            intc.OnGPIO(3, true);
            Assert.True(intc.IRQ.IsSet);
            Assert.AreEqual(12, intc.ReadDoubleWord(0x10));
            Assert.AreEqual(2, intc.NestingDepth);
            intc.OnGPIO(3, false);
            intc.WriteDoubleWord(0x18, 0);
            Assert.AreEqual(7, intc.CurrentPriority);
            Assert.False(intc.IRQ.IsSet);
            intc.OnGPIO(2, false);
            intc.WriteDoubleWord(0x18, 0);
            Assert.AreEqual(0, intc.CurrentPriority);
            Assert.True(intc.IRQ.IsSet);
            Assert.AreEqual(4, intc.ReadDoubleWord(0x10));
        }

        [Test]
        public void ShouldBreakPriorityTiesBySourceNumber()
        {
            intc.WriteWord(0x44, 0x0505);
            intc.OnGPIO(5, true);
            intc.OnGPIO(4, true);
            intc.WriteDoubleWord(0x08, 0);
            Assert.AreEqual(16, intc.ReadDoubleWord(0x10));
        }

        [Test]
        public void ShouldLatchShortPulseUntilAcknowledge()
        {
            intc.WriteByte(0x4A, 4);
            intc.WriteDoubleWord(0x08, 0);
            intc.OnGPIO(10, true);
            intc.OnGPIO(10, false);
            Assert.True(intc.IRQ.IsSet);
            Assert.AreEqual(40, intc.ReadDoubleWord(0x10));
            intc.WriteDoubleWord(0x18, 0);
            Assert.False(intc.IRQ.IsSet);
            Assert.AreEqual(0, intc.NestingDepth);
        }

        [Test]
        public void ShouldSetAndClearSoftwareInterruptByByteLane()
        {
            intc.WriteByte(0x42, 6);
            intc.WriteByte(0x22, 2);
            Assert.AreEqual(0x00000100, intc.ReadDoubleWord(0x20));
            intc.WriteDoubleWord(0x08, 0);
            Assert.AreEqual(8, intc.ReadDoubleWord(0x10));
            intc.WriteByte(0x22, 1);
            intc.WriteDoubleWord(0x18, 0);
            Assert.False(intc.IRQ.IsSet);
            Assert.AreEqual(0, intc.ReadByte(0x22));
        }

        private MPC56xx_INTC intc;
    }
}
