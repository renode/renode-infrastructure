//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Provides common base for <see cref="ITimeSource"> implementations.
    /// </summary>
    public abstract class TimeSourceBase : IdentifiableObject, ITimeSource, IDisposable
    {
        /// <summary>
        /// Creates new instance of time source.
        /// </summary>
        public TimeSourceBase()
        {
            virtualTimeSyncLock = new object();
            isOnSyncPhaseThreadLock = new object();

            blockingEvent = new ManualResetEvent(true);
            delayedActions = new SortedSet<DelayedTask>();
            handles = new HandlesCollection();
            stopwatch = new Stopwatch();

            hostTicksElapsed = new TimeVariantValue(10);
            virtualTicksElapsed = new TimeVariantValue(10);

            sync = new PrioritySynchronizer();

            Quantum = DefaultQuantum;

            this.Trace();
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public virtual void Dispose()
        {
            delayedActions.Clear();

            stopwatch.Stop();
            BlockHook          = null;
            StopRequested      = null;
            SyncHook           = null;
            TimePassed         = null;
            SinksReportedHook  = null;
            using(sync.HighPriority)
            {
                handles.LatchAllAndCollectGarbage();
                handles.UnlatchAll();

                foreach(var slave in handles.All)
                {
                    slave.Dispose();
                }
            }
        }

        /// <summary>
        /// Activates sources of the time source and provides an object that deactivates them on dispose.
        /// </summary>
        protected IDisposable ObtainSourceActiveState()
        {
            using(sync.HighPriority)
            {
                foreach(var slave in handles.All)
                {
                    slave.SourceSideActive = true;
                    slave.RequestStart();
                }
            }

            var result = new DisposableWrapper();
            result.RegisterDisposeAction(() =>
            {
                using(sync.HighPriority)
                {
                    foreach(var slave in handles.All)
                    {
                        slave.SourceSideActive = false;
                    }
                }
            });
            return result;
        }

        /// <summary>
        /// Starts the time source and provides an object that stops it on dispose.
        /// </summary>
        protected IDisposable ObtainStartedState()
        {
            Start();
            return new DisposableWrapper().RegisterDisposeAction(() => Stop());
        }

        /// <summary>
        /// Starts this time source and activates all associated slaves.
        /// </summary>
        /// <returns>False it the handle has already been started.</returns>
        protected bool Start()
        {
            if(isStarted)
            {
                this.Trace("Already started");
                return false;
            }

            using(sync.HighPriority)
            {
                if(isStarted)
                {
                    this.Trace("Already started");
                    return false;
                }

                stopwatch.Start();
                isStarted = true;
                return true;
            }
        }

        /// <summary>
        /// Requests start of all registered slaves.
        /// </summary>
        /// <remark>
        /// The method should be called after activating this source using <cref="ActivateSlavesSourceSize">,
        /// otherwise a race condition situation might happen.
        /// </remark>
        protected void RequestSlavesStart()
        {
            using(sync.HighPriority)
            {
                foreach(var slave in handles.All)
                {
                    slave.RequestStart();
                }
            }
        }

        /// <summary>
        /// Calls <see cref="StopRequested"/> event.
        /// </summary>
        protected void RequestStop()
        {
            StopRequested?.Invoke();
        }

        /// <summary>
        /// Stops this time source and deactivates all associated slaves.
        /// </summary>
        protected void Stop()
        {
            RequestStop();
            using(sync.HighPriority)
            {
                if(!isStarted)
                {
                    this.Trace("Not started");
                    return;
                }

                stopwatch.Stop();
                isStarted = false;
                sync.Pulse();
                blockingEvent.Set();
            }
        }

        /// <summary>
        /// Queues an action to execute in the nearest synced state.
        /// </summary>
        /// <param name="executeImmediately">Flag indicating if the action should be executed immediately when executed in already synced context or should it wait for the next synced state.</param>
        public void ExecuteInNearestSyncedState(Action<TimeStamp> what, bool executeImmediately = false)
        {
            if(IsOnSyncPhaseThread && executeImmediately)
            {
                what(new TimeStamp(ElapsedVirtualTime, Domain));
                return;
            }
            lock(delayedActions)
            {
                delayedActions.Add(new DelayedTask(what, new TimeStamp(), ++delayedTaskId));
            }
        }

        /// <summary>
        /// Queues an action to execute in the nearest synced state after <paramref name="when"> time point.
        /// </summary>
        /// <remarks>
        /// If the <see cref="when"> time stamp comes from other time domain it will be executed in the nearest synced state.
        /// </remarks>
        /// <returns>
        /// The ID of the action, which can be used to cancel it with <see cref="CancelActionToExecuteInSyncedState">.
        /// </returns>
        public ulong ExecuteInSyncedState(Action<TimeStamp> what, TimeStamp when)
        {
            lock(delayedActions)
            {
                var id = ++delayedTaskId;
                delayedActions.Add(new DelayedTask(what, when.Domain != Domain ? new TimeStamp() : when, id));
                return id;
            }
        }

        /// <summary>
        /// Removes a queued action to execute in the synced state by ID.
        /// </summary>
        /// <param name="actionId">The ID of the action to remove.</param>
        /// <returns>
        /// True if the action was successfully removed, otherwise false.
        /// </returns>
        public bool CancelActionToExecuteInSyncedState(ulong actionId)
        {
            lock(delayedActions)
            {
                return delayedActions.RemoveWhere(action => action.Id == actionId) == 1;
            }
        }

        /// <see cref="ITimeSource.RegisterSink">
        public void RegisterSink(ITimeSink sink)
        {
            using(sync.HighPriority)
            {
                var handle = new TimeHandle(this, sink) { SourceSideActive = isStarted };
                StopRequested += handle.RequestPause;
                handles.Add(handle);
#if DEBUG
                this.Trace($"Registering sink ({(sink as IIdentifiable)?.GetDescription()}) in source ({this.GetDescription()}) via handle ({handle.GetDescription()})");
#endif
                // assigning TimeHandle to a sink must be done when everything is configured, otherwise a race condition might happen (dispatcher starts its execution when time source and handle are not yet ready)
                sink.TimeHandle = handle;
            }
        }

        public IEnumerable<ITimeSink> Sinks { get { using(sync.HighPriority) { return handles.Select(x => x.TimeSink); } } }

        /// <see cref="ITimeSource.ReportHandleActive">
        public void ReportHandleActive()
        {
            blockingEvent.Set();
        }

        /// <see cref="ITimeSource.ReportTimeProgress">
        public void ReportTimeProgress()
        {
            SynchronizeVirtualTime();
        }

        private void SynchronizeVirtualTime()
        {
            lock(virtualTimeSyncLock)
            {
                if(!handles.TryGetCommonElapsedTime(out var currentCommonElapsedTime))
                {
                    return;
                }

                if(currentCommonElapsedTime == ElapsedVirtualTime)
                {
                    return;
                }

                DebugHelper.Assert(currentCommonElapsedTime > ElapsedVirtualTime, $"A slave reports time from the past! The current virtual time is {ElapsedVirtualTime}, but {currentCommonElapsedTime} has been reported");

                var timeDiff = currentCommonElapsedTime - ElapsedVirtualTime;
                this.Trace($"Reporting time passed: {timeDiff}");
                // this will update ElapsedVirtualTime
                UpdateTime(timeDiff);
                TimePassed?.Invoke(timeDiff);
            }
        }

        public override string ToString()
        {
            return string.Join("\n",
                $"Elapsed Virtual Time: {ElapsedVirtualTime}",
                $"Elapsed Host Time: {ElapsedHostTime}",
                $"Current load: {CurrentLoad}",
                $"Cumulative load: {CumulativeLoad}",
                $"State: {State}",
                $"Advance immediately: {AdvanceImmediately}",
                $"Quantum: {Quantum}");
        }

        /// <see cref="ITimeSource.Domain">
        public abstract ITimeDomain Domain { get; }

        // TODO: this name does not give a lot to a user - maybe we should rename it?
        /// <summary>
        /// Gets or sets flag indicating if the time flow should be slowed down to reflect real time or be as fast as possible.
        /// </summary>
        /// <remarks>
        /// Setting this flag to True has the same effect as setting <see cref="Performance"> to a very high value.
        /// </remarks>
        public bool AdvanceImmediately { get; set; }

        /// <summary>
        /// Gets current state of this time source.
        /// </summary>
        public TimeSourceState State { get; private set; }

        // TODO: do not allow to set Quantum of 0
        /// <see cref="ITimeSource.Quantum">
        public TimeInterval Quantum
        {
            get => quantum;
            set
            {
                if(quantum == value)
                {
                    return;
                }

                var oldQuantum = quantum;
                quantum = value;
                QuantumChanged?.Invoke(oldQuantum, quantum);
            }
        }

        /// <summary>
        /// Gets the value representing current load, i.e., value indicating how much time the emulation spends sleeping in order to match the expected <see cref="Performance">.
        /// </summary>
        /// <remarks>
        /// Value 1 means that there is no sleeping, i.e., it is not possible to execute faster. Value > 1 means that the execution is slower than expected. Value < 1 means that increasing <see cref="Performance"> will lead to faster execution.
        /// This value is calculated as an average of 10 samples.
        /// </remarks>
        public double CurrentLoad { get { lock(hostTicksElapsed) { return hostTicksElapsed.AverageValue * 1.0 / virtualTicksElapsed.AverageValue; } } }

        /// <summary>
        /// Gets the value representing load (see <see cref="CurrentLoad">) calculated from all samples.
        /// </summary>
        public double CumulativeLoad { get { lock(hostTicksElapsed) { return hostTicksElapsed.CumulativeValue * 1.0 / virtualTicksElapsed.CumulativeValue; } } }

        /// <summary>
        /// Gets the amount of virtual time elapsed from the perspective of this time source.
        /// </summary>
        /// <remarks>
        /// This is a minimum value of all associated <see cref="TimeHandle.TotalElapsedTime">.
        /// </remarks>
        public TimeInterval ElapsedVirtualTime { get { return TimeInterval.FromTicks(virtualTicksElapsed.CumulativeValue); } }

        /// <summary>
        /// Gets the amount of host time elapsed from the perspective of this time source.
        /// </summary>
        public TimeInterval ElapsedHostTime { get { return TimeInterval.FromTicks(hostTicksElapsed.CumulativeValue); } }

        /// <summary>
        /// Gets the amount that the virtual time is ahead of the host time from the perspective of this time source,
        /// or 0 if the virtual time is behind the host time.
        /// </summary>
        public TimeInterval ElapsedVirtualHostTimeDifference
        {
            get
            {
                lock(hostTicksElapsed)
                {
                    var hostTicks = hostTicksElapsed.CumulativeValue;
                    var virtualTicks = virtualTicksElapsed.CumulativeValue;
                    if(virtualTicks <= hostTicks)
                    {
                        return TimeInterval.Empty;
                    }
                    return TimeInterval.FromTicks(virtualTicks - hostTicks);
                }
            }
        }

        /// <summary>
        /// Gets the virtual time point of the nearest synchronization of all associated <see cref="ITimeHandle">.
        /// </summary>
        public TimeInterval NearestSyncPoint { get; private set; }

        /// <summary>
        /// Gets the number of synchronizations points reached so far.
        /// </summary>
        public long NumberOfSyncPoints { get; private set; }

        /// <summary>
        /// Gets or sets the flag indicating if the current thread is a safe thread executing sync phase.
        /// </summary>
        public bool IsOnSyncPhaseThread
        {
            get
            {
                lock(isOnSyncPhaseThreadLock)
                {
                    return executeThreadId == Thread.CurrentThread.ManagedThreadId;
                }
            }

            private set
            {
                lock(isOnSyncPhaseThreadLock)
                {
                    executeThreadId = value ? Thread.CurrentThread.ManagedThreadId : (int?)null;
                }
            }
        }

        /// <summary>
        /// Forces the execution phase of time sinks to be done in serial.
        /// </summary>
        /// <remarks>
        /// Using this option might reduce the performance of the execution, but ensures the determinism.
        /// </remarks>
        public bool ExecuteInSerial { get; set; }

        /// <summary>
        /// Action to be executed on every synchronization point.
        /// </summary>
        public event Action<TimeInterval> SyncHook;

        /// <summary>
        /// An event called when the time source is blocked by at least one of the sinks.
        /// </summary>
        public event Action BlockHook;

        /// <summary>
        /// An event called when no sink is in progress.
        /// </summary>
        public event Action SinksReportedHook;

        /// <summary>
        /// An event informing about the amount of passed virtual time. Might be called many times between two consecutive synchronization points.
        /// </summary>
        public event Action<TimeInterval> TimePassed;

        /// <summary>
        /// An event called when the Quantum is changed.
        /// </summary>
        public event Action<TimeInterval, TimeInterval> QuantumChanged;

        /// <summary>
        /// Execute one iteration of time-granting loop.
        /// </summary>
        /// <remarks>
        /// The steps are as follows:
        /// (1) remove and forget all slave handles that requested detaching
        /// (2) check if there are any blocked slaves; if so DO NOT grant a time interval
        /// (2.1) if there are no blocked slaves grant a new time interval to every slave
        /// (3) wait for all slaves that are relevant in this execution (it can be either all slaves or just blocked ones) until they report back
        /// (4) update elapsed virtual time
        /// (5) execute sync hook and delayed actions if any
        /// </remarks>
        /// <param name="virtualTimeElapsed">Contains the amount of virtual time that passed during execution of this method. It is the minimal value reported by a slave (i.e, some slaves can report higher/lower values).</param>
        /// <param name="timeLimit">Maximum amount of virtual time that can pass during the execution of this method. If not set, current <see cref="Quantum"> is used.</param>
        /// <returns>
        /// True if sync point has just been reached or False if the execution has been blocked.
        /// </returns>
        protected bool InnerExecute(out TimeInterval virtualTimeElapsed, TimeInterval? timeLimit = null)
        {
            if(updateNearestSyncPoint)
            {
                NearestSyncPoint += timeLimit.HasValue ? TimeInterval.Min(timeLimit.Value, Quantum) : Quantum;
                updateNearestSyncPoint = false;
                this.Trace($"Updated NearestSyncPoint to: {NearestSyncPoint}");
            }
            DebugHelper.Assert(NearestSyncPoint.Ticks >= ElapsedVirtualTime.Ticks, $"Nearest sync point set in the past: EVT={ElapsedVirtualTime} NSP={NearestSyncPoint}");

            isBlocked = false;
            var quantum = NearestSyncPoint - ElapsedVirtualTime;
            this.Trace($"Starting a loop with #{quantum.Ticks} ticks");

            SynchronizeVirtualTime();
            var elapsedVirtualTimeAtStart = ElapsedVirtualTime;

            using(sync.LowPriority)
            {
                handles.LatchAllAndCollectGarbage();
                var shouldGrantTime = handles.AreAllReadyForNewGrant;

                this.Trace($"Iteration start: slaves left {handles.ActiveCount}; will we try to grant time? {shouldGrantTime}");

                if(handles.ActiveCount > 0)
                {
                    var executor = new PhaseExecutor<LinkedListNode<TimeHandle>>();

                    if(!shouldGrantTime)
                    {
                        if(ExecuteInSerial)
                        {
                            // We only test in serial execution to ensure determinism
                            executor.RegisterTestPhase(ExecuteReadyForUnblockTestPhase);
                        }
                        executor.RegisterPhase(ExecuteUnblockPhase);
                        executor.RegisterPhase(ExecuteWaitPhase);
                    }
                    else if(quantum != TimeInterval.Empty)
                    {
                        executor.RegisterPhase(s => ExecuteGrantPhase(s, quantum));
                        executor.RegisterPhase(ExecuteWaitPhase);
                    }

                    if(ExecuteInSerial)
                    {
                        executor.ExecuteInSerial(handles.WithLinkedListNode);
                    }
                    else
                    {
                        executor.ExecuteInParallel(handles.WithLinkedListNode);
                    }

                    SynchronizeVirtualTime();
                    virtualTimeElapsed = ElapsedVirtualTime - elapsedVirtualTimeAtStart;
                }
                else
                {
                    this.Trace($"There are no slaves, updating VTE by {quantum.Ticks}");
                    // if there are no slaves just make the time pass
                    virtualTimeElapsed = quantum;

                    UpdateTime(quantum);
                    // here we must trigger `TimePassed` manually as no handles has been updated so they won't reflect the passed time
                    TimePassed?.Invoke(quantum);
                }

                handles.UnlatchAll();
            }

            SinksReportedHook?.Invoke();
            if(!isBlocked)
            {
                ExecuteSyncPhase();
                updateNearestSyncPoint = true;
            }
            else
            {
                BlockHook?.Invoke();
            }

            State = TimeSourceState.Idle;

            this.Trace($"The end of {nameof(InnerExecute)} with result={!isBlocked}");
            return !isBlocked;
        }

        private void UpdateTime(TimeInterval virtualTimeElapsed)
        {
            lock(hostTicksElapsed)
            {
                // Converting to TimeInterval truncates to whole microseconds. Convert before saving the value to
                // elapsedAtLastUpdate, as otherwise we would lose this decimal part.
                var currentTimestamp = TimeInterval.FromTimeSpan(stopwatch.Elapsed);
                var elapsedThisTime = currentTimestamp - elapsedAtLastUpdate;
                elapsedAtLastUpdate = currentTimestamp;

                this.Trace($"Updating virtual time by {virtualTimeElapsed.TotalMicroseconds} us");
                this.virtualTicksElapsed.Update(virtualTimeElapsed.Ticks);
                this.hostTicksElapsed.Update(elapsedThisTime.Ticks);
            }
        }

        /// <summary>
        /// Activates all slaves from source side perspective, i.e., tells them that there will be time granted in the nearest future.
        /// </summary>
        protected void ActivateSlavesSourceSide(bool state = true)
        {
            using(sync.HighPriority)
            {
                foreach(var slave in handles.All)
                {
                    slave.SourceSideActive = state;
                    if(state)
                    {
                        slave.RequestStart();
                    }
                }
            }
        }

        /// <summary>
        /// Deactivates all slaves from  source side perspective, i.e., tells them that there will be no grants in the nearest future.
        /// </summary>
        protected void DeactivateSlavesSourceSide()
        {
            ActivateSlavesSourceSide(false);
        }

        /// <summary>
        /// Suspends an execution of the calling thread if blocking event is set.
        /// </summary>
        /// <remarks>
        /// This is just to improve performance of the emulation - avoid spinning when any of the sinks is blocking.
        /// </remarks>
        protected void WaitIfBlocked()
        {
            // this 'if' statement and 'canBeBlocked' variable are here for performance only
            // calling `WaitOne` in every iteration can cost a lot of time;
            // waiting on 'blockingEvent' is not required for the time framework to work properly,
            // but decreases cpu usage when any handle is known to be blocking
            if(isBlocked)
            {
                // value of 'isBlocked' will be reevaluated in 'ExecuteInner' method
                blockingEvent.WaitOne(10);
                // this parameter here is kind of a hack:
                // in theory we could use an overload without timeout,
                // but there is a bug and sometimes it blocks forever;
                // this is just a simple workaround
            }
        }

        /// <summary>
        /// Forces value of elapsed virtual time and nearest sync point.
        /// </summary>
        /// <remarks>
        /// It is called when attaching a new time handle to synchronize the initial value of virtual time.
        /// </remarks>
        protected void ResetVirtualTime(TimeInterval interval)
        {
            lock(hostTicksElapsed)
            {
                Debug.Assert(ElapsedVirtualTime <= interval, $"Couldn't reset back in time from {ElapsedVirtualTime} to {interval}.");

                virtualTicksElapsed.Reset(interval.Ticks);
                NearestSyncPoint = interval;

                using(sync.HighPriority)
                {
                    foreach(var handle in handles.All)
                    {
                        handle.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Grants time interval to a single handle.
        /// </summary>
        private void ExecuteGrantPhase(LinkedListNode<TimeHandle> handle, TimeInterval quantum)
        {
            State = TimeSourceState.ReportingElapsedTime;
            handle.Value.GrantTimeInterval(quantum);
        }

        /// <summary>
        /// Unblocks a single handle allowing it to continue
        /// execution of the previously granted time interval.
        /// </summary>
        private void ExecuteUnblockPhase(LinkedListNode<TimeHandle> handle)
        {
            handle.Value.UnblockHandle();
        }

        /// <summary>
        /// Tests the given handle for readiness to be unblocked.
        /// If the handle is not ready the execution is blocked.
        /// </summary>
        private bool ExecuteReadyForUnblockTestPhase(LinkedListNode<TimeHandle> handle)
        {
            var isReady = handle.Value.IsReadyToBeUnblocked;
            isBlocked |= !isReady;
            return isReady;
        }

        /// <summary>
        /// Waits until the handle finishes its execution.
        /// </summary>
        /// <remarks>
        /// This method must be called with a <see cref="sync"/> locked.
        /// </remarks>
        private void ExecuteWaitPhase(LinkedListNode<TimeHandle> handle)
        {
            State = TimeSourceState.WaitingForReportBack;
            var result = handle.Value.WaitUntilDone(out var usedInterval);
            if(!result.IsDone)
            {
                EnterBlockedState(!handle.Value.SinkSideActive);
            }

            using(sync.HighPriority)
            {
                handles.UpdateHandle(handle);
            }
        }

        /// <summary>
        /// Sets blocking event to true.
        /// </summary>
        /// <param name="waitForEvent">Describes if we should block until external event</param>
        private void EnterBlockedState(bool waitForEvent)
        {
            isBlocked = true;
            // The blocking event is by default in the `set` state.
            // It enters the `unset` state only temporarily between calls to `EnterBlockedState` (with the wait for event flag set) and `ReportHandleActive`.
            if(waitForEvent)
            {
                blockingEvent.Reset();
            }
        }

        /// <summary>
        /// Executes sync phase actions in a safe state.
        /// </summary>
        private void ExecuteSyncPhase()
        {
            this.Trace($"Before syncpoint, EVT={ElapsedVirtualTime.Ticks}, NSP={NearestSyncPoint.Ticks}");
            // if no slave returned blocking state, sync point should be reached
            DebugHelper.Assert(ElapsedVirtualTime == NearestSyncPoint);
            this.Trace($"We are at the sync point #{NumberOfSyncPoints}");

            State = TimeSourceState.ExecutingSyncHook;

            DelayedTask[] tasksAsArray;
            TimeStamp timeNow;
            lock(delayedActions)
            {
                IsOnSyncPhaseThread = true;
                SyncHook?.Invoke(ElapsedVirtualTime);

                State = TimeSourceState.ExecutingDelayedActions;
                timeNow = new TimeStamp(ElapsedVirtualTime, Domain);
                // we are not incrementing delayedTaskId here because DelayedTask object is only created temporarily for comparison,
                // all operations on delayedActions are in the lock() blocks and delayedTaskId is never decremented so its current value
                // is greater or equal to every currently existing DelayedTask object which is what we care for in this comparison
                var tasksToExecute = delayedActions.GetViewBetween(DelayedTask.Zero, new DelayedTask(null, timeNow, delayedTaskId));
                tasksAsArray = tasksToExecute.ToArray();
                tasksToExecute.Clear();
            }

            foreach(var task in tasksAsArray)
            {
                task.What(timeNow);
            }
            IsOnSyncPhaseThread = false;
            NumberOfSyncPoints++;
        }

        // This value is dropped because it should always be false after deserialization in order to start the emulation properly,
        // otherwise starting stopwatch is omitted in TimeSourceBase.Start() method.
        // If it wasn't marked as transient it could be true when the emulation wasn't paused before serialization because it was already in a safe state.
        [Transient]
        protected volatile bool isStarted;
        protected bool isPaused;

        protected readonly HandlesCollection handles;
        protected readonly Stopwatch stopwatch;
        // we use special object for locking as it was observed that idle dispatcher thread can starve other threads when using simple lock(object)
        protected readonly PrioritySynchronizer sync;

        /// <summary>
        /// Used to request a pause on sinks before trying to acquire their locks.
        /// </summary>
        /// <remarks>
        /// Triggering this event can improve pausing efficiency by interrupting the sink execution in the middle of a quant.
        /// </remarks>
        private event Action StopRequested;

        [Antmicro.Migrant.Constructor(true)]
        private ManualResetEvent blockingEvent;

        private TimeInterval elapsedAtLastUpdate;
        private bool isBlocked;
        private bool updateNearestSyncPoint;
        private int? executeThreadId;
        private ulong delayedTaskId;
        private TimeInterval quantum;

        private readonly TimeVariantValue virtualTicksElapsed;
        private readonly TimeVariantValue hostTicksElapsed;
        private readonly SortedSet<DelayedTask> delayedActions;
        private readonly object virtualTimeSyncLock;
        private readonly object isOnSyncPhaseThreadLock;

        private static readonly TimeInterval DefaultQuantum = TimeInterval.FromMicroseconds(100);

        /// <summary>
        /// Allows locking without starvation.
        /// </summary>
        protected class PrioritySynchronizer : IdentifiableObject, IDisposable
        {
            public PrioritySynchronizer()
            {
                innerLock = new object();
            }

            /// <summary>
            /// Used to obtain lock with low priority.
            /// </summary>
            /// <remarks>
            /// Any thread already waiting on the lock with high priority is guaranteed to obtain it prior to this one.
            /// There are no guarantees for many threads with the same priority.
            /// </remarks>
            public PrioritySynchronizer LowPriority
            {
                get
                {
                    // here we assume that `highPriorityRequestPending` will be reset soon,
                    // so there is no point of using more complicated synchronization methods
                    while(highPriorityRequestPendingCounter > 0) ;
                    Monitor.Enter(innerLock);

                    return this;
                }
            }

            /// <summary>
            /// Used to obtain lock with high priority.
            /// </summary>
            /// <remarks>
            /// It is guaranteed that the thread wanting to lock with high priority will not wait indefinitely if all other threads lock with low priority.
            /// There are no guarantees for many threads with the same priority.
            /// </remarks>
            public PrioritySynchronizer HighPriority
            {
                get
                {
                    Interlocked.Increment(ref highPriorityRequestPendingCounter);
                    Monitor.Enter(innerLock);
                    Interlocked.Decrement(ref highPriorityRequestPendingCounter);
                    return this;
                }
            }

            public void Dispose()
            {
                Monitor.Exit(innerLock);
            }

            public void WaitWhile(Func<bool> condition, string reason)
            {
                innerLock.WaitWhile(condition, reason);
            }

            public void Pulse()
            {
                Monitor.PulseAll(innerLock);
            }

            private readonly object innerLock;
            private volatile int highPriorityRequestPendingCounter;
        }

        /// <summary>
        /// Represents a time-variant value.
        /// </summary>
        private class TimeVariantValue
        {
            public TimeVariantValue(int size)
            {
                buffer = new ulong[size];
            }

            /// <summary> <summary>
            /// Resets the value and clears the internal buffer.
            /// </summary>
            public void Reset(ulong value = 0)
            {
                position = 0;
                CumulativeValue = 0;
                partialSum = 0;
                Array.Clear(buffer, 0, buffer.Length);

                Update(value);
            }

            /// <summary>
            /// Updates the <see cref="RawValue">.
            /// </summary>
            public void Update(ulong value)
            {
                RawValue = value;
                CumulativeValue += value;

                partialSum += value;
                partialSum -= buffer[position];
                buffer[position] = value;
                position = (position + 1) % buffer.Length;
            }

            public ulong RawValue { get; private set; }

            /// <summary>
            /// Returns average of <see cref="RawValues"> over the last <see cref="size"> samples.
            /// </summary>
            public ulong AverageValue { get { return  (ulong)(partialSum / (ulong)buffer.Length); } }

            /// <summary>
            /// Returns total sum of all <see cref="RawValues"> so far.
            /// </summary>
            public ulong CumulativeValue { get; private set; }

            private readonly ulong[] buffer;
            private int position;
            private ulong partialSum;
        }

        /// <summary>
        /// Represents a task that is scheduled for execution in the future.
        /// </summary>
        private struct DelayedTask : IComparable<DelayedTask>
        {
            static DelayedTask()
            {
                Zero = new DelayedTask();
            }

            public DelayedTask(Action<TimeStamp> what, TimeStamp when, ulong id) : this()
            {
                What = what;
                When = when;
                Id = id;
            }

            public int CompareTo(DelayedTask other)
            {
                var result = When.TimeElapsed.CompareTo(other.When.TimeElapsed);
                return result != 0 ? result : Id.CompareTo(other.Id);
            }

            public Action<TimeStamp> What { get; private set; }

            public TimeStamp When { get; private set; }

            public static DelayedTask Zero { get; private set; }

            public ulong Id { get; }
        }

        /// <summary>
        /// Allows to execute registered actions in serial or in parallel.
        /// </summary>
        private class PhaseExecutor<T>
        {
            public PhaseExecutor()
            {
                testPhases = new List<Func<T, Boolean>>();
                phases = new List<Action<T>>();
            }

            public void RegisterPhase(Action<T> action)
            {
                phases.Add(action);
            }

            public void RegisterTestPhase(Func<T, Boolean> predicate)
            {
                testPhases.Add(predicate);
            }

            public void ExecuteInSerial(IEnumerable<T> targets)
            {
                if(phases.Count == 0)
                {
                    return;
                }
                if(!ExecuteTestPhase(targets))
                {
                    return;
                }

                foreach(var target in targets)
                {
                    foreach(var phase in phases)
                    {
                        phase(target);
                    }
                }
            }

            public void ExecuteInParallel(IEnumerable<T> targets)
            {
                if(!ExecuteTestPhase(targets))
                {
                    return;
                }
                foreach(var phase in phases)
                {
                    foreach(var target in targets)
                    {
                        phase(target);
                    }
                }
            }

            private bool ExecuteTestPhase(IEnumerable<T> targets)
            {
                foreach(var test in testPhases)
                {
                    foreach(var target in targets)
                    {
                        if(!test(target))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private readonly List<Func<T, Boolean>> testPhases;
            private readonly List<Action<T>> phases;
        }
    }
}
