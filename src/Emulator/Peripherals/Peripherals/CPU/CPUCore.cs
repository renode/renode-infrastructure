//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class CPUCore : IdentifiableObject, IEmulationElement
    {
        public virtual void Start()
        {
            Resume();
        }

        public void Resume()
        {
            lock(pauseLock)
            {
                if(isAborted || !isPaused)
                {
                    return;
                }
                started = true;
                isPaused = false;
                OnResume();
                this.NoisyLog("Resumed.");
            }
        }

        public void Pause()
        {
            if(isAborted || isPaused)
            {
                // cpu is already paused or aborted
                return;
            }

            lock(pauseLock)
            {
                OnPause();
            }
        }

        public virtual bool IsHalted { get; set; }

        /// <summary>
        /// An ID that can identify a CPU in a multicore environment. Its specific interpretation will depend on CPU architecture.
        /// </summary>
        /// <remarks>
        /// This ID doesn't have to be either globally unique or sequential.
        /// On certain heterogeneous systems, there might be processors with duplicate IDs, which can be valid, depending on the platform configuration.
        /// </remarks>
        public uint MultiprocessingId { get; }

        public bool IsStarted => started;

        protected CPUCore(uint cpuId)
        {
            MultiprocessingId = cpuId;
        }

        protected void Abort()
        {
            isAborted = true;
            this.Log(LogLevel.Error, "CPU aborted");
        }

        protected virtual void OnResume()
        {
            // by default do nothing
        }

        protected virtual void OnPause()
        {
            // by default do nothing
        }

        [Transient]
        protected volatile bool started;

        protected volatile bool isAborted;
        protected volatile bool isPaused;

        protected readonly object pauseLock = new object();
        protected readonly object haltedLock = new object();
    }
}