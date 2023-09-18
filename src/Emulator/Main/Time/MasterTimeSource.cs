//
// Copyright (c) 2010-2023 Antmicro
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
                if(isDisposed)
                {
                    this.Trace("Already disposed");
                    return;
                }
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
            EnsureDispatcherExited();

            using(ObtainStartedState())
            using(this.ObtainSourceActiveState())
            {
                while(!isDisposed && period.Ticks > 0)
                {
                    WaitIfBlocked();
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
            EnsureDispatcherExited();

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
                // Make sure the previous instance of the dispatcher thread has finished.
                // Otherwise it could keep running after we started the new one and cause
                // a tricky crash down the line.
                dispatcherThread?.Join();
                // Get a fresh cancellation token for the new thread.
                dispatcherThreadCanceller?.Dispose();
                dispatcherThreadCanceller = new CancellationTokenSource();
                dispatcherThread = new Thread(() => Dispatcher(dispatcherThreadCanceller.Token)) { Name = "MasterTimeSource Dispatcher", IsBackground = true };
                dispatcherThread.Start();
                this.Trace("Started");
            }
        }

        /// <summary>
        /// Stop the time-dispatching thread.
        /// Note that if this is called on the dispatcher thread itself from a time callback,
        /// the thread will continue running for a moment after this function returns, but it
        /// will not begin a new iteration of InnerExecute.
        /// </summary>
        public new void Stop()
        {
            this.Trace("Stopping...");
            lock(locker)
            {
                base.Stop();
                // Cancel the currently-running dispatcher thread, if any.
                dispatcherThreadCanceller?.Cancel();
                // If we're on the dispatcher thread, we are currently in the process of stopping
                // initiated by the dispatcher itself, for example as part of a hook callback.
                // In this case we can't join our own thread.
                if(dispatcherThread != null && dispatcherThread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    this.Trace("Waiting for dispatcher thread");
                    EnsureDispatcherExited();
                }
                this.Trace("Stopped");
            }
        }

        /// <see cref="ITimeSource.Domain">
        /// <remarks>
        /// The object of type <see cref="MasterTimeSource"> defines it's own time domain.
        /// </remarks>
        public override ITimeDomain Domain => this;

        private void Dispatcher(CancellationToken token)
        {
#if DEBUG
            using(this.TraceRegion("Dispatcher loop"))
#endif
            using(ObtainSourceActiveState())
            using(TimeDomainsManager.Instance.RegisterCurrentThread(() => new TimeStamp(NearestSyncPoint, Domain)))
            {
                try
                {
                    // The token will be canceled when stopping, just after isStarted gets cleared,
                    // with the crucial difference that the token is NOT shared between threads.
                    while(!token.IsCancellationRequested)
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

        private void EnsureDispatcherExited()
        {
            // We check isStarted to make sure the dispatcher thread is not supposed to be running
            // and then wait for it to exit if needed. When the time source is paused from within
            // the dispatcher, the thread object is left behind so that we can wait for it to exit
            // as needed before further time source operations.
            DebugHelper.Assert(!isStarted, "Dispatcher thread should not be set to run at this moment");
            dispatcherThread?.Join();
            dispatcherThread = null;
        }

        private bool isDisposed;
        [Transient]
        private Thread dispatcherThread;
        [Transient]
        private CancellationTokenSource dispatcherThreadCanceller;
        private readonly object locker;
    }
}
