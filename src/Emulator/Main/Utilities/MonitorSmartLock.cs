//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Threading;

namespace Antmicro.Renode.Utilities
{
    /// <summary>
    /// Simple lock wrapper, which prevents thread
    /// from entering it more than once
    /// </summary>
    public readonly ref struct MonitorSmartLock
    {
        /// <summary>
        /// If thread is already holding lock object this instance
        /// is created immediately. In other case the constructor is
        /// blocking until the thread successfully enters the gate.
        /// </summary>
        /// <param name="gate">lock object</param>
        public MonitorSmartLock(object gate)
        {
            if(!Monitor.IsEntered(gate))
            {
                this.gate = gate;
                Monitor.Enter(gate);
            }
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