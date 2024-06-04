//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class LimitTimer : ITimer, IPeripheral
    {
        public LimitTimer(IClockSource clockSource, long frequency, IPeripheral owner, string localName, ulong limit = ulong.MaxValue, Direction direction = Direction.Descending, bool enabled = false, WorkMode workMode = WorkMode.Periodic, bool eventEnabled = false, bool autoUpdate = false, int divider = 1)
        {
            if(limit <= 0)
            {
                throw new ConstructionException("Limit must be greater than 0");
            }
            if(divider <= 0)
            {
                throw new ConstructionException("Divider must be greater than 0");
            }
            if(frequency <= 0)
            {
                throw new ConstructionException("Frequency must be greater than 0");
            }

            irqSync = new object();
            this.clockSource = clockSource;

            initialFrequency = frequency;
            initialLimit = limit;
            initialDirection = direction;
            initialEnabled = enabled;
            initialWorkMode = workMode;
            initialEventEnabled = eventEnabled;
            initialAutoUpdate = autoUpdate;
            initialDivider = divider;
            this.owner = this is IPeripheral && owner == null ? this : owner;
            this.localName = localName;
            InternalReset();
        }

        protected LimitTimer(IClockSource clockSource, long frequency, ulong limit = ulong.MaxValue, Direction direction = Direction.Descending, bool enabled = false, WorkMode workMode = WorkMode.Periodic, bool eventEnabled = false, bool autoUpdate = false, int divider = 1) 
            : this(clockSource, frequency, null, null, limit, direction, enabled, workMode, eventEnabled, autoUpdate, divider)
        {
        }

        public ulong GetValueAndLimit(out ulong currentLimit)
        {
            var clockEntry = clockSource.GetClockEntry(OnLimitReached);
            currentLimit = clockEntry.Period;
            return clockEntry.Value;
        }

        public uint Increment(ulong incrementBy)
        {
            var incValue = Value + incrementBy;

            Value = incValue % Limit;

            return (uint)(incValue / Limit);
        }

        public uint Decrement(ulong decrementBy)
        {
            var timesOverflown = (uint)(((Limit - 1 - Value) + decrementBy) / Limit);

            Value = Value + (Limit * timesOverflown) - decrementBy;

            return timesOverflown;
        }

        public long Frequency
        {
            get
            {
                return frequency;
            }
            set
            {
                if(value <= 0)
                {
                    throw new ArgumentException("Frequency must be greater than 0");
                }
                frequency = value;
                var effectiveFrequency = frequency / Divider;
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(frequency: effectiveFrequency));

                RequestReturnOnCurrentCpu();
            }
        }

        public ulong Value
        {
            get
            {
                return clockSource.GetClockEntry(OnLimitReached).Value;
            }
            set
            {
                if(value > initialLimit)
                {
                    throw new ArgumentException("Value cannot be larger than limit");
                }

                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(value: value));

                RequestReturnOnCurrentCpu();
            }
        }

        public WorkMode Mode
        {
            get
            {
                return clockSource.GetClockEntry(OnLimitReached).WorkMode;
            }
            set
            {
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(workMode: value));
            }
        }

        public int Divider
        {
            get
            {
                return divider;
            }
            set
            {
                if(value == divider)
                {
                    return;
                }
                if(value <= 0)
                {
                    throw new ArgumentException("Divider must be greater than 0");
                }
                divider = value;
                var effectiveFrequency = Frequency / divider;
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(frequency: effectiveFrequency));

                RequestReturnOnCurrentCpu();
            }
        }

        public ulong Limit
        {
            get
            {
                return clockSource.GetClockEntry(OnLimitReached).Period;
            }
            set
            {
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry =>
                {
                    if(AutoUpdate)
                    {
                        return oldEntry.With(period: value, value: oldEntry.Direction == Direction.Ascending ? 0 : value);
                    }

                    return oldEntry.With(period: value);
                }, () =>
                {
                    throw new InvalidOperationException("Should not reach here.");
                });

                RequestReturnOnCurrentCpu();
            }
        }

        public bool AutoUpdate { get; set; }

        public bool Enabled
        {
            get
            {
                return clockSource.GetClockEntry(OnLimitReached).Enabled;
            }
            set
            {
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(enabled: value),
                    () => { throw new InvalidOperationException("Should not reach here."); });
                    // should not reach here - limit should already be set in ctor

                RequestReturnOnCurrentCpu();
            }
        }

        public bool EventEnabled
        {
            get
            {
                lock(irqSync)
                {
                    return eventEnabled;
                }
            }
            set
            {
                lock(irqSync)
                {
                    eventEnabled = value;
                }
            }
        }

        public bool Interrupt
        {
            get
            {
                lock(irqSync)
                {
                    return rawInterrupt && eventEnabled;
                }
            }
        }

        public bool RawInterrupt
        {
            get
            {
                lock(irqSync)
                {
                    return rawInterrupt;
                }
            }
        }

        public void ClearInterrupt()
        {
            lock(irqSync)
            {
                rawInterrupt = false;
            }
        }

        public void ResetValue()
        {
            clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry =>
            {
                    if(oldEntry.Direction == Direction.Ascending)
                    {
                        return oldEntry.With(value: 0);
                    }
                    return oldEntry.With(value: oldEntry.Period);
            });

            RequestReturnOnCurrentCpu();
        }

        public Direction Direction
        {
            get
            {
                return clockSource.GetClockEntry(OnLimitReached).Direction;
            }
            set
            {
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(direction: value),
                    () =>
                    {
                        throw new InvalidOperationException("Should not reach here.");
                    });

                RequestReturnOnCurrentCpu();
            }
        }

        public virtual void Reset()
        {
            InternalReset();
        }

        public event Action LimitReached;

        // Should be used with caution as that clears all the subscriptions from all the sources.
        // Consequences within wider context may be difficult to predict
        protected void ClearSubscriptions()
        {
            LimitReached = null;
        }

        protected virtual void OnLimitReached()
        {
            lock(irqSync)
            {
                rawInterrupt = true;

                if (!eventEnabled)
                {
                    return;
                }

                var alarm = LimitReached;
                if(alarm != null)
                {
                    alarm();
                }
            }
        }

        private void InternalReset()
        {
            frequency = initialFrequency;
            divider = initialDivider;

            var clockEntry = new ClockEntry(initialLimit,  frequency / divider, OnLimitReached, owner, localName, initialEnabled, initialDirection, initialWorkMode)
                { Value = initialDirection == Direction.Ascending ? 0 : initialLimit };

            clockSource.ExchangeClockEntryWith(OnLimitReached, x => clockEntry, () => clockEntry);
            EventEnabled = initialEventEnabled;
            AutoUpdate = initialAutoUpdate;
            rawInterrupt = false;
        }

        private void RequestReturnOnCurrentCpu()
        {
            if(EmulationManager.Instance.CurrentEmulation.TryGetExecutionContext(out var machine, out var cpu))
            {
                (cpu as IControllableCPU)?.RequestReturn();
            }
        }

        private readonly long initialFrequency;
        private readonly WorkMode initialWorkMode;
        private readonly ulong initialLimit;
        private readonly Direction initialDirection;
        private readonly bool initialEnabled;
        private readonly IClockSource clockSource;
        private readonly object irqSync;
        private readonly bool initialEventEnabled;
        private readonly bool initialAutoUpdate;
        private readonly int initialDivider;
        private readonly IPeripheral owner;
        private readonly string localName; 

        private bool eventEnabled;
        private bool rawInterrupt;
        private long frequency;
        private int divider;
    }
}

