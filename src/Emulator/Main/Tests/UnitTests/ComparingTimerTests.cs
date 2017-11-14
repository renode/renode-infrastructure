//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using NUnit.Framework;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class ComparingTimerTests
    {
        [Test]
        public void ShouldThrowOnCompareHigherThanLimit()
        {
            var machine = new Machine();
            var timer = new ComparingTimer(machine, 10, 20, compare: 5);
            Assert.Throws<InvalidOperationException>(() => timer.Compare = 30);
        }

        [Test]
        public void ShouldThrowOnNegativeCompare()
        {
            var machine = new Machine();
            var timer = new ComparingTimer(machine, 10, 20, compare: 5);
            Assert.Throws<InvalidOperationException>(() => timer.Compare = -2);
        }
    }
}

