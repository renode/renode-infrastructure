//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Threading;
using Antmicro.Renode.Time;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using System;

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
        public static TimeoutEvent EnqueueTimeoutEvent(this TimeSourceBase timeSource, ulong virtualMilliseconds,
            Action callback = null)
        {
            TimeoutEvent timeoutEvent = null;

            if(virtualMilliseconds == 0)
            {
                timeoutEvent = new TimeoutEvent(timeSource);
                timeoutEvent.Trigger();
            }
            else
            {
                var when = timeSource.ElapsedVirtualTime + TimeInterval.FromMilliseconds(virtualMilliseconds);
                var actionId = timeSource.ExecuteInSyncedState(_ =>
                {
                    callback?.Invoke();
                    timeoutEvent.Trigger();
                }, new TimeStamp(when, timeSource.Domain));
                timeoutEvent = new TimeoutEvent(timeSource, actionId);
            }

            return timeoutEvent;
        }
    }
    
    public class TimeoutEvent
    {
        public TimeoutEvent(TimeSourceBase timeSource, ulong? actionId = null)
        {
            waitHandle = new AutoResetEvent(false);
            this.timeSource = timeSource;
            this.actionId = actionId;
        }
        
        public void Trigger()
        {
            IsTriggered = true;
            waitHandle.Set();
        }

        public void Cancel()
        {
            if(actionId == null)
            {
                return;
            }
            timeSource.CancelActionToExecuteInSyncedState(actionId.Value);
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
        // We cannot serialize the timeSource but since we trigger the event after deserialization
        // anyway, it doesn't matter since it can't be canceled at that point.
        [Transient]
        private readonly TimeSourceBase timeSource;
        // We also clear out actionId to make Cancel a no-op after deserialization.
        [Transient]
        private readonly ulong? actionId;
    }
}

