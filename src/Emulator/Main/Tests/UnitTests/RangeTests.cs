//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using NUnit.Framework;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class RangeTests
    {
        [Test]
        public void ShouldIntersectRange()
        {
            var range1 = new Range(0x1000, 0x200);
            var range2 = new Range(0x1100, 0x300);
            var expectedResult = new Range(0x1100, 0x100);
            var intersection1 = range1.Intersect(range2);
            var intersection2 = range2.Intersect(range1);
            Assert.AreEqual(expectedResult, intersection1);
            Assert.AreEqual(expectedResult, intersection2);
        }

        [Test]
        public void ShouldIntersectRangesWithOneCommonAddress()
        {
            var range1 = new Range(0x1000, 0x200);
            var range2 = new Range(0x11ff, 0x300);
            var expectedResult = new Range(0x11ff, 0x1);
            var intersection = range1.Intersect(range2);
            Assert.AreEqual(expectedResult, intersection);
        }

        [Test]
        public void ShouldShiftRange()
        {
            var range = new Range(0x2200, 0x200);
            var expectedResult = new Range(0x2500, 0x200);
            var result = range.ShiftBy(0x300);
            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void ShouldContainFirstAddress()
        {
            var range = new Range(0x1600, 0xA00);
            Assert.IsTrue(range.Contains(0x1600));
        }

        [Test]
        public void ShouldContainLastAddress()
        {
            var range = new Range(0x1600, 0xA00);
            Assert.IsTrue(range.Contains(0x1fff));
        }

        [Test]
        public void ShouldNotContainNearbyAddresses()
        {
            var range = new Range(0x600, 0x100);
            Assert.IsFalse(range.Contains(0x5FF));
            Assert.IsFalse(range.Contains(0x700));
        }

        [Test]
        public void ShouldContainItself()
        {
            var range = new Range(0x1000, 0x1200);
            Assert.IsTrue(range.Contains(range));
        }
    }
}

