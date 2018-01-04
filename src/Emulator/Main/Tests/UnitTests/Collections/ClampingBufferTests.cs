//
// Copyright (c) 2010-2018 Antmicro
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
	public class ClampingBufferTests
	{
		[Test]
		public void ShouldSaveWithoutOverflow()
		{
			var buffer = new CircularBuffer<int>(5);
			buffer.Add(1);
			buffer.Add(2);
			buffer.Add(3);
			CollectionAssert.AreEqual(new [] { 1, 2, 3 }, buffer);
		}

		[Test]
		public void ShouldSaveWithoutOverflowArray()
		{
			var buffer = new CircularBuffer<int>(5);
			var array = new [] { 1, 2, 3, -1, 0 };
			for(var i = 0; i < 3; i++)
			{
				buffer.Add(array[i]);
			}
			var copy = new int[5];
			copy[3] = -1;
			buffer.CopyTo(copy, 0);
			CollectionAssert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSaveWithOverflow()
		{
			var buffer = new CircularBuffer<int>(4);
			for(var i = 0; i < 6; i++)
			{
				buffer.Add(i);
			}
			Assert.AreEqual(buffer, new [] { 3, 4, 5 });
		}

		[Test]
		public void ShouldSaveWithOverflowArray()
		{
			var buffer = new CircularBuffer<int>(4);
			for(var i = 0; i < 6; i++)
			{
				buffer.Add(i);
			}
			var result = new [] { 3, 4, 5 };
			var copy = new int[3];
			buffer.CopyTo(copy, 0);
			Assert.AreEqual(result, copy);
		}
	}
}

