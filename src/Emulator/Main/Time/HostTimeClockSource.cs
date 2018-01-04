//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;
using System.Diagnostics;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Time
{
    public sealed class HostTimeClockSource : BaseClockSource, IHasOwnLife
    {
        public HostTimeClockSource() : base(true)
        {
            stopwatch = new Stopwatch();
            startStopSync = new object();
            updateSync = new object();
            threadFinished = new ManualResetEventSlim();
            quickProgress = new AutoResetEvent(false);
            tokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            lock(startStopSync)
            {
                paused = true;
                stopwatch.Stop();
                CheckThread();
                lastValue = 0;
                stopwatch.Reset();
            }
        }

        public void Resume()
        {
            lock(startStopSync)
            {
                paused = false;
                stopwatch.Start();
                CheckThread();
            }
        }

        public override void AddClockEntry(ClockEntry entry)
        {
            base.AddClockEntry(entry);
            QuicklyRestartUpdateThread();
            CheckThread();
        }

        public override ClockEntry GetClockEntry(Action handler)
        {
            QuicklyRestartUpdateThread();
            return base.GetClockEntry(handler);
        }

        public override void ExchangeClockEntryWith(Action handler, Func<ClockEntry, ClockEntry> visitor,
            Func<ClockEntry> factoryIfNonExistant)
        {
            base.ExchangeClockEntryWith(handler, visitor, factoryIfNonExistant);
            QuicklyRestartUpdateThread();
        }

        public override bool RemoveClockEntry(Action handler)
        {
            var result = base.RemoveClockEntry(handler);
            QuicklyRestartUpdateThread();
            CheckThread();
            return result;
        }

        public override IEnumerable<ClockEntry> EjectClockEntries()
        {
            var result = base.EjectClockEntries();
            CheckThread();
            return result;
        }

        public override ulong CurrentValue
        {
            get
            {
                QuicklyRestartUpdateThread();
                lock(updateSync)
                {
                    return lastValue;
                }
            }
        }

        public int? UpdateThreadId
        {
            get
            {
                return updateThreadId;
            }
        }

        private void UpdateThread(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                var nearestInterruptTrigger = Consts.TimeQuantum.Multiply(Math.Min(NearestLimitIn, MaximumWaitInTicks));
                quickProgress.WaitOne(nearestInterruptTrigger);
                lock(updateSync)
                {
                    var currentValue = (ulong)(stopwatch.ElapsedTicks / Consts.TimeQuantum.Ticks);
                    var difference = currentValue - lastValue;
                    lastValue = currentValue;
                    Advance(difference);
                }
            }
            threadFinished.Set();
            updateThreadId = null;
        }

        private void CheckThread()
        {
            lock(startStopSync)
            {
                var toBeStarted = !paused && HasEntries;
                if(isStarted == toBeStarted)
                {
                    return;
                }
                if(toBeStarted)
                {
                    tokenSource = new CancellationTokenSource();
                    var thread = new Thread(() => UpdateThread(tokenSource.Token))
                    {
                        IsBackground = true,
                        Name = this.GetType().Name
                    };
                    threadFinished.Reset();
                    thread.Start();
                    updateThreadId = thread.ManagedThreadId;
                }
                else
                {
                    tokenSource.Cancel();
                    QuicklyRestartUpdateThread();
                    if(Thread.CurrentThread.ManagedThreadId != updateThreadId)
                    {
                        threadFinished.Wait();   
                    }
                }
                isStarted = toBeStarted;
            }
        }

        private void QuicklyRestartUpdateThread()
        {
            quickProgress.Set();
        }

        [Transient] // transient, since it is always 0 after pause
        private ulong lastValue;

        [Constructor]
        private ManualResetEventSlim threadFinished;

        [Constructor(false)]
        private AutoResetEvent quickProgress;

        [Transient]
        private CancellationTokenSource tokenSource;

        [Transient]
        private bool isStarted;

        [Transient]
        private bool paused;

        [Transient]
        private int? updateThreadId;

        private readonly object startStopSync;
        private readonly object updateSync;

        [Constructor]
        private readonly Stopwatch stopwatch;

        private static readonly ulong MaximumWaitInTicks = (ulong)(TimeSpan.FromSeconds(10).Ticks / Consts.TimeQuantum.Ticks);
    }
}
