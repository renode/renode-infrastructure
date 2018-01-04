//
// Copyright (c) 2010-2018 Antmicro
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

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class CoreTimerTest
    {

        [Test]
        public void ShouldBeAscending()
        {
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 100, 100000, Direction.Ascending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                machine.Start();
                var oldValue = 0UL;
                for(var i = 0; i < 100; i++)
                {
                    manualClockSource.AdvanceBySeconds(1);
                    var value = timer.Value;
                    Assert.Greater(value, oldValue, "Timer is not monotonic.");
                    oldValue = value;
                }
                machine.Pause();
            }
        }

        [Test]
        public void ShouldBeDescending()
        {
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 100, 100000, Direction.Descending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                machine.Start();
                var oldValue = timer.Limit;
                for(var i = 0; i < 100; i++)
                {
                    manualClockSource.AdvanceBySeconds(1);
                    var value = timer.Value;
                    Assert.Less(value, oldValue, "Timer is not monotonic.");
                    oldValue = value;
                }
                machine.Pause();
            }
        }

        [Test]
        public void ShouldNotExceedLimitAscending()
        {
            var limit = 100UL;
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 1, limit, Direction.Ascending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                machine.Start();
                manualClockSource.AdvanceBySeconds(limit - 1);
                for(var i = 0; i < 3; ++i)
                {
                    var value = timer.Value;
                    Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                    Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                    manualClockSource.AdvanceBySeconds(1);
                }
                Thread.Sleep(7);
                machine.Pause();
            }
        }

        [Test]
        public void ShouldNotExceedLimitDescending()
        {
            var limit = 100UL;
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 1, limit, Direction.Descending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                machine.Start();
                manualClockSource.AdvanceBySeconds(limit - 1);
                for(var i = 0; i < 3; i++)
                {
                    var value = timer.Value;
                    Assert.LessOrEqual(value, limit, "Timer exceeded given limit.");
                    Assert.GreaterOrEqual(value, 0, "Timer returned negative value.");
                    manualClockSource.AdvanceBySeconds(1);
                }
                machine.Pause();
            }
        }

        [Test]
        public void ShouldSwitchDirectionProperly()
        {
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 100, 100000, Direction.Ascending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                timer.EventEnabled = true;
                var ticked = false;
                timer.LimitReached += () => ticked = true;
                machine.Start();
                manualClockSource.AdvanceBySeconds(2);
                timer.Direction = Direction.Descending; // and then change the direction
                manualClockSource.AdvanceBySeconds(2);
                Assert.IsTrue(ticked);
              
            }
        }

        [Test]
        public void ShouldNotFireAlarmWhenInterruptsAreDisabled()
        {
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 1, 10, Direction.Descending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                var ticked = false;
                timer.LimitReached += () => ticked = true;
                machine.Start();
                manualClockSource.AdvanceBySeconds(11);
                Assert.IsFalse(ticked);
            }
        }

        [Test]
        public void ShouldFireAlarmWhenInterruptsAreEnabled()
        {
            using(var machine = new Machine())
            {
                var timer = new LimitTimer(machine, 1, 10, Direction.Descending, true);
                var manualClockSource = new ManualClockSource();
                machine.SetClockSource(manualClockSource);
                timer.EventEnabled = true;
                var ticked = false;
                timer.LimitReached += () => ticked = true;
                machine.Start();
                // var val =timer.Value;
                manualClockSource.AdvanceBySeconds(10);
                Assert.IsTrue(ticked);
            }
        }
    }
}

