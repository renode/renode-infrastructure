//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents a main time source generating the time flow.
    /// </summary>
    /// <remarks>
    /// This time source can be set to run for a specified time, specified number of sync points or indefinitely.
    /// </remarks>
    public class MasterTimeSource : TimeSourceBase, IDisposable, ITimeDomain
    {
        /// <summary>
        /// Creates new master time source instance.
        /// </summary>
        public MasterTimeSource()
        {
            locker = new object();
        }

        /// <summary>
        /// Disposes all slaves and stops underlying dispatcher thread.
        /// </summary>
        public override void Dispose()
        {
            this.Trace("Disposing...");
            lock(locker)
            {
                isDisposed = true;
                // `Dispose` must be called before `Stop` as the latter waits for all `slaves` to finish (naturally or as a result of `Dispose`)
                base.Dispose();
                Stop();
            }
            this.Trace("Disposed");
        }

        /// <summary>
        /// Run the time source for a specified interval of virtual time.
        /// </summary>
        /// <remarks>
        /// This method is blocking. It can be interrupted by disposing the time source.
        /// </remarks>
        /// <param name="period">Amount of virtual time to pass.</param>
        public void RunFor(TimeInterval period)
        {
            DebugHelper.Assert(dispatcherThread == null, "Dispatcher thread should not run at this moment");

            using(ObtainStartedState())
            using(this.ObtainSourceActiveState())
            {
                while(!isDisposed && period.Ticks > 0)
                {
                    InnerExecute(out var timeElapsed, period);
                    period -= timeElapsed;
                }
            }
        }

        /// <summary>
        /// Run the time source for a specified number of synchronization points.
        /// </summary>
        /// <remarks>
        /// This method is blocking. It can be interrupted by disposing the time source.
        /// </remarks>
        /// <param name="numberOfSyncPoints">Number of synchronization points to pass (default 1).</param>
        public void Run(uint numberOfSyncPoints = 1)
        {
            DebugHelper.Assert(dispatcherThread == null, "Dispatcher thread should not run at this moment");

            using(ObtainStartedState())
            using(this.ObtainSourceActiveState())
            {
                for(var i = 0u; i < numberOfSyncPoints; i++)
                {
                    bool syncPointReached;
                    do
                    {
                        if(isDisposed)
                        {
                            break;
                        }
                        syncPointReached = InnerExecute(out var notused);
                    }
                    while(!syncPointReached);
                }
            }
        }

        /// <summary>
        /// Start the time-dispatching thread that provides new time grants in the background loop.
        /// </summary>
        /// <remarks>
        /// This method is non-blocking. In order to stop the thread call <see cref="Stop"> method.
        /// </remarks>
        public new void Start()
        {
            this.Trace("Starting...");
            lock(locker)
            {
                if(!base.Start())
                {
                    this.Trace();
                    return;
                }
                dispatcherThread = new Thread(Dispatcher) { Name = "MasterTimeSource Dispatcher", IsBackground = true };
                dispatcherThread.Start();
                this.Trace("Started");
            }
        }

        /// <summary>
        /// Stop the time-dispatching thread.
        /// </summary>
        public new void Stop()
        {
            this.Trace("Stopping...");
            lock(locker)
            {
                base.Stop();
                this.Trace("Waiting for dispatcher thread");
                dispatcherThread?.Join();
                dispatcherThread = null;
                this.Trace("Stopped");
            }
        }

        /// <see cref="ITimeSource.Domain">
        /// <remarks>
        /// The object of type <see cref="MasterTimeSource"> defines it's own time domain.
        /// </remarks>
        public override ITimeDomain Domain => this;

        private void Dispatcher()
        {
#if DEBUG
            using(this.TraceRegion("Dispatcher loop"))
#endif
            using(ObtainSourceActiveState())
            using(TimeDomainsManager.Instance.RegisterCurrentThread(() => new TimeStamp(NearestSyncPoint, Domain)))
            {
                try
                {
                    while(isStarted)
                    {
                        WaitIfBlocked();
                        InnerExecute(out var notused);
                    }
                }
                catch(Exception e)
                {
                    this.Trace(LogLevel.Error, $"Got an exception: {e.Message} @ {e.StackTrace}");
                    throw;
                }
            }
        }

        private bool isDisposed;
        [Transient]
        private Thread dispatcherThread;
        private readonly object locker;
    }
}
