//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ComparingTimer : ITimer, IPeripheral
    {
        public ComparingTimer(Machine machine, long frequency, long limit = long.MaxValue, Direction direction = Direction.Ascending,
            bool enabled = false, WorkMode workMode = WorkMode.OneShot, long compare = long.MaxValue)
        {
            if(compare > limit || compare < 0)
            {
                throw new ConstructionException(string.Format(CompareHigherThanLimitMessage, compare, limit));
            }
            clockSource = machine.ObtainClockSource();

            initialDirection = direction;
            initialFrequency = frequency;
            initialLimit = limit;
            initialCompare = compare;
            initialEnabled = enabled;
            initialWorkMode = workMode;
            InternalReset();
        }

        public bool Enabled
        {
            get
            {
                return clockSource.GetClockEntry(CompareReached).Enabled;
            }
            set
            {
                clockSource.ExchangeClockEntryWith(CompareReached, oldEntry => oldEntry.With(enabled: value));
            }
        }

        public long Value
        {
            get
            {
                var currentValue = 0L;
                clockSource.GetClockEntryInLockContext(CompareReached, entry =>
                {
                    currentValue = valueAccumulatedSoFar + entry.Value;
                });
                return currentValue;
                
            }
            set
            {
                clockSource.ExchangeClockEntryWith(CompareReached, entry => { 
                    valueAccumulatedSoFar = value;
                    Compare = compareValue;
                    return entry.With(value: 0);
                });
            }
        }

        public long Compare
        {
            get
            {
                var returnValue = 0L;
                clockSource.ExecuteInLock(() =>
                {
                    returnValue = compareValue;
                });
                return returnValue;
            }
            set
            {
                if(value > initialLimit || value < 0)
                {
                    throw new InvalidOperationException(CompareHigherThanLimitMessage.FormatWith(value, initialLimit));
                }
                clockSource.ExchangeClockEntryWith(CompareReached, entry =>
                {
                    compareValue = value;
                    // here we temporary convert to ulong since negative value will require a ClockEntry to overflow,
                    // which will occur later than reaching
                    var nextEventIn = (long)Math.Min((ulong)(compareValue - valueAccumulatedSoFar), (ulong)(initialLimit - valueAccumulatedSoFar));
                    valueAccumulatedSoFar += entry.Value;
                    return entry.With(period: nextEventIn - entry.Value, value: 0);
                });
            }
        }

        public virtual void Reset()
        {
            InternalReset();
        }

        protected virtual void OnCompare()
        {
        }

        private void CompareReached()
        {
            // since we use OneShot, timer's value is already 0 and it is disabled now
            // first we add old limit to accumulated value:
            valueAccumulatedSoFar += clockSource.GetClockEntry(CompareReached).Period;
            if(valueAccumulatedSoFar >= initialLimit && compareValue != initialLimit)
            {
                // compare value wasn't actually reached, the timer reached its limit
                // we don't trigger an event in such case
                valueAccumulatedSoFar = 0;
                clockSource.ExchangeClockEntryWith(CompareReached, entry => entry.With(period: compareValue, enabled: true));
                return;
            }
            // real compare event - then we reenable the timer with the next event marked by limit
            // which will probably be soon corrected by software
            clockSource.ExchangeClockEntryWith(CompareReached, entry => entry.With(period: initialLimit - valueAccumulatedSoFar, enabled: true));
            if(valueAccumulatedSoFar >= initialLimit)
            {
                valueAccumulatedSoFar = 0;
            }
            OnCompare();
        }

        private void InternalReset()
        {
            var clockEntry = new ClockEntry(initialCompare, ClockEntry.FrequencyToRatio(this, initialFrequency), CompareReached, initialEnabled, initialDirection, initialWorkMode)
                { Value = initialDirection == Direction.Ascending ? 0 : initialLimit };
            clockSource.ExchangeClockEntryWith(CompareReached, entry => clockEntry, () => clockEntry);
            valueAccumulatedSoFar = 0;
            compareValue = initialCompare;
        }

        private long valueAccumulatedSoFar;
        private long compareValue;

        private readonly Direction initialDirection;
        private readonly long initialFrequency;
        private readonly IClockSource clockSource;
        private readonly long initialLimit;
        private readonly WorkMode initialWorkMode;
        private readonly long initialCompare;
        private readonly bool initialEnabled;

        private const string CompareHigherThanLimitMessage = "Compare value ({0}) cannot be higher than limit ({1}) nor negative.";
    }
}

