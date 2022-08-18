//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Time
{
    public interface IClockSource
    {
        void ExecuteInLock(Action action);
        void AddClockEntry(ClockEntry entry);
        void ExchangeClockEntryWith(Action handler, Func<ClockEntry, ClockEntry> visitor,
            Func<ClockEntry> factoryIfNonExistent = null);
        bool TryRemoveClockEntry(Action handler);
        ClockEntry GetClockEntry(Action handler);
        void GetClockEntryInLockContext(Action handler, Action<ClockEntry> visitor);
        IEnumerable<ClockEntry> GetAllClockEntries();
        TimeInterval CurrentValue { get; }
        IEnumerable<ClockEntry> EjectClockEntries();
        void AddClockEntries(IEnumerable<ClockEntry> entries);
    }
}

