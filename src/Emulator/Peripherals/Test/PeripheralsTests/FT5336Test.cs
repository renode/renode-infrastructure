//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;

using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class FT5336Test
    {
        [Test]
        public void NormalTest()
        {
            DoTest(50, 60);
        }

        [Test]
        public void RotatedTest()
        {
            DoTest(60, 50, isRotated: true);
        }

        [Test]
        public void InvertedTest()
        {
            DoTest(60, 60, isInvertedX: true);
        }

        [Test]
        public void RotatedInvertedTest()
        {
            DoTest(90, 50, isRotated: true, isInvertedY: true);
        }

        private void AssertTouch(FT5336 touch, ushort oX, ushort oY)
        {
            var msg = new byte[] { 0x3 };
            touch.Write(msg);
            var report = touch.Read(0x4);
            // Bits 14+ of X is the touch status, 2 is "held down"
            // Bits 12+ of Y is ID, ignore it
            Assert.AreEqual((report[0] & 0xF0) >> 6, 2);
            var rX = ((report[0] & 0xF) << 8) | report[1];
            var rY = ((report[2] & 0xF) << 8) | report[3];
            Assert.AreEqual(oX, rX);
            Assert.AreEqual(oY, rY);
        }

        private void DoTest(ushort oX, ushort oY, bool isRotated = false, bool isInvertedX = false, bool isInvertedY = false)
        {
            var touch = new FT5336(isRotated, isInvertedX, isInvertedY);
            touch.MaxX = 110;
            touch.MaxY = 150;
            touch.MoveTo(50, 60);
            touch.Press();
            AssertTouch(touch, oX, oY);
        }
    }
}
