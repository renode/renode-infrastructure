//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Timers;
using NUnit.Framework;
using System.Threading;
using System.Diagnostics;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class CoreTimerTest
    {
        [Test]
        public void ShouldBeAscending()
        {
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 100, null, String.Empty, 100000, Direction.Ascending, true);
            var oldValue = 0UL;
            for(var i = 0; i < 100; i++)
            {
                manualClockSource.AdvanceBySeconds(1);
                var value = timer.Value;
                Assert.Greater(value, oldValue, "Timer is not monotonic.");
                oldValue = value;
            }
        }

        [Test]
        public void ShouldBeDescending()
        {
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 100, null, String.Empty, 100000, Direction.Descending, true);
            var oldValue = timer.Limit;
            for(var i = 0; i < 100; i++)
            {
                manualClockSource.AdvanceBySeconds(1);
                var value = timer.Value;
                Assert.Less(value, oldValue, "Timer is not monotonic.");
                oldValue = value;
            }
        }

        [Test]
        public void ShouldNotExceedLimitAscending()
        {
            var limit = 100UL;
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 1, null, String.Empty, limit, Direction.Ascending, true);
            manualClockSource.AdvanceBySeconds(limit - 1);
            for(var i = 0; i < 3; ++i)
            {
                var value = timer.Value;
                Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                manualClockSource.AdvanceBySeconds(1);
            }
        }

        [Test]
        public void ShouldNotExceedLimitDescending()
        {
            var limit = 100UL;
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 1, null, String.Empty, limit, Direction.Descending, true);
            manualClockSource.AdvanceBySeconds(limit - 1);
            for(var i = 0; i < 3; i++)
            {
                var value = timer.Value;
                Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                manualClockSource.AdvanceBySeconds(1);
            }
        }

        [Test]
        public void ShouldHandleMicrosecondPrecisionTimerEvents()
        {
            uint activationCounter = 0;

            var limit = 4UL;
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 1000000, null, String.Empty, limit, Direction.Descending, true);
            timer.EventEnabled = true;
            timer.LimitReached += () => activationCounter++;
            manualClockSource.Advance(TimeInterval.FromMicroseconds(10));

            Assert.AreEqual(2, activationCounter, "Timer did not go off twice.");
            Assert.AreEqual(2, timer.Value, "Timer value is not expected.");
        }

        [Test]
        public void ShouldHandleSubMicrosecondPrecisionTimerEvents()
        {
            uint activationCounter = 0;

            var limit = 4UL;
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 10000000, null, String.Empty, limit, Direction.Descending, true);
            timer.EventEnabled = true;
            timer.LimitReached += () => activationCounter++;
            manualClockSource.Advance(TimeInterval.FromMicroseconds(1));

            Assert.AreEqual(2, activationCounter, "Timer did not go off twice.");
            Assert.AreEqual(2, timer.Value, "Timer value is not expected.");
        }

        [Test]
        public void ShouldSwitchDirectionProperly()
        {
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 100, null, String.Empty, 100000, Direction.Ascending, true);
            timer.EventEnabled = true;
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            manualClockSource.AdvanceBySeconds(2);
            timer.Direction = Direction.Descending; // and then change the direction
            manualClockSource.AdvanceBySeconds(2);
            Assert.IsTrue(ticked);
        }

        [Test]
        public void ShouldNotFireAlarmWhenInterruptsAreDisabled()
        {
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 1, null, String.Empty, 10, Direction.Descending, true);
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            manualClockSource.AdvanceBySeconds(11);
            Assert.IsFalse(ticked);
        }

        [Test]
        public void ShouldFireAlarmWhenInterruptsAreEnabled()
        {
            var manualClockSource = new ManualClockSource();
            var timer = new LimitTimer(manualClockSource, 1, null, String.Empty, 10, Direction.Descending, true);
            timer.EventEnabled = true;
            var ticked = false;
            timer.LimitReached += () => ticked = true;
            // var val =timer.Value;
            manualClockSource.AdvanceBySeconds(10);
            Assert.IsTrue(ticked);
        }

        private class ManualClockSource : BaseClockSource
        {
            public void AdvanceBySeconds(ulong seconds)
            {
                Advance(TimeInterval.FromSeconds(seconds));
            }
        }
    }
}

