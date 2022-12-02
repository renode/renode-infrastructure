//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using NUnit.Framework;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

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

        [Test]
        public void EmptyRangeShouldContainItself()
        {
            Assert.IsTrue(Range.Empty.Contains(Range.Empty));
        }

        [Test]
        public void EmptyRangeShouldNotContainAnyAddress()
        {
            Assert.IsFalse(Range.Empty.Contains(0UL));
            Assert.IsFalse(Range.Empty.Contains(0x80000000UL));
            Assert.IsFalse(Range.Empty.Contains(0xFFFFFFFFUL));
            Assert.IsFalse(Range.Empty.Contains(0x8000000000000000UL));
            Assert.IsFalse(Range.Empty.Contains(0xFFFFFFFFFFFFFFFFUL));
        }

        [Test]
        public void ShouldContainEmptyRange()
        {
            var range = new Range(0x1000, 0x1200);
            Assert.IsTrue(range.Contains(Range.Empty));
        }

        [Test]
        public void SubtractNoIntersection()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1000, 0x200);
            CollectionAssert.AreEquivalent(new [] { range }, range.Subtract(sub));
        }

        [Test]
        public void SubtractEmpty()
        {
            var range = new Range(0x0, 0xA00);
            CollectionAssert.AreEquivalent(new [] { range }, range.Subtract(Range.Empty));
        }

        [Test]
        public void SubtractContaining()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1000, 0x1200);
            CollectionAssert.IsEmpty(range.Subtract(sub));
        }

        [Test]
        public void SubtractItself()
        {
            var range = new Range(0x1600, 0xA00);
            CollectionAssert.IsEmpty(range.Subtract(range));
        }

        [Test]
        public void SubtractLeft()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1000, 0x800);
            var expected = new Range(0x1800, 0x800);
            CollectionAssert.AreEquivalent(new [] { expected }, range.Subtract(sub));
        }

        [Test]
        public void SubtractRight()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1800, 0x800);
            var expected = new Range(0x1600, 0x200);
            CollectionAssert.AreEquivalent(new [] { expected }, range.Subtract(sub));
        }

        [Test]
        public void SubtractContained()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1800, 0x200);
            var expected1 = new Range(0x1600, 0x200);
            var expected2 = new Range(0x1A00, 0x600);
            CollectionAssert.AreEquivalent(new [] { expected1, expected2 }, range.Subtract(sub));
        }
    }
}

