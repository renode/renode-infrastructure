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
    public class DummySynchronizationDomain : ISynchronizationDomain
    {
        public ISynchronizer ProvideSynchronizer()
        {
            return new DummySynchronizer();
        }

        public void ExecuteOnNearestSync(Action action)
        {
            action();
        }

        public long SynchronizationsCount
        {
            get
            {
                return 0;
            }
        }

        public long SyncUnit { get; set; }

        public bool OnSyncPointThread
        {
            get
            {
                return false;
            }
        }

        private sealed class DummySynchronizer : ISynchronizer
        {
            public bool Sync()
            {
                return true;
            }

            public void CancelSync()
            {

            }

            public void RestoreSync()
            {

            }

            public void Exit()
            {

            }
        }
    }
}

