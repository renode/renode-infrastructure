//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using NUnit.Framework;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class ComparingTimerTests
    {
        [Test]
        public void ShouldThrowOnCompareHigherThanLimit()
        {
            var timer = new ComparingTimer(new BaseClockSource(), 10, 20, compare: 5);
            Assert.Throws<InvalidOperationException>(() => timer.Compare = 30);
        }
    }
}

