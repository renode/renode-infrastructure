//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Hooks;

namespace Antmicro.Renode.Time
{
    public static class SynchronizationExtensions
    {
        // note that this function only exists to provide access to feature
        // that is normally not accessible due to the limitations of the monitor
        public static void SetSyncDomainFromEmulation(this ISynchronized @this, int domainIndex)
        {
            @this.SyncDomain = EmulationManager.Instance.CurrentEmulation.SyncDomains[domainIndex];
        }

        // again, exists only for monitor
        public static void SetHookAtSyncPoint(this Emulation emulation, int domainIndex, string handler)
        {
            var engine = new SyncPointHookPythonEngine(handler, emulation);
            ((SynchronizationDomain)emulation.SyncDomains[domainIndex]).SetHookOnSyncPoint(engine.Hook);
        }

        // and again
        public static void ClearHookAtSyncPoint(this Emulation emulation, int domainIndex)
        {
            ((SynchronizationDomain)emulation.SyncDomains[domainIndex]).ClearHookOnSyncPoint();
        }
    }
}

