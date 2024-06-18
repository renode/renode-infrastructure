//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class RangeTests
    {
        [Test]
        public void CreateRangeWithExtensions()
        {
            var rangeAt0x1000Size0x100 = new Range(0x1000, 0x100);
            Assert.AreEqual(rangeAt0x1000Size0x100, 0x1000.By(0x100));
            Assert.AreEqual(rangeAt0x1000Size0x100, 0x1000.To(0x10FF));

            var oneByteRange = new Range(0x0, 1);
            Assert.AreEqual(oneByteRange, 0x0.By(1));
            Assert.AreEqual(oneByteRange, 0x0.To(0x0));
        }

        [Test]
        public void ShouldHandleCreatingRangeFromZeroToUlongMaxValue()
        {
            // `Range.Size` is `ulong` so the full ulong range with size 2^64 simply isn't
            // supported. Other constructors use `ulong` size so they have no problem with
            // handling such a range. Let's make sure the exception occurs and isn't nasty.
            Assert.Throws<RecoverableException>(
                () => 0x0.To(ulong.MaxValue),
                "<0, 2^64-1> ranges aren't currently supported"
            );
        }

        [Test]
        public void ShouldHandleCreatingRangeWithInvalidEndAddress()
        {
            Assert.Throws<RecoverableException>(
                () => 0x1000.To(0x100),
                "the start address can't be higher than the end address"
            );
        }

        [Test]
        public void ShouldHandleCreatingRangeWithInvalidSize()
        {
            var size = ulong.MaxValue;
            var startAddress = (ulong)0x1000;

            var expectedExceptionReason = $"size is too large: 0x{size:X}";
            Assert.Throws<RecoverableException>(() => new Range(startAddress, size), expectedExceptionReason);
            Assert.Throws<RecoverableException>(() => startAddress.By(size), expectedExceptionReason);
        }

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
        public void SubtractNoIntersection()
        {
            var range = new Range(0x1600, 0xA00);
            var sub = new Range(0x1000, 0x200);
            CollectionAssert.AreEquivalent(new [] { range }, range.Subtract(sub));
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

        [Test]
        public void MinimalRangesCollectionAdd()
        {
            var ranges = new MinimalRangesCollection();
            Assert.AreEqual(ranges.Count, 0);

            ranges.Add(new Range(0x1000, 0x400));
            ranges.Add(new Range(0x1800, 0x800));
            Assert.AreEqual(ranges.Count, 2);

            ranges.Add(new Range(0x0, 0x1000));
            Assert.AreEqual(ranges.Count, 2);

            ranges.Add(new Range(0x2000, 0x400));
            Assert.AreEqual(ranges.Count, 2);

            ranges.Add(new Range(0x1000, 0x1000));
            Assert.AreEqual(ranges.Count, 1);

            var expected = new Range(0x0, 0x2400);
            CollectionAssert.AreEquivalent(new [] { expected }, ranges);
        }

        [Test]
        public void MinimalRangesCollectionRemoveOutside()
        {
            var ranges = new MinimalRangesCollection();
            var range = new Range(0x1000, 0x1000);
            ranges.Add(range);

            Assert.IsFalse(ranges.Remove(new Range(0x0, 0x1000)));
            Assert.IsFalse(ranges.Remove(new Range(0x2000, 0x1000)));
            Assert.IsFalse(ranges.Remove(new Range(0x8000, 0x1000)));

            CollectionAssert.AreEquivalent(new [] { range }, ranges);
        }

        [Test]
        public void MinimalRangesCollectionRemoveSubrange()
        {
            var ranges = new MinimalRangesCollection();
            var modifiedRange = new Range(0x1000, 0x1000);
            var untouchedRange = new Range(0x8000, 0x1000);
            ranges.Add(modifiedRange);
            ranges.Add(untouchedRange);

            var sub = new Range(0x1400, 0x800);
            var wasRemoved = ranges.Remove(sub);
            Assert.IsTrue(wasRemoved);

            var expected1 = new Range(0x1000, 0x400);
            var expected2 = new Range(0x1C00, 0x400);
            CollectionAssert.AreEquivalent(new [] { expected1, expected2, untouchedRange }, ranges);
        }
    }
}

