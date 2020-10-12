//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Threading;
using Antmicro.Renode.Time;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities
{
    public static class TimeSourceBaseExtensions
    {
        // note: this method currently implements
        // the not-sooner-than behaviour which means
        // that it's guaranteed the timeout event
        // will not trigger earlier than current time
        // plus `virtualMilliseconds` (except for the
        // serialization, in which case the event will 
        // be triggered immediately);
        // technically the event is triggered in the synchronization phase,
        // so it might be delayed maximally by the size of the quantum
        public static TimeoutEvent EnqueueTimeoutEvent(this TimeSourceBase timeSource, ulong virtualMilliseconds)
        {
            var timeoutEvent = new TimeoutEvent();

            if(virtualMilliseconds == 0)
            {
                timeoutEvent.Trigger();
            }
            else
            {
                var when = timeSource.ElapsedVirtualTime + TimeInterval.FromMilliseconds(virtualMilliseconds);
                timeSource.ExecuteInSyncedState(_ =>
                {
                    timeoutEvent.Trigger();
                }, new TimeStamp(when, timeSource.Domain));
            }

            return timeoutEvent;
        }
    }
    
    public class TimeoutEvent
    {
        public TimeoutEvent()
        {
            waitHandle = new AutoResetEvent(false);
        }
        
        public void Trigger()
        {
            IsTriggered = true;
            waitHandle.Set();
        }

        public WaitHandle WaitHandle => waitHandle;

        public bool IsTriggered { get; private set; }

        [PreSerialization]
        private void PreSerialization()
        {
            // We cannot serialize `waitHandle` as
            // it internally contains an IntPtr;
            // that's why we use constructor attribute.
            // This, however, causes the original wait handle
            // to be lost - everything waiting for it
            // would timeout anyway after deserialization.
            Trigger();
        }

        [Constructor(false)]
        private AutoResetEvent waitHandle;
    }
}

