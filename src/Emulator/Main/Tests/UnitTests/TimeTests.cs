//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Time;
using System.Collections.Generic;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class TimeTests
    {
        [Test]
        public void ShouldTickWithOneHandler()
        {
            var clocksource = new BaseClockSource();
            var counter = 0;

            clocksource.AddClockEntry(new ClockEntry(2, 1, () => counter++, null, String.Empty) { Value = 0 });
            clocksource.Advance(TimeInterval.FromTicks(1));
            Assert.AreEqual(0, counter);
            clocksource.Advance(TimeInterval.FromTicks(1));
            Assert.AreEqual(1, counter);
            clocksource.Advance(TimeInterval.FromTicks(1));
            Assert.AreEqual(1, counter);
            clocksource.Advance(TimeInterval.FromTicks(2));
            Assert.AreEqual(2, counter);
        }

        [Test]
        public void ShouldTickWithTwoHandlers()
        {
            var clocksource = new BaseClockSource();
            var counterA = 0;
            var counterB = 0;

            clocksource.AddClockEntry(new ClockEntry(2, 1, () => counterA++, null, String.Empty) { Value = 0 });
            clocksource.AddClockEntry(new ClockEntry(5, 1, () => counterB++, null, String.Empty) { Value = 0 });
            clocksource.Advance(TimeInterval.FromTicks(2));
            Assert.AreEqual(1, counterA);
            Assert.AreEqual(0, counterB);
            clocksource.Advance(TimeInterval.FromTicks(2));
            Assert.AreEqual(2, counterA);
            Assert.AreEqual(0, counterB);
            clocksource.Advance(TimeInterval.FromTicks(1));
            Assert.AreEqual(2, counterA);
            Assert.AreEqual(1, counterB);
        }

        [Test]
        public void ShouldHaveHandlersInSync()
        {
            // we test here whether handler executed by the slower clock entry
            // always "sees" value of the faster one as ten times its own value
            var clockSource = new BaseClockSource();

            Action firstHandler = () =>
            {
            };

            var values = new List<ulong>();

            // clock entry with ratio -10 is 10 times slower than the one with 1
            clockSource.AddClockEntry(new ClockEntry(10000, 1, firstHandler, null, String.Empty));
            clockSource.AddClockEntry(new ClockEntry(1, -10, () => values.Add(clockSource.GetClockEntry(firstHandler).Value), null, String.Empty));

            clockSource.Advance(TimeInterval.FromTicks(9), true);
            clockSource.Advance(TimeInterval.FromTicks(8), true);
            clockSource.Advance(TimeInterval.FromTicks(20), true);

            CollectionAssert.AreEqual(new [] { 10, 20, 30 }, values);
        }
    }
}

