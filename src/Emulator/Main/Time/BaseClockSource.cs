//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    public class BaseClockSource : IClockSource
    {
        public BaseClockSource(bool skipAdvancesHigherThanNearestLimit = false)
        {
            this.skipAdvancesHigherThanNearestLimit = skipAdvancesHigherThanNearestLimit;
            clockEntries = new List<ClockEntry>();
            clockEntriesUpdateHandlers = new List<UpdateHandlerDelegate>();
            unaccountedTimes = new List<TimeInterval>();
            toNotify = new List<Action>();
            nearestLimitIn = TimeInterval.Maximal;
            sync = new object();
            reupdateNeeded = new ThreadLocal<bool>();
            updateAlreadyInProgress = new ThreadLocal<bool>();
        }

        public TimeInterval NearestLimitIn
        {
            get
            {
                lock(sync)
                {
                    return nearestLimitIn;
                }
            }
        }

        public void Advance(TimeInterval time, bool immediately = false)
        {
            lock(sync)
            {
                if(time > nearestLimitIn && !skipAdvancesHigherThanNearestLimit)
                {
                    var left = time;
                    while(left.Ticks > 0)
                    {
                        var thisTurn = TimeInterval.Min(nearestLimitIn, left);
                        left -= thisTurn;
                        AdvanceInner(thisTurn, immediately);
                    }
                }
                else
                {
                    AdvanceInner(time, immediately);
                }
            }
        }

        public virtual void ExecuteInLock(Action action)
        {
            lock(sync)
            {
                action();
            }
        }

        public virtual void AddClockEntry(ClockEntry entry)
        {
            lock(sync)
            {
                if(clockEntries.FindIndex(x => x.Handler == entry.Handler) != -1)
                {
                    throw new ArgumentException("A clock entry with given handler already exists in the clock source.");
                }
                UpdateLimits();
                clockEntries.Add(entry);
                clockEntriesUpdateHandlers.Add(null);
                unaccountedTimes.Add(TimeInterval.Empty);
                UpdateUpdateHandler(clockEntries.Count - 1);
                UpdateLimits();
            }
            NotifyNumberOfEntriesChanged(clockEntries.Count - 1, clockEntries.Count);
        }

        public virtual void ExchangeClockEntryWith(Action handler, Func<ClockEntry, ClockEntry> visitor,
            Func<ClockEntry> factoryIfNonExistent)
        {
            lock(sync)
            {
                UpdateLimits();
                var indexOfEntry = clockEntries.FindIndex(x => x.Handler == handler);

                if(indexOfEntry == -1)
                {
                    if(factoryIfNonExistent != null)
                    {
                        clockEntries.Add(factoryIfNonExistent());
                        clockEntriesUpdateHandlers.Add(null);
                        unaccountedTimes.Add(TimeInterval.Empty);
                        UpdateUpdateHandler(clockEntries.Count - 1);
                    }
                    else
                    {
                        throw new KeyNotFoundException();
                    }
                }
                else
                {
                    clockEntries[indexOfEntry] = visitor(clockEntries[indexOfEntry]);
                    UpdateUpdateHandler(indexOfEntry);
                }
                UpdateLimits();
            }
        }

        public virtual ClockEntry GetClockEntry(Action handler)
        {
            lock(sync)
            {
                var i = clockEntries.IndexOf(x => x.Handler == handler);
                if(i == -1)
                {
                    throw new KeyNotFoundException();
                }
                var result = clockEntries[i];

                // Perform a full update of the clock entry we're getting
                if(!result.Enabled)
                {
                    return result;
                }
                if(updateAlreadyInProgress.Value)
                {
                    return result;
                }
                updateAlreadyInProgress.Value = true;
                try
                {
                    var updateHandler = clockEntriesUpdateHandlers[i];
                    if(updateHandler(ref result, elapsed + unaccountedTimes[i], ref nearestLimitIn))
                    {
                        result.Handler();
                    }
                    // This elapsed time is now accounted for this entry so clear it
                    unaccountedTimes[i] = TimeInterval.Empty;
                }
                finally
                {
                    updateAlreadyInProgress.Value = false;
                }
                clockEntries[i] = result;

                // Clear elapsed and deposit it as unaccounted time on all other enabled clock entries
                var triggerFullUpdate = false;
                for(int j = 0; j < clockEntries.Count; ++j)
                {
                    if(i != j && clockEntries[j].Enabled)
                    {
                        unaccountedTimes[j] += elapsed;
                        triggerFullUpdate |= unaccountedTimes[j] >= nearestLimitIn;
                    }
                }
                elapsed = TimeInterval.Empty;

                if(triggerFullUpdate)
                {
                    UpdateLimits();
                    // unaccountedTimes cleared by Update
                    result = clockEntries[i];
                }

                return result;
            }
        }

        public virtual void GetClockEntryInLockContext(Action handler, Action<ClockEntry> visitor)
        {
            lock(sync)
            {
                UpdateLimits();
                var result = clockEntries.FirstOrDefault(x => x.Handler == handler);
                if(result.Handler == null)
                {
                    throw new KeyNotFoundException();
                }
                visitor(result);
            }
        }

        public IEnumerable<ClockEntry> GetAllClockEntries()
        {
            lock(sync)
            {
                UpdateLimits();
                return clockEntries.ToList();
            }
        }

        public virtual bool TryRemoveClockEntry(Action handler)
        {
            int oldCount;
            lock(sync)
            {
                oldCount = clockEntries.Count;
                var indexToRemove = clockEntries.FindIndex(x => x.Handler == handler);
                if(indexToRemove == -1)
                {
                    return false;
                }
                UpdateLimits();
                clockEntries.RemoveAt(indexToRemove);
                clockEntriesUpdateHandlers.RemoveAt(indexToRemove);
                unaccountedTimes.RemoveAt(indexToRemove);
                UpdateLimits();
            }
            NotifyNumberOfEntriesChanged(oldCount, clockEntries.Count);
            return true;
        }

        public virtual TimeInterval CurrentValue
        {
            get
            {
                return totalElapsed;
            }
        }

        public virtual IEnumerable<ClockEntry> EjectClockEntries()
        {
            int oldCount;
            IEnumerable<ClockEntry> result;
            lock(sync)
            {
                oldCount = clockEntries.Count;
                result = clockEntries.ToArray();
                clockEntries.Clear();
                clockEntriesUpdateHandlers.Clear();
                unaccountedTimes.Clear();
            }
            NotifyNumberOfEntriesChanged(oldCount, 0);
            return result;
        }

        public void AddClockEntries(IEnumerable<ClockEntry> entries)
        {
            lock(sync)
            {
                foreach(var entry in entries)
                {
                    AddClockEntry(entry);
                }
            }
        }

        public bool HasEntries
        {
            get
            {
                lock(sync)
                {
                    return clockEntries.Count > 0;
                }
            }
        }

        public event Action<int, int> NumberOfEntriesChanged;

        private static bool HandleDirectionDescendingPositiveRatio(ref ClockEntry entry, TimeInterval time, ref TimeInterval nearestTickIn)
        {
            var emulatorTicks = time.Ticks;
            var entryTicks = emulatorTicks * entry.Ratio + entry.ValueResiduum;
            var isReached = entryTicks.Integer >= entry.Value;
            entry.ValueResiduum = entryTicks.Fractional;
            if(isReached)
            {
                entry.Value = entry.Period;
                entry.ValueResiduum = Fraction.Zero;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }
            else
            {
                entry.Value -= entryTicks.Integer;
            }

            var emulatorTicksToLimit = (entry.Value - entry.ValueResiduum) / entry.Ratio;
            var wholeTicksToLimit = emulatorTicksToLimit.Integer;
            if(wholeTicksToLimit < uint.MaxValue && emulatorTicksToLimit.Fractional.Numerator != 0)
            {
                wholeTicksToLimit += 1;
            }
            nearestTickIn = nearestTickIn.WithTicksMin(wholeTicksToLimit);
            return isReached;
        }

        private static bool HandleDirectionAscendingPositiveRatio(ref ClockEntry entry, TimeInterval time, ref TimeInterval nearestTickIn)
        {
            var emulatorTicks = time.Ticks;
            var entryTicks = emulatorTicks * entry.Ratio + entry.ValueResiduum;
            var isReached = false;
            entry.Value += entryTicks.Integer;
            entry.ValueResiduum = entryTicks.Fractional;

            if(entry.Value >= entry.Period)
            {
                isReached = true;
                entry.Value = 0;
                entry.ValueResiduum = Fraction.Zero;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }

            var emulatorTicksToLimit = (entry.Period - entry.Value - entry.ValueResiduum) / entry.Ratio;
            var wholeTicksToLimit = emulatorTicksToLimit.Integer;
            if(wholeTicksToLimit < uint.MaxValue && emulatorTicksToLimit.Fractional.Numerator != 0)
            {
                wholeTicksToLimit += 1;
            }
            nearestTickIn = nearestTickIn.WithTicksMin(wholeTicksToLimit);
            return isReached;
        }

        private void AdvanceInner(TimeInterval time, bool immediately)
        {
            lock(sync)
            {
                #if DEBUG
                if(time > nearestLimitIn && !skipAdvancesHigherThanNearestLimit)
                {
                    throw new InvalidOperationException("Should not reach here.");
                }
                #endif
                elapsed += time;
                totalElapsed += time;
                if(nearestLimitIn > time && !immediately)
                {
                    // nothing happens
                    nearestLimitIn -= time;
                    return;
                }

                if(updateAlreadyInProgress.Value)
                {
                    reupdateNeeded.Value = true;
                }
                else
                {
                    var alreadyRunHandlers = new List<Action>();
                    Update(elapsed, ref alreadyRunHandlers);
                    // Check if another update was attempted in the meantime, e.g., a clock entry was updated within the handlers.
                    while(reupdateNeeded.Value)
                    {
                        reupdateNeeded.Value = false;
                        Update(TimeInterval.Empty, ref alreadyRunHandlers);
                    }
                }

                elapsed = TimeInterval.Empty;
            }
        }

        private void NotifyNumberOfEntriesChanged(int oldValue, int newValue)
        {
            var numberOfEntriesChanged = NumberOfEntriesChanged;
            if(numberOfEntriesChanged != null)
            {
                numberOfEntriesChanged(oldValue, newValue);
            }
        }

        private void UpdateLimits()
        {
            AdvanceInner(TimeInterval.Empty, true);
        }

        private void Update(TimeInterval time, ref List<Action> alreadyRunHandlers)
        {
            if(updateAlreadyInProgress.Value)
            {
                return;
            }
            try
            {
                updateAlreadyInProgress.Value = true;
                lock(sync)
                {
                    nearestLimitIn = TimeInterval.Maximal;
                    for(var i = 0; i < clockEntries.Count; i++)
                    {
                        var clockEntry = clockEntries[i];
                        var updateHandler = clockEntriesUpdateHandlers[i];
                        if(!clockEntry.Enabled)
                        {
                            continue;
                        }
                        if(updateHandler(ref clockEntry, time + unaccountedTimes[i], ref nearestLimitIn) && !alreadyRunHandlers.Contains(clockEntry.Handler))
                        {
                            toNotify.Add(clockEntry.Handler);
                        }
                        clockEntries[i] = clockEntry;
                        unaccountedTimes[i] = TimeInterval.Empty;
                    }
                }
                try
                {
                    foreach(var action in toNotify)
                    {
                        action();
                        alreadyRunHandlers.Add(action);
                    }
                }
                finally
                {
                    toNotify.Clear();
                }
            }
            finally
            {
                updateAlreadyInProgress.Value = false;
            }
        }

        private void UpdateUpdateHandler(int clockEntryIndex)
        {
            if(clockEntries[clockEntryIndex].Direction == Direction.Descending)
            {
                clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionDescendingPositiveRatio;
            }
            else
            {
                clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionAscendingPositiveRatio;
            }
        }

        [Constructor]
        private ThreadLocal<bool> reupdateNeeded;
        [Constructor]
        private ThreadLocal<bool> updateAlreadyInProgress;

        private TimeInterval nearestLimitIn;
        private TimeInterval elapsed;
        private TimeInterval totalElapsed;
        private readonly bool skipAdvancesHigherThanNearestLimit;
        private readonly List<Action> toNotify;
        private readonly List<ClockEntry> clockEntries;
        private readonly List<UpdateHandlerDelegate> clockEntriesUpdateHandlers;
        private readonly List<TimeInterval> unaccountedTimes;
        private readonly object sync;

        private delegate bool UpdateHandlerDelegate(ref ClockEntry entry, TimeInterval time, ref TimeInterval nearestTickIn);
    }
}

