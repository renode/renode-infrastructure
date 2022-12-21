//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Collections;
using NUnit.Framework;

namespace Antmicro.Renode.UnitTests.Collections
{
    [TestFixture]
    public class CircularBufferTests
    {
        [Test]
        public void ShouldEnqueueAndDequeueWithoutWrap()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 5; ++i)
            {
                buffer.Enqueue(i);
            }
            for(var i = 0; i < 5; ++i)
            {
                Assert.IsTrue(buffer.TryDequeue(out var result));
                Assert.AreEqual(i, result);
            }
        }

        [Test]
        public void ShouldEnqueueAndDequeueWithWrap()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 10; ++i)
            {
                buffer.Enqueue(i);
            }
            for(var i = 5; i < 10; ++i)
            {
                Assert.IsTrue(buffer.TryDequeue(out var result));
                Assert.AreEqual(i, result);
            }
        }

        [Test]
        public void ShouldNotDequeueWhenEmpty()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 5; ++i)
            {
                buffer.Enqueue(i);
            }
            for(var i = 0; i < 5; ++i)
            {
                buffer.TryDequeue(out var _);
            }
            Assert.IsFalse(buffer.TryDequeue(out var __));
        }

        [Test]
        public void ShouldNotPeekWhenEmpty()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 5; ++i)
            {
                buffer.Enqueue(i);
            }
            for(var i = 0; i < 5; ++i)
            {
                buffer.TryDequeue(out var _);
            }
            Assert.IsFalse(buffer.TryPeek(out var __));
        }

        [Test]
        public void ShouldNotChangeCountWhenPeeked()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 5; ++i)
            {
                buffer.Enqueue(i);
            }
            for(var i = 0; i < 5; ++i)
            {
                buffer.TryPeek(out var _);
            }
            Assert.AreEqual(5, buffer.Count);
        }

        [Test]
        public void ShouldEnqueueAndPeekWithoutWrap()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 5; ++i)
            {
                buffer.Enqueue(i);
                Assert.IsTrue(buffer.TryPeek(out var result));
                // The expected result is 0 because the first entry won't get
                // pushed off the end of the buffer.
                Assert.AreEqual(0, result);
            }
        }

        [Test]
        public void ShouldEnqueueAndPeekWithWrap()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i < 10; ++i)
            {
                buffer.Enqueue(i);
                Assert.IsTrue(buffer.TryPeek(out var result));
                // The first 5 pushes will return 0, then 1 2 3 4 5
                // i - 4 because Enqueue(5) pushes off the 0 so after it we have
                // i = 5 and we expect to read a 1
                Assert.AreEqual(i < 5 ? 0 : i - 4, result);
            }
        }

        [Test]
        public void ShouldNeverWrapWithSingleElement()
        {
            var buffer = new CircularBuffer<int>(5);
            for(var i = 0; i <= 10; i++)
            {
                buffer.TryDequeue(out var _);
                buffer.Enqueue(i);
                Assert.IsFalse(buffer.IsWrapped);
            }
        }

        [Test]
        public void ShouldSaveWithoutWrap()
        {
            var buffer = new CircularBuffer<int>(5);
            buffer.Enqueue(1);
            buffer.Enqueue(2);
            buffer.Enqueue(3);
            CollectionAssert.AreEquivalent(new [] { 1, 2, 3 }, buffer);
        }

        [Test]
        public void ShouldSaveWithoutWrapArray()
        {
            var buffer = new CircularBuffer<int>(5);
            var array = new [] { 1, 2, 3, -1, 0 };
            for(var i = 0; i < 3; i++)
            {
                buffer.Enqueue(array[i]);
            }
            var copy = new int[5];
            copy[3] = -1;
            buffer.CopyTo(copy, 0);
            CollectionAssert.AreEqual(array, copy);
        }

        [Test]
        public void ShouldSaveWithWrap()
        {
            var buffer = new CircularBuffer<int>(4);
            for(var i = 0; i < 6; i++)
            {
                buffer.Enqueue(i);
            }
            Assert.AreEqual(buffer, new [] { 2, 3, 4, 5 });
        }

        // This tests that the IEnumerator implementation works correctly
        // when the buffer is exactly full.
        [Test]
        public void ShouldSaveWithExactSize()
        {
            var buffer = new CircularBuffer<int>(4);
            for(var i = 0; i < 4; i++)
            {
                buffer.Enqueue(i);
            }
            Assert.AreEqual(new [] { 0, 1, 2, 3 }, buffer);
        }

        [Test]
        public void ShouldSaveWithWrapArray()
        {
            var buffer = new CircularBuffer<int>(4);
            for(var i = 0; i < 6; i++)
            {
                buffer.Enqueue(i);
            }
            var result = new [] { 2, 3, 4, 5 };
            var copy = new int[4];
            buffer.CopyTo(copy, 0);
            Assert.AreEqual(result, copy);
        }

        [Test]
        public void ShouldDetectOverflow()
        {
            var buffer = new CircularBuffer<int>(3);
            for(var i = 0; i < 3; i++)
            {
                Assert.IsTrue(buffer.Enqueue(i));
            }
            Assert.IsFalse(buffer.Enqueue(3));
        }

        [Test]
        public void ShouldNotDetectOverflow()
        {
            var buffer = new CircularBuffer<int>(3);
            for(var i = 0; i < 3; i++)
            {
                buffer.Enqueue(i);
            }
            buffer.TryDequeue(out var _);
            Assert.IsTrue(buffer.Enqueue(2));
        }
    }
}

