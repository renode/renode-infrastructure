//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Time;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.EventRecording;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;

namespace UnitTests
{
    [TestFixture]
    public class TimeSourceTests
    {
        [Test]
        public void ShouldHandleMultipleSinks()
        {
            const int slavesCount = 5;
            const int roundsCount = 3;

            using(var masterTimeSource = new MasterTimeSource())
            {
                var timeSinks = new SimpleTimeSink[slavesCount];
                for(int i = 0; i < slavesCount; i++)
                {
                    timeSinks[i] = new SimpleTimeSink(double.MaxValue);
                    masterTimeSource.RegisterSink(timeSinks[i]);
                }

                // the first round does not increment the time - it just triggers a sync point
                masterTimeSource.Run(roundsCount + 1);
                Assert.AreEqual(roundsCount + 1, masterTimeSource.NumberOfSyncPoints);

                for(int i = 0; i < slavesCount; i++)
                {
                    Assert.AreEqual(roundsCount * masterTimeSource.Quantum.Ticks, timeSinks[i].ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(roundsCount, timeSinks[i].NumberOfRounds);
                }
            }
        }

        [Test]
        public void ShouldNotSleepOnAdvanceImmediately()
        {
            const int slavesCount = 5;
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource { Quantum = TimeInterval.FromMilliseconds(1000), AdvanceImmediately = true })
            {
                var timeSinks = new SimpleTimeSink[slavesCount];
                for(int i = 0; i < slavesCount; i++)
                {
                    timeSinks[i] = new SimpleTimeSink(double.MaxValue);
                    timeSource.RegisterSink(timeSinks[i]);
                }
                var sw = Stopwatch.StartNew();

                // the first round does not increment the time - it just triggers a sync point
                timeSource.Run(roundsCount + 1);

                var after = sw.Elapsed;
                Assert.IsTrue(after.TotalSeconds < roundsCount);
            }
        }

        // TODO: think about those tests
        [Test, Ignore("Ignored")]
        public void ShouldCalculateCumulativeLoadForIndefinitePerformance()
        {
            const int slavesCount = 5;
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource() { Quantum = TimeInterval.FromTicks(1000000), AdvanceImmediately = true })
            {
                var timeSinks = new SimpleTimeSink[slavesCount];
                for(int i = 0; i < slavesCount; i++)
                {
                    timeSinks[i] = new SimpleTimeSink(double.MaxValue);
                    timeSource.RegisterSink(timeSinks[i]);
                }
                timeSource.Run(roundsCount);
                Assert.IsTrue(timeSource.CumulativeLoad < 0.1);
            }
        }

