//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Time;
using Antmicro.Renode.UnitTests.Utilities;
using Antmicro.Renode.Core;
using System.Threading;
using System.Collections.Concurrent;
using Antmicro.Renode.UnitTests.Mocks;
using System.IO;
using Antmicro.Renode.Peripherals;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.EventRecording;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class SyncDomainsTest
    {
        [Test]
        public void ShouldSyncMachines()
        {
            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncDomain = syncDomain;
                machines[i].SyncUnit = 10000;
            }

            var events = new ConcurrentQueue<int>();

            var thread1 = new Thread(() =>
            {
                clockSources[0].Advance(5000);
                events.Enqueue(0);
                clockSources[0].Advance(5000);

                events.Enqueue(1);
                clockSources[0].Advance(5000);
                clockSources[0].Advance(5000);

                clockSources[0].Advance(5000);
                events.Enqueue(2);
                clockSources[0].Advance(5000);
            }) { IsBackground = true };

            var thread2 = new Thread(() =>
            {
                clockSources[1].Advance(3000);
                events.Enqueue(3);
                clockSources[1].Advance(7000);

                clockSources[1].Advance(7000);
                events.Enqueue(4);
                clockSources[1].Advance(3000);

                events.Enqueue(5);
                clockSources[1].Advance(7000);
                clockSources[1].Advance(3000);
            }) { IsBackground = true };

            thread1.Start();
            thread2.Start();

            Assert.IsTrue(thread1.Join(Timeout), "Thread 1 did not finish execution.");
            Assert.IsTrue(thread2.Join(Timeout), "Thread 2 did not finish execution.");

            var eventsAsArray = events.ToArray();
            AssertOrder(0, 4, eventsAsArray);
            AssertOrder(3, 1, eventsAsArray);
            AssertOrder(1, 5, eventsAsArray);
            AssertOrder(4, 2, eventsAsArray);
        }

        [Test]
        public void ShouldSyncMachinesWithDifferentSyncUnits()
        {
            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncDomain = syncDomain;
            }
            machines[0].SyncUnit = 10;
            machines[1].SyncUnit = 5;

            var events = new ConcurrentQueue<int>();

            var thread1 = new Thread(() =>
            {
                events.Enqueue(0);
                clockSources[0].Advance(5);
                events.Enqueue(1);
                clockSources[0].Advance(5);

                events.Enqueue(3);
                clockSources[0].Advance(5);
            });

            var thread2 = new Thread(() =>
            {
                events.Enqueue(4);
                clockSources[1].Advance(5);

                events.Enqueue(5);
            });

            thread1.Start();
            thread2.Start();

            Assert.IsTrue(thread1.Join(Timeout), "Thread 1 did not finish execution.");
            Assert.IsTrue(thread2.Join(Timeout), "Thread 2 did not finish execution.");

            var eventsAsArray = events.ToArray();
            AssertOrder(0, 5, eventsAsArray);
            AssertOrder(1, 5, eventsAsArray);
            AssertOrder(4, 3, eventsAsArray);
        }

        [Test]
        public void ShouldSyncTwoMachinesAndExternal()
        {
            var externalMock = new MockExternal();
            externalMock.SyncDomain = syncDomain;

            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncUnit = 10;
                machines[i].SyncDomain = syncDomain;
            }

            var events = new ConcurrentQueue<int>();

            var thread1 = new Thread(() =>
            {
                events.Enqueue(0);
                clockSources[0].Advance(10);

                events.Enqueue(1);
            });

            var thread2 = new Thread(() =>
            {
                events.Enqueue(2);
                clockSources[1].Advance(10);

                events.Enqueue(3);
            });

            externalMock.OnNearestSync(() => events.Enqueue(4));

            thread2.Start();
            thread1.Start();

            Assert.IsTrue(thread1.Join(Timeout), "Thread 1 did not finish execution.");
            Assert.IsTrue(thread2.Join(Timeout), "Thread 2 did not finish execution.");

            var eventsAsArray = events.ToArray();
            AssertOrder(0, 3, eventsAsArray);
            AssertOrder(2, 1, eventsAsArray);
            AssertOrder(0, 4, eventsAsArray);
            AssertOrder(2, 4, eventsAsArray);
            AssertOrder(4, 1, eventsAsArray);
            AssertOrder(4, 3, eventsAsArray);
        }

        [Test]
        public void ShouldFireSyncPointHook()
        {
            var events = new ConcurrentQueue<int>();
            syncDomain.SetHookOnSyncPoint(number => events.Enqueue((int)number));

            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncUnit = 10;
                machines[i].SyncDomain = syncDomain;
            }

            var thread1 = new Thread(() =>
            {
                events.Enqueue(10);
                clockSources[0].Advance(10);

                events.Enqueue(20);
                clockSources[0].Advance(10);

                clockSources[0].Advance(10);
                events.Enqueue(30);
            });

            var thread2 = new Thread(() =>
            {
                events.Enqueue(40);
                clockSources[1].Advance(10);

                clockSources[1].Advance(10);

                clockSources[1].Advance(10);
                events.Enqueue(50);
            });

            thread1.Start();
            thread2.Start();
            Assert.IsTrue(thread1.Join(Timeout), "Thread 1 did not finish execution.");
            Assert.IsTrue(thread2.Join(Timeout), "Thread 2 did not finish execution.");

            var eventsAsArray = events.ToArray();

            AssertOrder(10, 50, eventsAsArray);
            AssertOrder(20, 50, eventsAsArray);
            AssertOrder(40, 20, eventsAsArray);
            AssertOrder(10, 0, eventsAsArray);
            AssertOrder(0, 20, eventsAsArray);
            AssertOrder(20, 1, eventsAsArray);
            AssertOrder(1, 30, eventsAsArray);
            AssertOrder(2, 30, eventsAsArray);
            AssertOrder(40, 0, eventsAsArray);
            AssertOrder(0, 50, eventsAsArray);
            AssertOrder(1, 50, eventsAsArray);
            AssertOrder(2, 50, eventsAsArray);
        }

        [Test]
        public void ShouldCancelSync()
        {
            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncDomain = syncDomain;
                machines[i].SyncUnit = 1;
            }
            machines[0].Start();

            var thread = new Thread(() => 
            { 
                try
                {
                    clockSources[0].Advance(2); 
                }
                catch(OperationCanceledException)
                {
                }
            }) { IsBackground = true };
            thread.Start();
            clockSources[1].Advance(1);

            machines[0].Pause();
            Assert.IsTrue(thread.Join(Timeout), "Thread did not finish execution.");
        }

        [Test, Ignore]
        public void ShouldNotLoseSyncWhenPaused()
        {
            for(var i = 0; i < 2; i++)
            {
                machines[i].SetClockSource(clockSources[i]);
                machines[i].SyncDomain = syncDomain;
                machines[i].SyncUnit = 1;
                machines[i].Start();
            }

            machines[0].Start();
            var thread = new Thread(() => clockSources[0].Advance(1)) { IsBackground = false };
            thread.Start();
            while(clockSources[0].CurrentValue == 0)
            {
                Thread.Yield();
            };

            machines[0].Pause();
            Assert.IsTrue(thread.Join(Timeout), "Thread did not finish execution.");
            thread = new Thread(() => machines[0].Start());
            thread.Start();
            Assert.IsFalse(thread.Join(TimeSpan.FromMilliseconds(100)), "Thread finished execution while it should have not.");
        }
        
        [Test, Repeat(5)]
        public void ShouldRecordAndPlayEvents()
        {
            var random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

            var machineThreadFunctionFactory = new Func<BaseClockSource, ThreadStart>(cSource => new ThreadStart(() =>
            {
                for(var i = 0; i < 100; i++)
                {
                    Thread.Sleep(10);
                    cSource.Advance(10);
                }
            }));

            var machineFactory = new Func<BaseClockSource, Machine>(cSource =>
            {
                var sDomain = new SynchronizationDomain();
                var result = new Machine();
                result.SetClockSource(cSource);
                result.SyncUnit = 10;
                result.SyncDomain = sDomain;
                var peripheral = new PeripheralMock(result, sDomain);
                result.SystemBus.Register(peripheral, 0.To(1));
                result.SetLocalName(peripheral, "mock");
                return result;
            });
                
            var clockSource = clockSources[0];
            PeripheralMock peripheralMock;
            Thread machineThread;

            using(var machine = machineFactory(clockSource))
            {
                peripheralMock = (PeripheralMock)machine["sysbus.mock"];
                machine.RecordTo(temporaryFile.Value, RecordingBehaviour.DomainExternal);

                machineThread = new Thread(machineThreadFunctionFactory(clockSource));
                machineThread.Start();

                var eventNo = 0;
                while(!machineThread.Join(0))
                {
                    peripheralMock.Method(eventNo++);
                    peripheralMock.MethodTwoArgs(eventNo++, 0);
                    Thread.Sleep(random.Next(30));
                }
            }

            var recordedEvents = peripheralMock.Events;
            clockSource = clockSources[1];

            using(var machine = machineFactory(clockSource))
            {
                peripheralMock = (PeripheralMock)machine["sysbus.mock"];
                machine.PlayFrom(temporaryFile.Value);

                machineThread = new Thread(machineThreadFunctionFactory(clockSource));
                machineThread.Start();
                machineThread.Join();
            }

            var playedEvents = peripheralMock.Events;
            CollectionAssert.AreEqual(recordedEvents, playedEvents);
        }

        [Test]
        public void ShouldWorkWhenSyncDomainIsEnteredTwoTimes()
        {
            machines[0].SetClockSource(clockSources[0]);
            machines[0].SyncDomain = syncDomain;
            machines[0].SyncDomain = syncDomain;
            machines[0].SyncUnit = 10;

            clockSources[0].Advance(20);
        }

        [Test]
        public void ShouldExecuteDelayedEvent()
        {
            var i = 0;
            machines[0].SetClockSource(clockSources[0]);
            machines[0].SyncDomain = syncDomain;
            machines[0].SyncUnit = 10;

            machines[0].ExecuteIn(() => i++, TimeSpan.FromTicks(5 * Antmicro.Renode.Time.Consts.TimeQuantum.Ticks));
            Assert.AreEqual(0, i);
            clockSources[0].Advance(10);
            Assert.AreEqual(1, i);
        }

        [Test]
        public void ShouldExecuteDelayedEventInEvent()
        {
            var i = 0;
            machines[0].SetClockSource(clockSources[0]);
            machines[0].SyncDomain = syncDomain;
            machines[0].SyncUnit = 10;

            machines[0].ExecuteIn(() => machines[0].ExecuteIn(() => i++), 
                TimeSpan.FromTicks(10 * Antmicro.Renode.Time.Consts.TimeQuantum.Ticks));
            Assert.AreEqual(0, i);
            clockSources[0].Advance(10);
            Assert.AreEqual(0, i);
            clockSources[0].Advance(10);
            Assert.AreEqual(1, i);
        }

        [Test]
        public void ShouldHaveDeterministicRealTimeClock()
        {
            machines[0].SetClockSource(clockSources[0]);
            machines[0].SyncDomain = syncDomain;
            machines[0].SyncUnit = 10;

            var initialValue = machines[0].GetRealTimeClockBase();

            machines[0].Start();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            Assert.AreEqual(initialValue, machines[0].GetRealTimeClockBase());

            clockSources[0].Advance(10000);
            Assert.AreNotEqual(initialValue, machines[0].GetRealTimeClockBase());            
        }

        [SetUp]
        public void SetUp()
        {
            clockSources = new LazyPool<BaseClockSource>(() => new BaseClockSource());
            machines = new LazyPool<Machine>(() => new Machine());
            syncDomain = new SynchronizationDomain();
            temporaryFile = new Lazy<string>(Path.GetTempFileName);
        }

        [TearDown]
        public void TearDown()
        {
            if(temporaryFile.IsValueCreated)
            {
                File.Delete(temporaryFile.Value);
            }
        }

        private static void AssertOrder(int before, int after, int[] events)
        {
            Assert.IsTrue(Array.IndexOf(events, before) < Array.IndexOf(events, after),
                string.Format("{0} was not before {1} in the array.", before, after));
        }


        private SynchronizationDomain syncDomain;
        private LazyPool<BaseClockSource> clockSources;
        private LazyPool<Machine> machines;
        private Lazy<string> temporaryFile;

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

        private class PeripheralMock : IBytePeripheral
        {
            public PeripheralMock(Machine machine, SynchronizationDomain syncDomain)
            {
                this.machine = machine;
                this.syncDomain = syncDomain;
                events = new Queue<Tuple<long, int>>();
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
                machine.ReportForeignEvent(counter, MethodInner);
            }

            public void MethodTwoArgs(int counter, int dummyArg)
            {
                machine.ReportForeignEvent(counter, dummyArg, MethodTwoArgsInner);
            }

            public IEnumerable<Tuple<long, int>> Events
            {
                get
                {
                    return events.ToArray();
                }
            }

            private void MethodInner(int counter)
            {
                events.Enqueue(Tuple.Create(syncDomain.SynchronizationsCount, counter));
            }

            private void MethodTwoArgsInner(int counter, int dummyArg)
            {
                events.Enqueue(Tuple.Create(syncDomain.SynchronizationsCount, counter));
            }

            private readonly Machine machine;
            private readonly SynchronizationDomain syncDomain;
            private readonly Queue<Tuple<long, int>> events;
        }
    }
}
