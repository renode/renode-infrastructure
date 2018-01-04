//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Time
{
    public interface ISynchronizationDomain
    {
        long SynchronizationsCount { get; }
        ISynchronizer ProvideSynchronizer();
        void ExecuteOnNearestSync(Action action);
        bool OnSyncPointThread { get; }
    }
}

