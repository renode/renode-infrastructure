//
// Copyright (c) 2010-2018 Antmicro
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
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Time
{
    public class BaseClockSource : IClockSource
    {
        public BaseClockSource(bool skipAdvancesHigherThanNearestLimit = false)
        {
            this.skipAdvancesHigherThanNearestLimit = skipAdvancesHigherThanNearestLimit;
            clockEntries = new List<ClockEntry>();
            clockEntriesUpdateHandlers = new List<UpdateHandlerDelegate>();
            toNotify = new List<Action>();
            nearestLimitIn = long.MaxValue;
            sync = new object();
            updateAlreadyInProgress = new ThreadLocal<bool>();
        }

        public ulong NearestLimitIn
        {
            get
            {
                lock(sync)
                {
                    return nearestLimitIn;
                }
            }
        }

        public void Advance(ulong ticks, bool immediately = false)
        {
            lock(sync)
            {
                if(ticks > nearestLimitIn && !skipAdvancesHigherThanNearestLimit)
                {
                    var left = ticks;
                    while(left > 0)
                    {
                        var thisTurn = Math.Min(nearestLimitIn, left);
                        left -= thisTurn;
                        AdvanceInner(thisTurn, immediately);
                    }
                }
                else
                {
                    AdvanceInner(ticks, immediately);
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
                clockEntries.Add(entry);
                clockEntriesUpdateHandlers.Add(null);
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
                UpdateLimits();
                var result = clockEntries.FirstOrDefault(x => x.Handler == handler);
                if(result.Handler == null)
                {
                    throw new KeyNotFoundException();
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
                return clockEntries.ToList();
            }
        }

        public virtual bool RemoveClockEntry(Action handler)
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
                clockEntries.RemoveAt(indexToRemove);
                clockEntriesUpdateHandlers.RemoveAt(indexToRemove);
                UpdateLimits();
            }
            NotifyNumberOfEntriesChanged(oldCount, clockEntries.Count);
            return true;
        }

        public virtual ulong CurrentValue
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

        private static bool HandleDirectionDescendingPositiveRatio(ref ClockEntry entry, ulong ticks, ref ulong nearestTickIn)
        {
            var ticksByRatio = ticks * (ulong)entry.Ratio;
            var isReached = ticksByRatio >= entry.Value;
            entry.ValueResiduum = 0;
            if(isReached)
            {
                entry.Value = entry.Period;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }
            else
            {
                entry.Value -= ticksByRatio;
            }

            nearestTickIn = Math.Min(nearestTickIn, (entry.Value - 1) / (ulong)entry.Ratio + 1);
            return isReached;
        }

        private static bool HandleDirectionDescendingNegativeRatio(ref ClockEntry entry, ulong ticks, ref ulong nearestTickIn)
        {
            var ratio = (ulong)(-entry.Ratio);
            var ticksByRatio = (ticks + entry.ValueResiduum) / ratio;
            var isReached = ticksByRatio >= entry.Value;
            entry.ValueResiduum = (ticks + entry.ValueResiduum) % ratio;

            if(isReached)
            {
                // TODO: maybe issue warning if its lower than zero
                entry.Value = entry.Period;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }
            else
            {
                entry.Value -= ticksByRatio;
            }

            nearestTickIn = Math.Min(nearestTickIn, entry.Value * ratio + entry.ValueResiduum);
            return isReached;
        }

        private static bool HandleDirectionAscendingPositiveRatio(ref ClockEntry entry, ulong ticks, ref ulong nearestTickIn)
        {
            var flag = false;

            entry.Value += ticks * (ulong)entry.Ratio;
            entry.ValueResiduum = 0;

            if(entry.Value >= entry.Period)
            {
                flag = true;
                entry.Value = 0;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }

            nearestTickIn = Math.Min(nearestTickIn, (entry.Period - entry.Value - 1) / (ulong)entry.Ratio + 1);
            return flag;
        }

        private static bool HandleDirectionAscendingNegativeRatio(ref ClockEntry entry, ulong ticks, ref ulong nearestTickIn)
        {
            var flag = false;
            ulong ratio = (ulong)(-entry.Ratio);

            entry.Value += (ticks + entry.ValueResiduum) / ratio;
            entry.ValueResiduum = (ticks + entry.ValueResiduum) % ratio;

            if(entry.Value >= entry.Period)
            {
                flag = true;
                entry.Value = 0;
                entry = entry.With(enabled: entry.Enabled & (entry.WorkMode != WorkMode.OneShot));
            }

            nearestTickIn = Math.Min(nearestTickIn, ((entry.Period - entry.Value) * ratio) - entry.ValueResiduum);
            return flag;
        }

        private void AdvanceInner(ulong ticks, bool immediately)
        {
            lock(sync)
            {
                #if DEBUG
                if(ticks > nearestLimitIn && !skipAdvancesHigherThanNearestLimit)
                {
                    throw new InvalidOperationException("Should not reach here.");
                }
                #endif
                elapsed += ticks;
                totalElapsed += ticks;
                if(nearestLimitIn > ticks && !immediately)
                {
                    // nothing happens
                    nearestLimitIn -= ticks;
                    return;
                }
                Update(elapsed);
                elapsed = 0;
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
            AdvanceInner(0, true);
        }

        private void Update(ulong ticks)
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
                    nearestLimitIn = ulong.MaxValue;
                    for(var i = 0; i < clockEntries.Count; i++)
                    {
                        var clockEntry = clockEntries[i];
                        var updateHandler = clockEntriesUpdateHandlers[i];
                        if(!clockEntry.Enabled)
                        {
                            continue;
                        }
                        if(updateHandler(ref clockEntry, ticks, ref nearestLimitIn))
                        {
                            toNotify.Add(clockEntry.Handler);
                        }
                        clockEntries[i] = clockEntry;
                    }
                }
                try
                {
                    foreach(var action in toNotify)
                    {
                        action();
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
                if(clockEntries[clockEntryIndex].Ratio > 0)
                {
                    clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionDescendingPositiveRatio;
                }
                else
                {
                    clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionDescendingNegativeRatio;
                }
            }
            else
            {
                if(clockEntries[clockEntryIndex].Ratio > 0)
                {
                    clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionAscendingPositiveRatio;
                }
                else
                {
                    clockEntriesUpdateHandlers[clockEntryIndex] = HandleDirectionAscendingNegativeRatio;
                }
            }
        }

        [Constructor]
        private ThreadLocal<bool> updateAlreadyInProgress;

        private ulong nearestLimitIn;
        private ulong elapsed;
        private ulong totalElapsed;
        private readonly bool skipAdvancesHigherThanNearestLimit;
        private readonly List<Action> toNotify;
        private readonly List<ClockEntry> clockEntries;
        private readonly List<UpdateHandlerDelegate> clockEntriesUpdateHandlers;
        private readonly object sync;

        private delegate bool UpdateHandlerDelegate(ref ClockEntry entry, ulong ticks, ref ulong nearestTickIn);
    }
}

