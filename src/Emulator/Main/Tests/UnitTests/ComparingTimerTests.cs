//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class ComparingTimerTests
    {
        [Test]
        public void ShouldThrowOnCompareHigherThanLimit()
        {
            var timer = new ComparingTimer(new BaseClockSource(), 10, null, String.Empty, 20, compare: 5);
            Assert.Throws<InvalidOperationException>(() => timer.Compare = 30);
        }

        [Test]
        public void ShouldHandleCompareValueChangeWhenEnabled()
        {
            var compareCounter = 0;
            var clockSource = new BaseClockSource();
            var timer = new ComparingTimer(clockSource, 1000000, null, String.Empty, 65535 + 1, enabled: true, workMode: WorkMode.Periodic, eventEnabled: true, compare: 65535);
            timer.CompareReached += delegate { compareCounter++; };

            clockSource.Advance(TimeInterval.FromMicroseconds(65535), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromMicroseconds(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            compareCounter = 0;

            clockSource.Advance(TimeInterval.FromMicroseconds(16304), true);
            Assert.AreEqual(16304, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            timer.Compare = 0;
            Assert.AreEqual(16304, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            clockSource.Advance(TimeInterval.FromMicroseconds(65535 - 16304), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            clockSource.Advance(TimeInterval.FromMicroseconds(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromMicroseconds(65535), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromMicroseconds(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(2, compareCounter);
        }

        [Test]
        public void ShouldClearWhenValueSetToZero()
        {
            var compare = 200UL;
            var limit = 300UL;
            var compareCounter = 0;
            var clockSource = new BaseClockSource();
            var timer = new ComparingTimer(clockSource, frequency: 1000000, owner: null, localName: String.Empty, limit: limit, enabled: true, eventEnabled: true, compare: compare);
            timer.CompareReached += delegate { compareCounter++; };

            // Run to 100 ticks
            var advance = 100UL;
            clockSource.Advance(TimeInterval.FromMicroseconds(advance), true);
            Assert.AreEqual(advance, timer.Value);
            Assert.AreEqual(compare, timer.Compare);
            Assert.AreEqual(compareCounter, 0);

            // Clear timer
            timer.Value = 0;
            Assert.AreEqual(timer.Value, 0);
            Assert.AreEqual(timer.Compare, compare);
        }

        [Test]
        public void ShouldGenerateCompareEventAtCompareAfterClear()
        {
            var limit = 300UL;
            var compare = 100UL;
            var compareCounter = 0;
            var clockSource = new BaseClockSource(); var timer = new ComparingTimer(clockSource, frequency: 1000000, owner: null,
                  localName: String.Empty, limit: limit, enabled: true, eventEnabled: true, compare: compare);
            timer.CompareReached += delegate { compareCounter++; };

            // Run to 200 ticks, then clear
            var advance = 200UL;
            clockSource.Advance(TimeInterval.FromMicroseconds(advance), true);
            timer.Value = 0;

            // Now run to compare
            compareCounter = 0;
            clockSource.Advance(TimeInterval.FromMicroseconds(compare), true);
            Assert.AreEqual(compareCounter, 1);
            Assert.AreEqual(timer.Value, compare);
            Assert.AreEqual(timer.Compare, compare);
        }

        [Test]
        public void ShouldNotGenerateAdditionalCompareEvent()
        {
            var limit = 300UL;
            var compare = 100UL;
            var compareCounter = 0;
            var clockSource = new BaseClockSource();
            var timer = new ComparingTimer(clockSource, frequency: 1000000, owner: null,
                  localName: String.Empty, limit: limit, enabled: true, eventEnabled: true, compare: compare);
            timer.CompareReached += delegate { compareCounter++; };

            Assert.AreEqual(0, compareCounter);

            // Run up to the first compare
            clockSource.Advance(TimeInterval.FromMicroseconds(100), true);
            Assert.AreEqual(1, compareCounter);

            // overwrite timer's value with the same value it has;
            // this should be a nop
            timer.Value = 100;

            Assert.AreEqual(1, compareCounter);

            // Now run to the next compare event
            clockSource.Advance(TimeInterval.FromMicroseconds(300), true);
            Assert.AreEqual(2, compareCounter);
            Assert.AreEqual(timer.Value, compare);
            Assert.AreEqual(timer.Compare, compare);
        }
    }
}

