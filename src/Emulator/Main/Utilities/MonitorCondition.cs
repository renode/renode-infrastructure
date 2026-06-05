//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class MonitorCondition
    {
        /// <summary>
        /// Extended version of Monitor.Wait, where only those 
        /// threads that not meet the condition will be woken up
        /// </summary>
        /// <param name="gate">Shared lock object</param>
        /// <param name="condition">Thread will sleep as long as the condition is true</param>
        public MonitorCondition(object gate, Func<bool> condition)
        {
            this.gate = gate;
            this.condition = condition;

            waiters = new();
            waitersPool = new();
        }

        /// <summary>
        /// This operation will block current thread as long as the condition is true
        /// </summary>
        /// <param name="reason">Wait reason</param>
        public void Wait(string reason)
        {
            ValidateGate();

            this.Trace($"Waiting for '{reason}'...");

            var count = 0;

            while(condition())
            {
                count++;

                var waiter = AcquireWaiter();
                waiters.Enqueue(waiter);

                Monitor.Exit(gate);

                try
                {
                    waiter.Wait();
                }
                finally
                {
                    Monitor.Enter(gate);
                }
            }

            this.Trace($"Waiting for '{reason}' finished.");

            // Main goal of this class is to reduce ammount of useless thread wake-ups.
            // If count is greater than 1 it means that the condition is too generic or
            // `Pulse` method should be used instead of `PulseAll`. First woken up thread 
            // can do something with contition result (inside lock), so next one won't be 
            // able to execute and go to sleep once again.
            if(count > 1)
            {
                Logger.Log(LogLevel.Warning, "Thread has been woken up {0} times, verify your condition", count);
            }
        }

        /// <summary>
        /// Wake up one waiting thread
        /// </summary>
        public void Pulse()
        {
            ValidateGate();
            PulseInner();
        }

        /// <summary>
        /// Wake up all waiting threads
        /// </summary>
        public void PulseAll()
        {
            ValidateGate();
            PulseAllInner();
        }

        /// <summary>
        /// Verify if condition is no longer true, then wake up waiting thread
        /// </summary>
        public void TryPulse()
        {
            ValidateGate();

            if(!condition())
            {
                PulseInner();
            }
        }

        /// <summary>
        /// Verify if condition is no longer true, then wake up all waiting threads
        /// </summary>
        public void TryPulseAll()
        {
            ValidateGate();

            if(!condition())
            {
                PulseAllInner();
            }
        }

        private void PulseInner()
        {
            if(waiters.Count == 0)
            {
                return;
            }

            ReleaseWaiter();
        }

        private void PulseAllInner()
        {
            while(waiters.Count > 0)
            {
                ReleaseWaiter();
            }
        }

        private void ValidateGate()
        {
            if(!Monitor.IsEntered(gate))
            {
                throw new SynchronizationLockException("Current context can be executed only while holding the shared gate lock");
            }
        }

        private Waiter AcquireWaiter()
        {
            if(waitersPool.TryDequeue(out var waiter))
            {
                return waiter;
            }

            return new Waiter();
        }

        private void ReleaseWaiter()
        {
            var waiter = waiters.Dequeue();
            waiter.Signal();
            waitersPool.Enqueue(waiter);
        }

        private readonly object gate;
        private readonly Func<bool> condition;
        private readonly Queue<Waiter> waiters;
        private readonly Queue<Waiter> waitersPool;

        private class Waiter
        {
            public Waiter()
            {
                semaphore = new(0, 1);
            }

            public void Wait()
            {
                semaphore.Wait();
            }

            public void Signal()
            {
                semaphore.Release();
            }

            private readonly SemaphoreSlim semaphore;
        }
    }
}
