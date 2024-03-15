//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents an object that can be used to interruptably suspend execution of the current thread for a given period.
    /// </summary>
    public class Sleeper
    {
        public Sleeper()
        {
            locker = new object();
            stopwatch = new Stopwatch();
            cancellationToken = new CancellationTokenSource();
        }

        /// <summary>
        /// Suspends execution of the current thread for <see cref="time"> period. This can be interrupted by calling <see cref="Disable"> method on this object from other thread.
        /// </summary>
        /// <returns>
        /// The flag informing if sleeping was interrupted.
        /// See <see cref="timeElapsed"> for the actual time spent sleeping before interruption.
        /// </returns>
        public bool Sleep(TimeSpan time, out TimeSpan timeElapsed, bool preserveInterruptRequest = false)
        {
            stopwatch.Restart();
            var timeLeft = time;
            var tokenSource = cancellationToken;
            this.Trace($"Asked to sleep for {timeLeft}");
            while(timeLeft.Ticks > 0 && !tokenSource.IsCancellationRequested)
            {
                this.Trace($"Sleeping for {timeLeft}");
                tokenSource.Token.WaitHandle.WaitOne(timeLeft);
                timeLeft = time - stopwatch.Elapsed;
            }
            stopwatch.Stop();
            
            timeElapsed = stopwatch.Elapsed > time ? time : stopwatch.Elapsed;
            lock(locker)
            {
                if(tokenSource.IsCancellationRequested && preserveInterruptRequest)
                {
                    // Cancel the new token so that the next Sleep will pick up the cancellation
                    // that interrupted this one
                    Disable();
                }
                return tokenSource.IsCancellationRequested;
            }
        }

        /// <summary>
        /// Disables sleeping.
        /// </summary>
        /// <remarks>
        /// All subsequent calls to <see cref="Sleep"> will finish immediately after calling this method.
        /// In order to be able to use <see cref="Sleep"> method again call to <see cref="Enable"> is necessary.
        /// </remarks>
        public void Disable()
        {
            lock(locker)
            {
                cancellationToken.Cancel();
            }
        }

        /// <summary>
        /// Enables sleeping.
        /// </summary>
        /// <remarks>
        /// Use this method to re-enable sleeping after calling <see cref="Disable">.
        /// </remarks>
        public void Enable()
        {
            lock(locker)
            {
                cancellationToken?.Cancel();
                cancellationToken = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// Interrupts sleeping.
        /// </summary>
        /// <remarks>
        /// Calling this method will wake up the sleeping thread if the sleeper is enabled.
        /// If the thread is not sleeping at the moment of calling this method, it has no effects.
        /// </remarks>
        public void Interrupt()
        {
            lock(locker)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                
                Disable();
                Enable();
            }
        }
        
        [Constructor]
        private CancellationTokenSource cancellationToken;
        private readonly Stopwatch stopwatch;
        private readonly object locker;
    }
}
