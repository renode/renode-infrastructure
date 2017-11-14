//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Time
{
    public abstract class SynchronizedExternalBase : ISynchronized
    {
        protected SynchronizedExternalBase()
        {
            SyncDomain = new DummySynchronizationDomain();
        }

        public ISynchronizationDomain SyncDomain { get; set; }

        protected void ExecuteOnNearestSync(Action action)
        {
            SyncDomain.ExecuteOnNearestSync(action);
        }
    }
}

