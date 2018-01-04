//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class LimitTimer : ITimer, IPeripheral
    {
        public LimitTimer(Machine machine, long frequency, ulong limit = ulong.MaxValue, Direction direction = Direction.Descending,
            bool enabled = false, WorkMode workMode = WorkMode.Periodic, bool eventEnabled = false, bool autoUpdate = false, int divider = 1)
        {
            if(limit == 0)
            {
                throw new ArgumentException("Limit cannot be zero.");
            }
            irqSync = new object();
            clockSource = machine.ObtainClockSource();

            initialFrequency = frequency;
            initialLimit = limit;
            initialDirection = direction;
            initialEnabled = enabled;
            initialWorkMode = workMode;
            initialEventEnabled = eventEnabled;
            initialAutoUpdate = autoUpdate;
            initialDivider = divider;
            InternalReset();
        }

        public ulong GetValueAndLimit(out ulong currentLimit)
        {
            var clockEntry = clockSource.GetClockEntry(OnLimitReached);
            currentLimit = clockEntry.Period;
            return clockEntry.Value;
        }

        public long Frequency
        {
            get
            {
                return frequency;
            }
            set
            {
                frequency = value;
                var effectiveFrequency = frequency / Divider;
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(ratio: ClockEntry.FrequencyToRatio(this, effectiveFrequency)));
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
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(value: value));
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
                if(value == 0)
                {
                    throw new ArgumentException("Divider cannot be zero.");
                }
                divider = value;
                var effectiveFrequency = Frequency / divider;
                clockSource.ExchangeClockEntryWith(OnLimitReached, oldEntry => oldEntry.With(ratio: ClockEntry.FrequencyToRatio(this, effectiveFrequency)));
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
            }
        }

        public virtual void Reset()
        {
            InternalReset();
        }

        public event Action LimitReached;

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

            var clockEntry = new ClockEntry(initialLimit, ClockEntry.FrequencyToRatio(this, frequency / divider), OnLimitReached, initialEnabled, initialDirection, initialWorkMode) 
                { Value = initialDirection == Direction.Ascending ? 0 : initialLimit };

            clockSource.ExchangeClockEntryWith(OnLimitReached, x => clockEntry, () => clockEntry);
            EventEnabled = initialEventEnabled;
            AutoUpdate = initialAutoUpdate;
            rawInterrupt = false;
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

        private bool eventEnabled;
        private bool rawInterrupt;
        private long frequency;
        private int divider;
    }
}

