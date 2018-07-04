//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents a helper class used to obtain a virtual time stamp of a current thread.
    /// </summary>
    public class TimeDomainsManager
    {
        public static TimeDomainsManager Instance = new TimeDomainsManager();

        /// <summary>
        /// Registers a time stamp provider for the current thread.
        /// </summary>
        public IDisposable RegisterCurrentThread(Func<TimeStamp> f)
        {
            timeGetters.AddOrUpdate(Thread.CurrentThread.ManagedThreadId, f, (k, v) => f);
            return new DisposableWrapper().RegisterDisposeAction(() => Instance.UnregisterCurrentThread());
        }

        /// <summary>
        /// Unregisters a time stamp provider for the current thread.
        /// </summary>
        public void UnregisterCurrentThread()
        {
            var result = timeGetters.TryRemove(Thread.CurrentThread.ManagedThreadId, out var unused);
            Renode.Debugging.DebugHelper.Assert(result, "Tried to unregister an unregistered thread.");
        }

        /// <summary>
        /// Returns virtual time stamp for the current thread.
        /// </summary>
        public TimeStamp VirtualTimeStamp
        {
            get
            {
                if(!timeGetters.TryGetValue(Thread.CurrentThread.ManagedThreadId, out var timestampGenerator))
                {
                    throw new ArgumentException($"Tried to obtain a virtual time stamp of an unregistered thread: '{Thread.CurrentThread.Name}'/{Thread.CurrentThread.ManagedThreadId}");
                }
                return timestampGenerator();
            }
        }

        /// <summary>
        /// Tries to obtain virtual time stamp for the current thread.
        /// </summary>
        /// <returns><c>true</c>, if virtual time stamp was obtained, <c>false</c> otherwise.</returns>
        /// <param name="virtualTimeStamp">Virtual time stamp.</param>
        public bool TryGetVirtualTimeStamp(out TimeStamp virtualTimeStamp)
        {
            if(!timeGetters.TryGetValue(Thread.CurrentThread.ManagedThreadId, out var timestampGenerator))
            {
                virtualTimeStamp = default(TimeStamp);
                return false;
            }
            virtualTimeStamp = timestampGenerator();
            return true;
        }

        private TimeDomainsManager()
        {
            timeGetters = new ConcurrentDictionary<int, Func<TimeStamp>>();
        }

        private readonly ConcurrentDictionary<int, Func<TimeStamp>> timeGetters;
    }
}
