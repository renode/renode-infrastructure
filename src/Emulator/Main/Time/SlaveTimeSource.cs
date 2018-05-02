//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents an intermediate entity that distribute interval granted from master time sources to other sinks.
    /// </summary>
    public class SlaveTimeSource : TimeSourceBase, ITimeSource, ITimeSink, IDisposable
    {
        /// <summary>
        /// Creates a new instance of <see cref="SlaveTimeSource">.
        /// </summary>
        public SlaveTimeSource(Machine machine)
        {
            this.machine = machine;
            locker = new object();
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public override void Dispose()
        {
            this.Trace("Disposing...");
            base.Dispose();
            base.Stop();
            lock(locker)
            {
                TimeHandle?.Dispose();
            }
            StopDispatcher();
            this.Trace("Disposed");
        }

        /// <summary>
        /// Pauses the execution of the time source. It can be resumed using <see cref="Resume"> method.
        /// </summary>
        public void Pause()
        {
            lock(locker)
            {
                this.Trace("Pausing...");
                if(!isStarted)
                {
                    this.Trace();
                    return;
                }
                if(isPaused)
                {
                    this.Trace();
                    return;
                }
                using(sync.HighPriority)
                {
                    isPaused = true;
                    DeactivateSlavesSourceSide();

                    // we must wait for unblocked slaves to finish their work
                    this.Trace("About to wait for unblocked slaves");
                    sync.WaitWhile(() => recentlyUnblockedSlaves.Count > 0, "Waiting for unblocked slaves");
                }
                this.Trace("Paused");
            }
        }

        /// <summary>
        /// Resumes execution of the time source.
        /// </summary>
        public void Resume()
        {
            this.Trace("Resuming...");
            lock(locker)
            {
                using(sync.HighPriority)
                {
                    ActivateSlavesSourceSide();
                    isPaused = false;
                }
            }
            this.Trace("Resumed");
        }

        /// <see cref="ITimeSource.Domain">
        /// <remarks>
        /// If this slave is not attached to any master time source, the domain is null.
        /// </remarks>
        public override ITimeDomain Domain { get { return timeHandle?.TimeSource.Domain; } }

        /// <see cref="ITimeSink.TimeHandle">
        /// <remarks>
        /// If this time source is already connected to a master, old handle is disposed before accepting the new one.
        /// </remarks>
        public TimeHandle TimeHandle
        {
            get
            {
                return timeHandle;
            }
            set
            {
                lock(locker)
                {
                    StopDispatcher();
                    TimeHandle?.Dispose();
                    this.Trace("About to attach to the new master");
                    timeHandle = value;
                    StartDispatcher();
                }
            }
        }

        /// <summary>
        /// Provides the implementation of time-distribution among slaves.
        /// </summary>
        private void Dispatch()
        {
            var localCopyOfTimeHandle = TimeHandle;
            try
            {
                // we must register this thread as a time provider to get current time stamp from sync hooks
                TimeDomainsManager.Instance.RegisterCurrentThread(() => new TimeStamp(localCopyOfTimeHandle.TimeSource.NearestSyncPoint, localCopyOfTimeHandle.TimeSource.Domain));

                this.Trace("Dispatcher thread started");
                ActivateSlavesSourceSide();
                firstIteration = true;
                localCopyOfTimeHandle.SinkSideActive = true;
                while(isStarted)
                {
                    WaitIfBlocked();
                    if(!localCopyOfTimeHandle.RequestTimeInterval(out var intervalGranted))
                    {
                        this.Trace("Time interval request interrupted");
                        // we will loop here a little when starting the emulation;
                        // without this sleep the CPU usage might go very high, especially when a lot of nodes are created at the same time
                        Thread.Sleep(100);
                        continue;
                    }

                    if(isPaused && recentlyUnblockedSlaves.Count == 0)
                    {
                        this.Trace("Handle paused");
                        localCopyOfTimeHandle.ReportBackAndBreak(intervalGranted);
                        continue;
                    }

                    var quantum = Quantum;
                    this.Trace($"Current QUANTUM is {quantum.Ticks} ticks");
                    var timeLeft = intervalGranted;

                    while(waitingForSlave || (timeLeft >= quantum && isStarted))
                    {
                        if(!firstIteration && !waitingForSlave)
                        {
                            NearestSyncPoint += quantum;
                        }

                        firstIteration = false;
                        waitingForSlave = false;
                        bool syncPointReached;
                        do
                        {
                            syncPointReached = InnerExecute(out var elapsed);
                            timeLeft -= elapsed;
                            if(!syncPointReached)
                            {
                                // we should not ask for time grant since the current one is not finished yet
                                waitingForSlave = true;
                                goto reportBreak;
                            }

                        }
                        while(!syncPointReached);
                    }

                    localCopyOfTimeHandle.ReportBackAndContinue(timeLeft);
                    continue;
                reportBreak:
                    localCopyOfTimeHandle.ReportBackAndBreak(timeLeft);
                }
            }
            catch(Exception e)
            {
                this.Trace(LogLevel.Error, $"Got an exception: {e.Message} {e.StackTrace}");
                throw;
            }
            finally
            {
                localCopyOfTimeHandle.SinkSideActive = false;
                this.Trace("Dispatcher thread stopped");
                DeactivateSlavesSourceSide();
                TimeDomainsManager.Instance.UnregisterCurrentThread();
            }
        }

        [PostDeserialization]
        private void StartDispatcher()
        {
            lock(locker)
            {
                if(dispatcherThread != null || TimeHandle == null)
                {
                    return;
                }

                isStarted = true;
                dispatcherThread = new Thread(Dispatch) { IsBackground = true, Name = "SlaveTimeSource Dispatcher Thread" };
                dispatcherThread.Start();
            }
        }

        private void StopDispatcher()
        {
            lock(locker)
            {
                isStarted = false;
                if(dispatcherThread != null && Thread.CurrentThread.ManagedThreadId != dispatcherThread.ManagedThreadId)
                {
                    dispatcherThread.Join();
                }
                dispatcherThread = null;
            }
        }

        [Transient]
        private Thread dispatcherThread;
        private TimeHandle timeHandle;
        private bool waitingForSlave;
        private bool firstIteration;
        private readonly Machine machine;
        private readonly object locker;
    }
}
