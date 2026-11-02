//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PWMLED : IGPIOReceiver, ILed
    {
        public PWMLED()
        {
            innerLock = new object();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                return;
            }

            lock(innerLock)
            {
                if(previousEvent != null)
                {
                    TimeInterval dt;
                    // TODO: Below `if` block is a way to avoid TimeInterval underflow and the resulting exception
                    // that occurs when the OnGPIO comes from a different thread(core) than expected.
                    // This should be fixed by making sure we always get the Timestamp from the correct thread.
                    // Until then the results will be radomly less accurate.
                    if(vts.TimeElapsed > previousEvent.Value.TimeElapsed)
                    {
                        dt = vts.TimeElapsed - previousEvent.Value.TimeElapsed;
                    }
                    else
                    {
                        dt = TimeInterval.Empty;
                    }

                    if(value)
                    {
                        // we switch to high, so up to this point it was low
                        LowTicks = dt.Ticks;
                    }
                    else
                    {
                        HighTicks = dt.Ticks;
                    }
                    State = value;
                }
                previousEvent = vts;
            }
        }

        public void Reset()
        {
            lock(innerLock)
            {
                LowTicks = 0;
                HighTicks = 0;
                previousEvent = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(PWMLED)}: HIGH {HighTicks} ({HighPercentage}%) LOW {LowTicks} ({LowPercentage}%)";
        }

        public ulong LowTicks { get; private set; }

        public ulong HighTicks { get; private set; }

        public double HighPercentage => (double)HighTicks / (HighTicks + LowTicks) * 100;

        public double LowPercentage => (double)LowTicks / (HighTicks + LowTicks) * 100;

        public bool State
        {
            get => state;

            private set
            {
                if(value == state)
                {
                    return;
                }

                state = value;
                StateChanged?.Invoke(this, state);
                this.Log(LogLevel.Noisy, "PWMLED state changed to {0}", state);
            }
        }

        public event Action<ILed, bool> StateChanged;

        private bool state;

        private TimeStamp? previousEvent;

        private readonly object innerLock;
    }
}