        [Test, Ignore("Ignored")]
        public void ShouldCalculateCumulativeLoadForHighPerformance()
        {
            const int slavesCount = 5;
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource() { Quantum = TimeInterval.FromTicks(1000000), AdvanceImmediately = false })
            {
                var timeSinks = new SimpleTimeSink[slavesCount];
                for(int i = 0; i < slavesCount; i++)
                {
                    timeSinks[i] = new SimpleTimeSink(3.0);
                    timeSource.RegisterSink(timeSinks[i]);
                }
                timeSource.Run(roundsCount);
                Assert.AreEqual(0.3, timeSource.CumulativeLoad, 0.05);
            }
        }

        [Test, Ignore("Ignored")]
        public void ShouldCalculateCumulativeLoadForLowPerformance()
        {
            const int slavesCount = 5;
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource() { Quantum = TimeInterval.FromTicks(1000000), AdvanceImmediately = false })
            {
                var timeSinks = new SimpleTimeSink[slavesCount];
                for(int i = 0; i < slavesCount; i++)
                {
                    timeSinks[i] = new SimpleTimeSink(0.1);
                    timeSource.RegisterSink(timeSinks[i]);
                }
                timeSource.Run(roundsCount);
                Assert.AreEqual(10.0, timeSource.CumulativeLoad, 0.05);
            }
        }

        [Test]
        public void ShouldHandleSlaveTimeSourceWithSameQuantum()
        {
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource { Quantum = TimeInterval.FromTicks(100), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(100), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0);

                timeSource.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // the first round does not increment the time - it just triggers a sync point
                timeSource.Run(roundsCount + 1);

                Assert.AreEqual(roundsCount + 1, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(roundsCount + 1, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(roundsCount, timeSink.NumberOfRounds);

                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSink.ElapsedVirtualTime.Ticks);
            }
        }

        [Test]
        public void ShouldHandleSlaveTimeSourceWithDifferentQuantum()
        {
            const int roundsCount = 3;

            using(var timeSource = new MasterTimeSource { Quantum = TimeInterval.FromTicks(100), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0);

                timeSource.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // the first round does not increment the time - it just triggers a sync point
                timeSource.Run(roundsCount + 1);

                Assert.AreEqual(roundsCount + 1, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(10 * roundsCount + 1, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(10 * roundsCount, timeSink.NumberOfRounds);

                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(roundsCount * timeSource.Quantum.Ticks, timeSink.ElapsedVirtualTime.Ticks);
            }
        }

        [Test]
        public void ShouldHandleSlaveTimeSourceWithNotAlignedQuantum()
        {
            using(var timeSource = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(3), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0);

                timeSource.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // the first round does not increment the time - it just triggers a sync point
                timeSource.Run(1);

                Assert.AreEqual(1, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(0, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(0, timeSink.NumberOfRounds);

                Assert.AreEqual(0, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(0, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(0, timeSink.ElapsedVirtualTime.Ticks);

                timeSource.Run(1);

                Assert.AreEqual(2, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(4, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(3, timeSink.NumberOfRounds);

                Assert.AreEqual(10, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(9, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(9, timeSink.ElapsedVirtualTime.Ticks);

                timeSource.Run(1);

                Assert.AreEqual(3, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(7, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(6, timeSink.NumberOfRounds);

                Assert.AreEqual(20, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(18, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(18, timeSink.ElapsedVirtualTime.Ticks);

                timeSource.Run(1);

                Assert.AreEqual(4, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(11, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(10, timeSink.NumberOfRounds);

                Assert.AreEqual(30, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(30, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(30, timeSink.ElapsedVirtualTime.Ticks);
                
                timeSource.Run(1);

                Assert.AreEqual(5, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(14, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(13, timeSink.NumberOfRounds);

                Assert.AreEqual(40, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(39, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(39, timeSink.ElapsedVirtualTime.Ticks);
                
                timeSource.Run(1);

                Assert.AreEqual(6, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(17, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(16, timeSink.NumberOfRounds);

                Assert.AreEqual(50, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(48, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(48, timeSink.ElapsedVirtualTime.Ticks);
                
                timeSource.Run(1);
                
                Assert.AreEqual(7, timeSource.NumberOfSyncPoints);
                Assert.AreEqual(21, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(20, timeSink.NumberOfRounds);

                Assert.AreEqual(60, timeSource.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(60, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(60, timeSink.ElapsedVirtualTime.Ticks);
            }
        }

        [Test]
        public void ShouldNotTickDisconnectedSlaveTimeSource()
        {
            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0);

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // the first round does not increment the time - it just triggers a sync point
                master.Run(1 + 1);

                Assert.AreEqual(2, master.NumberOfSyncPoints);
                Assert.AreEqual(2, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(1, timeSink.NumberOfRounds);

                Assert.AreEqual(10, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSink.ElapsedVirtualTime.Ticks);

                timeSlave.TimeHandle.Dispose();

                master.Run(1);

                Assert.AreEqual(3, master.NumberOfSyncPoints);
                Assert.AreEqual(2, timeSlave.NumberOfSyncPoints);
                Assert.AreEqual(1, timeSink.NumberOfRounds);

                Assert.AreEqual(20, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSink.ElapsedVirtualTime.Ticks);
            }
        }

        [Test]
        public void ShouldExecuteDelayedActionInNearestSyncedStateExactlyOnce()
        {
            var time = TimeInterval.Empty;

            var alreadyDone = false;
            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0, (sts, th, ti) =>
                {
                    if(alreadyDone)
                    {
                        return true;
                    }
                    timeSlave.ExecuteInNearestSyncedState(ts =>
                    {
                        time = ts.TimeElapsed;
                    });
                    alreadyDone = true;
                    return true;
                });

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                master.Run(1);
                Assert.AreEqual(0, time.Ticks);
                Assert.AreEqual(false, alreadyDone);
                // here we must run 2 rounds as delayed actions are executed at the beginning of each round
                master.Run(1);
                Assert.AreEqual(true, alreadyDone);
                Assert.AreEqual(10, time.Ticks);
                master.Run(1);
                Assert.AreEqual(10, time.Ticks);
            }
        }

        [Test]
        public void ShouldExecuteDelayedActionAddedLaterInNearestSyncedState()
        {
            var time = TimeInterval.Empty;

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0, (sts, th, ti) =>
                {
                    if(sts.NumberOfRounds == 10)
                    {
                        timeSlave.ExecuteInNearestSyncedState(ts =>
                        {
                            time = ts.TimeElapsed;
                        });
                    }
                    return true;
                });

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // the first round does not increment the time - it just triggers a sync point
                master.Run(10 + 1);

                Assert.AreEqual(100, time.Ticks);
            }
        }

        [Test]
        public void ShouldExecuteDelayedActionInSyncedStateExactlyOnce()
        {
            var time = TimeInterval.Empty;

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0, (sts, th, ti) =>
                {
                    if(sts.NumberOfRounds != 1)
                    {
                        return true;
                    }
                    timeSlave.ExecuteInSyncedState(ts =>
                    {
                        time = timeSlave.ElapsedVirtualTime;
                    }, new TimeStamp(TimeInterval.FromTicks(30), master.Domain));
                    return true;
                });

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                master.Run(3);
                Assert.AreEqual(0, time.Ticks);
                master.Run(1);
                Assert.AreEqual(30, time.Ticks);
                master.Run(1);
                Assert.AreEqual(30, time.Ticks);
            }
        }

        [Test]
        public void ShouldExecuteNonAlignedDelayedActionInSyncedStateExactlyOnce()
        {
            var time = TimeInterval.Empty;

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0, (sts, th, ti) =>
                {
                    if(sts.NumberOfRounds != 1)
                    {
                        return true;
                    }
                    timeSlave.ExecuteInSyncedState(ts =>
                    {
                        time = timeSlave.ElapsedVirtualTime;
                    }, new TimeStamp(TimeInterval.FromTicks(31), master.Domain));
                    return true;
                });

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                master.Run(4);
                Assert.AreEqual(0, time.Ticks);
                master.Run(1);
                Assert.AreEqual(40, time.Ticks);
                master.Run(1);
                Assert.AreEqual(40, time.Ticks);
            }
        }

        [Test]
        public void ShouldUpdateExecutedTimeAfterBlocking()
        {
            var indicator = false;
            var firstTime = true;
            var threadToSinkSync = new ManualResetEvent(false);
            var sinkToThreadSync = new ManualResetEvent(false);

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            {
                var timeSink = new SimpleTimeSink(1.0, (sts, th, ti) =>
                {
                    if(firstTime)
                    {
                        Assert.AreEqual(10, ti.Ticks);
                        firstTime = false;
                        var timeUsed = TimeInterval.FromTicks(ti.Ticks / 2);
                        var timeLeft = ti - timeUsed;

                        sts.ElapsedVirtualTime += timeUsed;

                        th.ReportBackAndBreak(timeLeft);
                        sinkToThreadSync.Set();
                        threadToSinkSync.WaitOne();
                    }
                    else
                    {
                        Assert.AreEqual(5, ti.Ticks);
                        sts.ElapsedVirtualTime += ti;
                        th.ReportBackAndContinue(TimeInterval.Empty);
                    }

                    return false;
                });

                var testerThread = new TestThread(() =>
                {
                    // wait for the pause
                    sinkToThreadSync.WaitOne();

                    // here we sleep to make sure that master won't go further
                    Thread.Sleep(5000);

                    Assert.AreEqual(5, master.ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(5, timeSlave.ElapsedVirtualTime.Ticks);
                    indicator = true;

                    threadToSinkSync.Set();
                });
                testerThread.Start();

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // just to pass the first syncpoint
                master.Run(1);
                testerThread.CheckExceptions();

                master.Run(1);
                Assert.IsTrue(indicator);
                testerThread.CheckExceptions();

                Assert.AreEqual(10, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSink.ElapsedVirtualTime.Ticks);
            }
        }

        [Test]
        public void ShouldHandleTwoBlockingSinks()
        {
            var indicator = false;

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSinkA2 = new MoreComplicatedTimeSink("A"))
            using(var timeSinkB2 = new MoreComplicatedTimeSink("B"))
            {
                var ttt = new TestThread(() =>
                {
                    Parallel(
                        () =>
                        {
                            timeSinkA2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(10, ti.Ticks);
                                var timeUsed = TimeInterval.FromTicks(4);
                                var timeLeft = ti - timeUsed;
                                sts.TimeHandle.ReportBackAndBreak(timeLeft);
                            });
                        },

                        () =>
                        {
                            timeSinkB2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(10, ti.Ticks);
                                var timeUsed = TimeInterval.FromTicks(6);
                                var timeLeft = ti - timeUsed;
                                sts.TimeHandle.ReportBackAndBreak(timeLeft);
                            });
                        }
                    );

                    // here we sleep to make sure that master won't go further
                    this.Trace();
                    Thread.Sleep(5000);

                    this.Trace();
                    Assert.AreEqual(4, master.ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(4, timeSlave.ElapsedVirtualTime.Ticks);

                    Parallel(
                        () =>
                        {
                            timeSinkA2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(6, ti.Ticks);
                                var timeUsed = TimeInterval.FromTicks(4);
                                var timeLeft = ti - timeUsed;
                                sts.TimeHandle.ReportBackAndBreak(timeLeft);
                            });
                        },

                        () =>
                        {
                            timeSinkB2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(4, ti.Ticks);
                                sts.TimeHandle.ReportBackAndBreak(ti);
                            });
                        }
                    );

                    // here we sleep to make sure that master won't go further
                    this.Trace();
                    Thread.Sleep(5000);

                    this.Trace();
                    Assert.AreEqual(6, master.ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(6, timeSlave.ElapsedVirtualTime.Ticks);

                    Parallel(
                        () =>
                        {
                            timeSinkA2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(2, ti.Ticks);
                                sts.TimeHandle.ReportBackAndBreak(ti);
                            });
                        },

                        () =>
                        {
                            timeSinkB2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(4, ti.Ticks);
                                sts.TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                            });
                        }
                    );

                    // here we sleep to make sure that master won't go further
                    this.Trace();
                    Thread.Sleep(5000);

                    this.Trace();
                    Assert.AreEqual(8, master.ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(8, timeSlave.ElapsedVirtualTime.Ticks);

                    Parallel(
                        () =>
                        {
                            timeSinkA2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.AreEqual(2, ti.Ticks);
                                indicator = true;
                                sts.TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                            });
                        },

                        () =>
                        {
                            timeSinkB2.ExecuteOnDispatcherThread((sts, ti) =>
                            {
                                this.Trace();
                                Assert.Fail();
                            }, false); // do not wait for finish
                            Thread.Sleep(10000); // wait for 10s and check if Fail() is called
                        }
                    );
                })
                { Name = "tester thread" };
                ttt.Start();

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSinkA2);
                timeSlave.RegisterSink(timeSinkB2);

                // just to pass the first syncpoint
                master.Run(1);

                master.Run(1);
                Assert.IsTrue(indicator);

                this.Trace();
                Assert.AreEqual(10, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);

                ttt.Join();
            }
        }

        [Test]
        public void ShouldHandleBlockingAtTheEndOfGrantedInterval()
        {
            var indicator = false;

            using(var master = new MasterTimeSource { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSlave = new SlaveTimeSource() { Quantum = TimeInterval.FromTicks(10), AdvanceImmediately = true })
            using(var timeSink = new MoreComplicatedTimeSink("A"))
            {
                var testerThread = new TestThread(() =>
                {
                    this.Trace();
                    timeSink.ExecuteOnDispatcherThread((ts, ti) =>
                    {
                        this.Trace();
                        Assert.AreEqual(10, ti.Ticks);

                        ts.TimeHandle.ReportBackAndBreak(TimeInterval.Empty);
                    });

                    this.Trace();
                    // here we sleep to make sure that master won't go further
                    Thread.Sleep(5000);

                    this.Trace();
                    Assert.AreEqual(10, master.ElapsedVirtualTime.Ticks);
                    Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);
                    indicator = true;

                    this.Trace();
                    timeSink.ExecuteOnDispatcherThread((ts, ti) =>
                    {
                        this.Trace();
                        Assert.AreEqual(0, ti.Ticks);
                        ts.TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                    });

                    timeSink.ExecuteOnDispatcherThread((ts, ti) =>
                    {
                        this.Trace();
                        Assert.AreEqual(10, ti.Ticks);
                        ts.TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                    });
                }){ Name = "tester thread" };
                testerThread.Start();

                master.RegisterSink(timeSlave);
                timeSlave.RegisterSink(timeSink);

                // just to pass the first syncpoint
                master.Run(1);
                testerThread.CheckExceptions();

                this.Trace();
                master.Run(1);
                testerThread.CheckExceptions();
                this.Trace();
                Assert.IsTrue(indicator);
                Assert.AreEqual(10, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(10, timeSlave.ElapsedVirtualTime.Ticks);

                master.Run(1);
                Assert.AreEqual(20, master.ElapsedVirtualTime.Ticks);
                Assert.AreEqual(20, timeSlave.ElapsedVirtualTime.Ticks);
                testerThread.CheckExceptions();
            }
        }

        [Test, Repeat(5)]
        public void ShouldRecordAndPlayEvents()
        {
            var temporaryFile = TemporaryFilesManager.Instance.GetTemporaryFile();
            var machineFactory = new Func<Machine>(() =>
            {
                var result = new Machine();
                var peripheral = new PeripheralMock(result);
                result.SystemBus.Register(peripheral, 0.By(1));
                result.SetLocalName(peripheral, "mock");
                return result;
            });

            ulong evt;
            IEnumerable<Tuple<ulong, int>> recordedEvents;
            using(var machine = machineFactory())
            {
                var peripheralMock = (PeripheralMock)machine["sysbus.mock"];

                EmulationManager.Instance.Clear();
                EmulationManager.Instance.CurrentEmulation.AddMachine(machine);
                EmulationManager.Instance.CurrentEmulation.StartAll();

                machine.RecordTo(temporaryFile, RecordingBehaviour.DomainExternal);

                for(var i = 0; i < 100; i++)
                {
                    peripheralMock.Method(i);
                    peripheralMock.MethodTwoArgs(i, 0);
                    Thread.Sleep(EmulationManager.Instance.CurrentEmulation.RandomGenerator.Next(30));
                }

                EmulationManager.Instance.CurrentEmulation.PauseAll();
                evt = EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime.Ticks;
                recordedEvents = peripheralMock.Events;
                EmulationManager.Instance.CurrentEmulation.RemoveMachine(machine);
            }

            IEnumerable<Tuple<ulong, int>> playedEvents;
            using(var machine = machineFactory())
            {
                var peripheralMock = (PeripheralMock)machine["sysbus.mock"];
                EmulationManager.Instance.Clear();
                EmulationManager.Instance.CurrentEmulation.AddMachine(machine);

                machine.PlayFrom(temporaryFile);

                EmulationManager.Instance.CurrentEmulation.StartAll();

                while(EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime.Ticks < evt)
                {
                    Thread.Yield();
                }

                EmulationManager.Instance.CurrentEmulation.PauseAll();
                playedEvents = peripheralMock.Events;
            }
            CollectionAssert.AreEqual(recordedEvents, playedEvents);
            TemporaryFilesManager.Instance.Cleanup();
        }

        private static void Parallel(Action a, Action b)
        {
            var t1 = Task.Factory.StartNew(a);
            var t2 = Task.Factory.StartNew(b);

            Task.WaitAll(t1, t2);
        }

        private class MoreComplicatedTimeSink : IdentifiableObject, ITimeSink, IDisposable
        {
            public MoreComplicatedTimeSink(string name)
            {
                barrier = new AutoResetEvent(false);
                barrierBack = new AutoResetEvent(false);
                this.name = name;
            }

            public void Dispose()
            {
                isDisposed = true;
                ExecuteOnDispatcherThread(null, false);
            }

            public TimeHandle TimeHandle
            {
                get => handle;
                set
                {
                    this.handle = value;
                    dispatcherThread = new TestThread(Dispatcher) { Name = $"MoreComplicatedTimeSink: {name}" };
                    dispatcherThread.Start();
                }
            }

            public void ExecuteOnDispatcherThread(Action<ITimeSink, TimeInterval> action, bool wait = true)
            {
                this.Trace("About to execute on dispatcher thread");
                this.action = action;
                barrier.Set();
                if(wait)
                {
                    this.Trace("Waiting until execution is finished");
                    barrierBack.WaitOne();
                    this.Trace("It's finished");
                    dispatcherThread.CheckExceptions();
                }
            }

            private void Dispatcher()
            {
                this.Trace("Starting dispatcher thread");
                try
                {
                    handle.SinkSideActive = true;
                    while(true)
                    {
                        this.Trace("Waiting on barrier...");
                        barrier.WaitOne();
                        this.Trace("After barrier");
                        var a = action;
                        action = null;
                        if(a == null)
                        {
                            this.Trace("Action is null, finishing");
                            break;
                        }

                        TimeInterval timeUnits;
                        while(!handle.RequestTimeInterval(out timeUnits))
                        {
                            this.Trace("Request not granted - finishing");
                            if(isDisposed)
                            {
                                return;
                            }
                            continue;
                        }

                        this.Trace("Before action");
                        a(this, timeUnits);
                        this.Trace("Action finished");
                        barrierBack.Set();
                    }
                }
                catch(Exception e)
                {
                    this.Trace(LogLevel.Error, $"Got an exception: {e.Message} @ {e.StackTrace}");
                    throw;
                }
                finally
                {
                    handle.SinkSideActive = false;
                    this.Trace("Dispatcher thread finished");
                }
            }

            private Action<ITimeSink, TimeInterval> action;
            private TimeHandle handle;
            private bool isDisposed;
            private TestThread dispatcherThread;
            private readonly AutoResetEvent barrier;
            private readonly AutoResetEvent barrierBack;
            private readonly string name;
        }

        private class SimpleTimeSink : ITimeSink
        {
            public SimpleTimeSink(double performance, Func<SimpleTimeSink, TimeHandle, TimeInterval, bool> a = null)
            {
                this.performance = performance;
                this.action = a;
            }

            public TimeHandle TimeHandle
            {
                get { return handle; }
                set
                {
                    this.handle = value;
                    dispatcherThread = new TestThread(Dispatcher) { Name = "SimpleTimeSink Dispatcher Thread" };
                    dispatcherThread.Start();
                }
            }

            public TimeInterval ElapsedVirtualTime { get; set; }

            public long NumberOfRounds { get; private set; }

            private void Dispatcher()
            {
                TimeInterval timeUnits;
                handle.SinkSideActive = true;
                while(true)
                {
                    if(!handle.RequestTimeInterval(out timeUnits))
                    {
                        if(handle.DetachRequested)
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    NumberOfRounds++;

                    if(action != null && !action(this, handle, timeUnits))
                    {
                        continue;
                    }

                    if(performance < double.MaxValue)
                    {
                        var scaledTicks = (ulong)(timeUnits.Ticks / performance);
                        System.Threading.Thread.Sleep(TimeInterval.FromTicks(scaledTicks).ToTimeSpan());
                    }

                    ElapsedVirtualTime += timeUnits;
                    handle.ReportBackAndContinue(TimeInterval.Empty);
                }
                handle.SinkSideActive = false;
            }

            private TestThread dispatcherThread;
            private TimeHandle handle;
            private readonly double performance;
            private readonly Func<SimpleTimeSink, TimeHandle, TimeInterval, bool> action;
        }

        private class PeripheralMock : IBytePeripheral
        {
            public PeripheralMock(IMachine machine)
            {
                this.machine = machine;
                events = new Queue<Tuple<ulong, int>>();
            }

            public void Reset()
            {
            }

            public byte ReadByte(long offset)
            {
                return 0;
            }

            public void WriteByte(long offset, byte value)
            {
            }

            public void Method(int counter)
            {
                machine.HandleTimeDomainEvent(MethodInner, counter, true);
            }

            public void MethodTwoArgs(int counter, int dummyArg)
            {
                machine.HandleTimeDomainEvent(MethodTwoArgsInner, counter, dummyArg, true);
            }

            public IEnumerable<Tuple<ulong, int>> Events
            {
                get
                {
                    return events.ToArray();
                }
            }

            private void MethodInner(int counter)
            {
                events.Enqueue(Tuple.Create(machine.LocalTimeSource.ElapsedVirtualTime.Ticks, counter));
            }

            private void MethodTwoArgsInner(int counter, int dummyArg)
            {
                events.Enqueue(Tuple.Create(machine.LocalTimeSource.ElapsedVirtualTime.Ticks, counter));
            }

            private readonly IMachine machine;
            private readonly Queue<Tuple<ulong, int>> events;
        }

        private class TestThread
        {
            public TestThread(Action a)
            {
                underlyingAction = a;
            }

            public void Start()
            {
                underlyingThread = new Thread(InnerBody) { Name = this.Name };
                underlyingThread.Start();
            }

            public void Join()
            {
                underlyingThread.Join();
                CheckExceptions();
            }

            public void CheckExceptions()
            {
                if(caughtException != null)
                {
                    throw caughtException;
                }
            }

            public string Name { get; set; }

            private void InnerBody()
            {
                try
                {
                    underlyingAction();
                }
                catch(Exception e)
                {
                    caughtException = e;
                }
            }

            private readonly Action underlyingAction;
            private Exception caughtException;
            private Thread underlyingThread;
        }
    }
}
