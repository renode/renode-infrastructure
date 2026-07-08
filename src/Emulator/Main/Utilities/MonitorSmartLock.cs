//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Threading;

namespace Antmicro.Renode.Utilities
{
    /// <summary>
    /// Simple lock wrapper, which prevents thread
    /// from entering it more than once
    /// </summary>
    public readonly ref struct MonitorSmartLock
    {
        private MonitorSmartLock(object gate)
        {
            this.gate = gate;
        }

        /// <summary>
        /// If thread is already holding lock object new instance
        /// is created immediately. In other case this method is
        /// blocking until the thread successfully enters the gate.
        /// </summary>
        /// <param name="gate">lock object</param>
        public static MonitorSmartLock Lock(object gate)
        {
            if(!Monitor.IsEntered(gate))
            {
                Monitor.Enter(gate);
                return new MonitorSmartLock(gate);
            }

            return new MonitorSmartLock(null);
        }

        /// <summary>
        /// If thread is already holding lock object new instance
        /// is created immediately. In other case this method is
        /// blocking until the thread successfully enters the gate
        /// or specified timeout ends.
        /// </summary>
        /// <param name="gate">lock object</param>
        /// <param name="millisecondsTimeout">the amount of time to wait for the lock</param>
        /// <param name="success">the result of the attempt to acquire the lock</param>
        public static MonitorSmartLock TryLock(object gate, TimeSpan millisecondsTimeout, ref bool success)
        {
            success = false;

            if(!Monitor.IsEntered(gate))
            {
                Monitor.TryEnter(gate, millisecondsTimeout, ref success);
            }

            return new MonitorSmartLock(success ? gate : null);
        }

        /// <summary>
        /// If during construction of this instance, thread locked
        /// gate object, it will be now released. Otherwise do nothing
        /// </summary>
        public void Dispose()
        {
            if(gate is not null)
            {
                Monitor.Exit(gate);
            }
        }

        private readonly object gate;
    }
}