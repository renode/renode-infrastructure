//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.UnitTests.Mocks;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class LimitTimerTests
    {
        [Test]
        public void ShouldBeAscending()
        {
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 100, null, String.Empty, 100000, Direction.Ascending, true);
            var oldValue = 0UL;
            for(var i = 0; i < 100; i++)
            {
                mockClockSource.AdvanceBySeconds(1);
                var value = timer.Value;
                Assert.Greater(value, oldValue, "Timer is not monotonic.");
                oldValue = value;
            }
        }

        [Test]
        public void ShouldBeDescending()
        {
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 100, null, String.Empty, 100000, Direction.Descending, true);
            var oldValue = timer.Limit;
            for(var i = 0; i < 100; i++)
            {
                mockClockSource.AdvanceBySeconds(1);
                var value = timer.Value;
                Assert.Less(value, oldValue, "Timer is not monotonic.");
                oldValue = value;
            }
        }

        [Test]
        public void ShouldNotExceedLimitAscending()
        {
            var limit = 100UL;
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 1, null, String.Empty, limit, Direction.Ascending, true);
            mockClockSource.AdvanceBySeconds(limit - 1);
            for(var i = 0; i < 3; ++i)
            {
                var value = timer.Value;
                Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                mockClockSource.AdvanceBySeconds(1);
            }
        }

        [Test]
        public void ShouldNotExceedLimitDescending()
        {
            var limit = 100UL;
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 1, null, String.Empty, limit, Direction.Descending, true);
            mockClockSource.AdvanceBySeconds(limit - 1);
            for(var i = 0; i < 3; i++)
            {
                var value = timer.Value;
                Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                mockClockSource.AdvanceBySeconds(1);
            }
        }

        [Test]
        public void ShouldHandleMicrosecondPrecisionTimerEvents()
        {
            uint activationCounter = 0;

            var limit = 4UL;
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 1000000, null, String.Empty, limit, Direction.Descending, true);
            timer.EventEnabled = true;
            timer.LimitReached += () => activationCounter++;
            mockClockSource.Advance(TimeInterval.FromMicroseconds(10));

            Assert.AreEqual(2, activationCounter, "Timer did not go off twice.");
            Assert.AreEqual(2, timer.Value, "Timer value is not expected.");
        }

        [Test]
        public void ShouldHandleSubMicrosecondPrecisionTimerEvents()
        {
            uint activationCounter = 0;

            var limit = 4UL;
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 10000000, null, String.Empty, limit, Direction.Descending, true);
            timer.EventEnabled = true;
            timer.LimitReached += () => activationCounter++;
            mockClockSource.Advance(TimeInterval.FromMicroseconds(1));

            Assert.AreEqual(2, activationCounter, "Timer did not go off twice.");
            Assert.AreEqual(2, timer.Value, "Timer value is not expected.");
        }

        [Test]
        public void ShouldSwitchDirectionProperly()
        {
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 100, null, String.Empty, 100000, Direction.Ascending, true);
            timer.EventEnabled = true;
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            mockClockSource.AdvanceBySeconds(2);
            timer.Direction = Direction.Descending; // and then change the direction
            mockClockSource.AdvanceBySeconds(2);
            Assert.IsTrue(ticked);
        }

        [Test]
        public void ShouldNotFireAlarmWhenInterruptsAreDisabled()
        {
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 1, null, String.Empty, 10, Direction.Descending, true);
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            mockClockSource.AdvanceBySeconds(11);
            Assert.IsFalse(ticked);
        }

        [Test]
        public void ShouldFireAlarmWhenInterruptsAreEnabled()
        {
            var mockClockSource = new MockClockSource();
            var timer = new LimitTimer(mockClockSource, 1, null, String.Empty, 10, Direction.Descending, true);
            timer.EventEnabled = true;
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            // var val =timer.Value;
            mockClockSource.AdvanceBySeconds(10);
            Assert.IsTrue(ticked);
        }
    }
}