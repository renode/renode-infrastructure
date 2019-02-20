//
// Copyright (c) 2010-2018 Antmicro
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

            clockSource.Advance(TimeInterval.FromTicks(65535), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromTicks(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            compareCounter = 0;

            clockSource.Advance(TimeInterval.FromTicks(16304), true);
            Assert.AreEqual(16304, timer.Value);
            Assert.AreEqual(65535, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            timer.Compare = 0;
            Assert.AreEqual(16304, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            clockSource.Advance(TimeInterval.FromTicks(65535 - 16304), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(0, compareCounter);

            clockSource.Advance(TimeInterval.FromTicks(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromTicks(65535), true);
            Assert.AreEqual(65535, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(1, compareCounter);

            clockSource.Advance(TimeInterval.FromTicks(1), true);
            Assert.AreEqual(0, timer.Value);
            Assert.AreEqual(0, timer.Compare);
            Assert.AreEqual(2, compareCounter);
        }
    }
}

