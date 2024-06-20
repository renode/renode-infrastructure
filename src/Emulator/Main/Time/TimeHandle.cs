//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.Threading;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Handle used for synchronization and communication between <see cref="ITimeSource"> and <see cref="ITimeSink">.
    /// </summary>
    public class TimeHandle : IdentifiableObject
    {
        // -------
        // The objects of this class are used to synchronize execution of `time sources` and `time sinks`.
        //
        // [SOURCE SIDE]                                   [SINK SIDE]
        //                                   Dispose
        //                                      |
        //                                      V
        //                                 +--------+
        // Latch                       ->  |        |
        // ...                             |        |
        // (Grant / Unblock + (Latch)) ->  |        |  <-  Request* + (Latch)
        // ...                             |  Time  |      ...
        // WaitUntilDone* + (Unlatch)  ->  | Handle |  <-  ReportBreak / ReportContinue
        // ...                             |        |
        // Unlatch                     ->  |        |
        //                                 |        |
        //                                 +--------+
        //                             --- properties ---
        //                                 +--------+
        // SourceSideActive            =   |        |  =   SinkSideActive
        //                                 |        |  =   Enabled
        //                                 +--------+
        //
        //
        // Methods marked with '*' are blocking:
        // * `Request` will block until `Grant` or `Unblock`
        // * `WaitUntilDone` will block until `ReportBreak` or `ReportContinue`
        //
        // Methods surrounded with '()' are executed conditionally:
        // * `Latch` as a result of `Request` is executed only if this is the first `Request` after `ReportBreak`
        // * `Unlatch` as a result of `WaitUntilDone` is executed only if this is the first `WaitUntilDone` after successful unblocking of the handle
        // * `Grant` is not executed as long as the previous `WaitUntilDone` does not finish successfully, returning `true`
        // * `Unlock` is executed only when the previous `WaitUntilDone` returned `false`
        //
        //
        // SOURCE SIDE simplified algorithm:
        // (1)  `Latch` the handle
        // (2?) `Grant` time or `Unblock` the handle if previous `WaitUntilDone` failed
        // (3)  Call `WaitUntilDone`
        // (4)  `Unlatch` the handle
        // (5)  Go to p. (1)
        //
        // SINK SIDE simplified algorithm:
        // (1) 'Request' time
        // (2) Execute the time-aware code for a given virtual time
        //   (2.1) Finish the execution when granted virtual time is depleted using `ReportContinue`
        //   (2.2) Stop the execution in an arbitrary moment with the intent of resuming in the future and use `ReportBreak`
        // (3) Go to p. (1)
        //
        //
        // Properties:
        // * `SourceSideActive` - when set to `false`: `Request` returns immediately with the `false` result
        // * `SinkSideActive`   - when set to `false`: `WaitUntilDone` returns immediately with the `false` result
        // * `Enabled`          - when set to `false`: `WaitUntilDone` returns immediately with the `true` result
        // * `DeferredEnabled`  - `Enabled` will be assigned this value when unlatched
        //
        // Internal state:
        // * `sourceSideInProgress` - `true` from  `Grant`                            to  `WaitUntilDone` or `Dispose`
        // * `sinkSideInProgress`   - `true` from  `Request`                          to  `ReportBreak` or `ReportContinue` or `Dispose`
        // * `grantPending`         - `true` from  `Grant` or `Unblock`               to  `Request`
        // * `reportPending`        - `true` from  `ReportBreak` or `ReportContinue`  to  `WaitUntilDone`
        // * `isBlocking`           - `true` from  `ReportBreak`                      to  `Request`
        //
        // Additional notes:
        //
        // 1. `Active` means that the code on source/sink side is working and follows the above-mentioned algorithm.
        // Going `inactive` is a signal for a handle that its operations should not block anymore as there is no chance for their successful termination in the nearest future (i.e., as long as the handle is inactive).
        //
        // 2. When the handle is `disabled`, it does not inform the sink about passed time but immediately reports back, thus not blocking execution of other handles.
        //
        // 3. The handle is not allowed to resume the execution after reporting a break, without the explicit permission obtained from the time source.
        // This is why the call of `Request` waits for the `UnblockHandle` when executed in a blocking state.
        // Once the permission is granted, the handle uses what is left from the previous quantum instead of waiting for a new one.
        //
        // 4. Latching is needed to ensure that the handle will not become disabled/re-enabled in an arbitrary moment.
        // As described in (2), the disabled handle does not synchronize the sink side, so it cannot switch state when the sink is in progress of an execution and the sink cannot resume execution when the source side is in progress.
        // It is possible to defer changing value of `Enabled` by using `DeferredEnabled` property - their values will be automatically synced (i.e., `Enabled` will get `DeferredEnabled` value) when unlatching the handle.
        // -------

        /// <summary>
        /// Creates a new time handle and associates it with <paramref name="timeSource"/>.
        /// </summary>
        public TimeHandle(ITimeSource timeSource, ITimeSink timeSink)
        {
            innerLock = new object();
            enabled = true;
            DeferredEnabled = true;

            TimeSource = timeSource;

            // we should not assign this handle to TimeSink as the source might not be configured properly yet
            TimeSink = timeSink;

            Reset();
            this.Trace();
        }

        public void Reset()
        {
            lock(innerLock)
            {
                Debug.Assert(TimeSource.ElapsedVirtualTime >= TotalElapsedTime, $"Trying to move time handle back in time from: {TotalElapsedTime} to {TimeSource.ElapsedVirtualTime}");
                TotalElapsedTime = TimeSource.ElapsedVirtualTime;
            }
        }

        /// <summary>
        /// Grants a time interval to <see cref="ITimeSink"/>.
        /// </summary>
        /// <remarks>
        /// This method is called by <see cref="ITimeSource"/> and results in unblocking execution of all registered <see cref="ITimeSink"/> for granted period.
        /// It is illegal to call this method twice in a row. It must be followed by calling <see cref="WaitUntilDone"/>.
        /// </remarks>
        public void GrantTimeInterval(TimeInterval interval)
        {
            this.Trace($"{interval.Ticks}");
            lock(innerLock)
            {
                DebugHelper.Assert(IsReadyForNewTimeGrant, "Interval granted, but the handle is not ready for a new one.");
                sourceSideInProgress = true;

                intervalGranted = interval;

                if(enabled)
                {
                    this.Trace();
                    grantPending = true;
                    Monitor.PulseAll(innerLock);
                }
                else
                {
                    this.Trace();
                    // if the handle is not enabled there is a special way of handling new time grants:
                    // they are not reported to the sink and the following 'WaitUntilDone' returns immediately behaving like the whole time was used up;
                    // we must make sure that the handle is not enabled before the next 'WaitUntilDone' because it could change its result
                    Latch();
                    deferredUnlatch = true;
                }
            }
            this.Trace();
        }

        /// <summary>
        /// Allows to continue execution of previously granted time interval.
        /// </summary>
        /// <remarks>
        /// This method is called by <see cref="ITimeSource"/> and results in unblocking execution of all registered <see cref="ITimeSink"/> in order to finish execution of previously granted period.
        /// It is illegal to call this method twice in a row. It must be followed by calling <see cref="WaitUntilDone"/>.
        /// </remarks>
        public bool UnblockHandle()
        {
            this.Trace();
            lock(innerLock)
            {
                Debug.Assert(isBlocking || !enabled, "This handle should be blocking or disabled");

                if(!waitsToBeUnblocked)
                {
                    return false;
                }

                waitsToBeUnblocked = false;
                grantPending = true;

                Monitor.PulseAll(innerLock);
                return true;
            }
        }

        /// <summary>
        /// Used by the slave to requests a new time interval from the source.
        /// This method blocks current thread until the time interval is granted.
        /// </summary>
        /// <remarks>
        /// This method will return immediately when the handle is disabled or detached.
        /// It is illegal to call this method twice in a row if the first call was successful (returned true). It must always be followed by calling <see cref="ReportBackAndContinue"> or <see cref="ReportBackAndBreak">.
        /// </remarks>
        /// <returns>
        /// True if the interval was granted or False when this call was interrupted as a result of detaching or disabling.
        /// If it returned true, <paramref name="interval"> contains the amount of virtual time to be used by the sink. It is the sum of time interval granted by the source (using <see cref="GrantInterval">) and a time left reported previously by <see cref="ReportBackAndContinue"> or <see cref="ReportBackAndBreak">.
        /// If it returned false, the time interval is not granted and it is illegal to report anything back using <see cref="ReportBackAndContinue"> or <see cref="ReportBackAndBreak">.
        /// </returns>
        public bool RequestTimeInterval(out TimeInterval interval)
        {
            this.Trace();
            lock(innerLock)
            {
                DebugHelper.Assert(!sinkSideInProgress, "Requested a new time interval, but the previous one is still processed.");

                var result = true;
                if(!Enabled || interrupt)
                {
                    result = false;
                }
                else if(isBlocking && SourceSideActive)
                {
                    if(changingEnabled)
                    {
                        // we test `changingEnabled` here to avoid starvation:
                        // in order to change state of `Enabled` property the handle must not be latched,
                        // so the operation blocks until `latchLevel` drops down to 0;
                        // calling this method (`RequestTimeInterval`) when the handle is in a blocking state results
                        // in latching it temporarily until `WaitUntilDone` is called;
                        // this temporary latching/unlatching together with normal latching/unlatching in a short loop
                        // can cause `latchLevel` to fluctuate from 1 to 2 never allowing the operation modifying `Enabled` to finish
                        result = false;
                    }
                    else
                    {
                        // we check SourceSideActive here as otherwise unblocking will not succeed anyway
                        DebugHelper.Assert(!grantPending, "New grant not expected when blocked.");
                        DebugHelper.Assert(!waitsToBeUnblocked, "Should not wait to be unblocked");

                        // we cannot latch again when deferredUnlatch is still on as we could overwrite it and never unlatch again
                        innerLock.WaitWhile(() => deferredUnlatch && SourceSideActive && !interrupt, "Waiting for previous unlatch");
                        if(!SourceSideActive || interrupt)
                        {
                            result = false;
                        }
                        else
                        {
                            this.Trace("Asking time source to unblock the time handle");
                            // latching here is to protect against disabling Enabled that would lead to making IsBlocking false while waiting for unblocking this handle
                            Latch();

                            waitsToBeUnblocked = true;
                            innerLock.WaitWhile(() => waitsToBeUnblocked && SourceSideActive && !interrupt, "Waiting to be unblocked");
                            if(!SourceSideActive || interrupt)
                            {
                                DebugHelper.Assert(waitsToBeUnblocked, "Expected only one condition to change");

                                Unlatch();
                                waitsToBeUnblocked = false;
                                result = false;

                                this.Trace("Unblocking handle is not allowed, quitting");
                            }
                            else
                            {
                                DebugHelper.Assert(!waitsToBeUnblocked, "Should not wait to be unblocked here");
                                DebugHelper.Assert(!deferredUnlatch, "Unexpected value of deferredUnlatch");

                                deferredUnlatch = true;
                                recentlyUnblocked = true;
                                isBlocking = false;

                                this.Trace("Handle unblocked");
                            }
                        }
                    }
                }
                else if(!grantPending)
                {
                    // wait until a new time interval is granted or this handle is disabled/deactivated
                    innerLock.WaitWhile(() => !grantPending && Enabled && SourceSideActive && !interrupt, "Waiting for a time grant");
                    result = grantPending && !delayGrant && !interrupt;
                    delayGrant = false;
                }

                if(!result)
                {
                    interval = TimeInterval.Empty;
                }
                else
                {
                    interval = intervalGranted + slaveTimeResiduum;
                    DebugHelper.Assert(reportedTimeResiduum == TimeInterval.Empty, "Reported time residuum should be empty at this point");
                    reportedTimeResiduum = slaveTimeResiduum;
                    slaveTimeResiduum = TimeInterval.Empty;

                    sinkSideInProgress = true;
                    grantPending = false;
                }

                this.Trace($"{result}, {interval.Ticks}");
                interrupt = false;
                return result;
            }
        }

        public void ReportProgress(TimeInterval progress)
        {
            if(progress.Ticks == 0)
            {
                return;
            }

            lock(innerLock)
            {
                // reportedTimeResiduum represents time that
                // has been reported, but not yet used;
                // we cannot report it again
                if(reportedTimeResiduum >= progress)
                {
                    reportedTimeResiduum -= progress;
                    return;
                }
                if(reportedTimeResiduum != TimeInterval.Empty)
                {
                    progress -= reportedTimeResiduum;
                    reportedTimeResiduum = TimeInterval.Empty;
                }

                this.Trace($"Reporting progress: {progress}");
                TotalElapsedTime += progress;
                reportedSoFar += progress;
                TimeSource.ReportTimeProgress();
            }
        }

        /// <summary>
        /// Informs a time source that the time interval is used, i.e., no more work can be done without exceeding it, and the sink is ready for the next one.
        /// </summary>
        /// <remarks>
        /// It is possible that some part of granted interval cannot be used in this round. This value must be passed in <paramref name="timeLeft"> parameter.
        /// It is illegal to call this method without first obtaining the interval using <see cref="RequestTimeInterval">.
        /// </remarks>
        /// <param name="timeLeft">Amount of time not used.</param>
        public void ReportBackAndContinue(TimeInterval timeLeft)
        {
            this.Trace($"{timeLeft.Ticks}");
            lock(innerLock)
            {
                if(DetachRequested)
                {
                    return;
                }

                DebugHelper.Assert(sinkSideInProgress, "Reporting a used time, but it seems that no grant has recently been requested.");
                sinkSideInProgress = false;

                DebugHelper.Assert(slaveTimeResiduum == TimeInterval.Empty, "Time residuum should be empty here.");
                slaveTimeResiduum = timeLeft;
                intervalToReport = intervalGranted;

                reportPending = true;

                Monitor.PulseAll(innerLock);
                this.Trace();
            }
            ReportedBack?.Invoke();
        }

        /// <summary>
        /// Informs a time source that the time sink interrupted the execution before finishing the granted interval.
        /// In order to finish the job it is required to call <see cref="RequestTimeInterval"> followed by <see cref="ReportBackAndContinue">.
        /// </summary>
        /// <remarks>
        /// No new time interval will be granted to this and all other time sinks in the time domain until <see cref="ReportBackAndContinue"> is called.
        /// It is illegal to call this method without first obtaining the interval using <see cref="RequestTimeInterval">.
        /// </remarks>
        /// <param name="intervalLeft">Amount of time not used.</param>
        public void ReportBackAndBreak(TimeInterval timeLeft)
        {
            this.Trace($"{timeLeft.Ticks}");
            lock(innerLock)
            {
                if(DetachRequested)
                {
                    return;
                }

                DebugHelper.Assert(sinkSideInProgress, "Reporting a used time, but it seems that no grant has recently been requested.");
                sinkSideInProgress = false;

                intervalToReport = intervalGranted - timeLeft;
                intervalGranted = timeLeft;
                isBlocking = true;

                reportPending = true;

                Monitor.PulseAll(innerLock);
                this.Trace();
            }
            ReportedBack?.Invoke();
        }

        /// <summary>
        /// Informs a time source that any available time is used.
        /// </summary>
        /// <remarks>
        /// It is illegal to call this method if an interval is obtained, i.e. between calls to <see cref="RequestTimeInterval"> and <see cref="ReportBackAndContinue"> or <see cref="ReportBackAndBreak">.
        /// </remarks>
        public bool TrySkipToSyncPoint(out TimeInterval intervalSkipped)
        {
            lock(innerLock)
            {
                if(!RequestTimeInterval(out intervalSkipped))
                {
                    return false;
                }
                ReportBackAndContinue(TimeInterval.Empty);
                return true;
            }
        }

        /// <summary>
        /// Disables the handle and requests detaching it from <see cref="ITimeSource"/>.
        /// </summary>
        public void Dispose()
        {
            this.Trace();
            lock(innerLock)
            {
                SinkSideActive = false;
                SourceSideActive = false;

                // this operation is blocking if the handle is latched
                // it does not allow the handle to be disposed when in use
                Enabled = false;

                DetachRequested = true;
                sinkSideInProgress = false;
                sourceSideInProgress = false;
                reportPending = false;
                intervalToReport = intervalGranted;
                Monitor.PulseAll(innerLock);

                PauseRequested = null;
                StartRequested = null;
            }
            this.Trace();
        }

        /// <summary>
        /// Blocks the execution of current thread until the slave reports back.
        /// </summary>
        /// <param name="intervalUsed">Amount of virtual time that passed from the perspective of a slave.</param>
        /// <returns>
        /// A structure containing two booleans:
        ///     * IsDone: True if the slave completed all the work or false if the execution was interrupted (and it's blocking now).
        ///     * IsUnblockedRecently: True if the handle has recently (i.e., since the last call to `WaitUntilDone`) been unblocked - it resumed the execution after reporting break.
        /// </returns>
        public WaitResult WaitUntilDone(out TimeInterval intervalUsed)
        {
            this.Trace();
            lock(innerLock)
            {
                Debugging.DebugHelper.Assert(sourceSideInProgress, "About to wait until time is used, but it seems none has recently been granted.");

                innerLock.WaitWhile(() => sinkSideInProgress || (SinkSideActive && grantPending), "Waiting until time is used.");

                intervalUsed = enabled ? intervalToReport : intervalGranted;
                intervalToReport = TimeInterval.Empty;

                var isDone = !isBlocking;
                if(enabled && !SinkSideActive && !reportPending)
                {
                    Debugging.DebugHelper.Assert(!deferredUnlatch, "Unexpected state of deferredUnlatch");

                    // 'false' value of 'SinkSideActive' means that there is no point hanging and waiting in this function as there is no chance of unblocking in the nearest future
                    // in such situation just return 'false' simulating blocked state
                    // the only exception is if `reportPending` is set which means that we should first return value as set be the previous Report{Continue,Break}

                    this.Trace("Forcing result to be false");

                    // being here means that the sink has not yet
                    // seen the granted interval, so we can act
                    // as if it called ReportBackAndBreak
                    grantPending = false;
                    isBlocking = true;
                    intervalUsed = TimeInterval.Empty;
                    isDone = false;
                    // intervalGranted does not change

                    Monitor.PulseAll(innerLock);
                    this.Trace();
                }

                Debugging.DebugHelper.Assert(reportedSoFar <= intervalUsed);
                // here we report the remaining part of granted time
                reportedTimeResiduum = TimeInterval.Empty;
                ReportProgress(intervalUsed - reportedSoFar);
                reportedSoFar = TimeInterval.Empty;

                reportPending = false;

                if(isDone)
                {
                    sourceSideInProgress = false;
                }

                var result = new WaitResult(isDone, recentlyUnblocked);
                recentlyUnblocked = false;
                if(deferredUnlatch)
                {
                    deferredUnlatch = false;
                    Unlatch();
                }

                Monitor.PulseAll(innerLock);

                this.Trace($"Reporting {intervalUsed.Ticks} ticks used. Local elapsed virtual time is {TotalElapsedTime.Ticks} ticks.");
                this.Trace(result.ToString());
                return result;
            }
        }

        /// <summary>
        /// Latches the time handle, i.e., blocks any calls resulting in changing <see cref="Enabled"> property until <see cref="Unlatch"> is called.
        /// </summary>
        /// <remarks>
        /// This method is intended for use by time source to ensure that all asynchronous changes to the time handle's state are masked.
        /// </remarks>
        public void Latch()
        {
            this.Trace();
            lock(innerLock)
            {
                latchLevel++;
                this.Trace($"Time handle latched; current level is {latchLevel}");
            }
            this.Trace();
        }

        /// <summary>
        /// Unlatches the time handle.
        /// </summary>
        /// <remarks>
        /// Calling this method will result in unblocking all threads wanting to change <see cref="Enabled"> property.
        /// </remarks>
        public void Unlatch()
        {
            this.Trace();
            lock(innerLock)
            {
                DebugHelper.Assert(latchLevel > 0, "Tried to unlatch not latched handle");
                latchLevel--;
                this.Trace($"Time handle unlatched; current level is {latchLevel}");
                // since there is one place when we wait for latch to be equal to 1, we have to pulse more often than only when latchLevel is 0
                Monitor.PulseAll(innerLock);

                if(latchLevel == 0)
                {
                    Enabled = DeferredEnabled;
                }
            }
            this.Trace();
        }

        /// <summary>
        /// Calls <see cref="PauseRequested"/> event.
        /// </summary>
        public void RequestPause()
        {
            this.Trace();
            PauseRequested?.Invoke();
        }

        /// <summary>
        /// Calls <see cref="StartRequested"/> event.
        /// </summary>
        public void RequestStart()
        {
            this.Trace();
            StartRequested?.Invoke();
        }

        /// <summary>
        /// Interrupts the current or next call to <see cref="RequestTimeInterval"/> causing it to return 'false' immediately.
        /// </summary>
        public void Interrupt()
        {
            lock(innerLock)
            {
                interrupt = true;
                Monitor.PulseAll(innerLock);
            }
        }

        /// <summary>
        /// Sets the value indicating if the handle is enabled, i.e., is sink interested in the time information.
        /// </summary>
        /// <remarks>
        /// When the handle is disabled it behaves as if the execution on the sink was instantaneous - it never blocks other threads, but keeps track of virtual time.
        /// Setting this property might be blocking (if the time handle is currently latched).
        /// Disabled handle will not block on <see cref="WaitUntilDone">, returning 'true' immediately.
        /// Disabling the handle interrupts current <see cref="RequestTimeInterval"> call and makes all following calls return immediately with 'false'.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                return enabled;
            }

            set
            {
                lock(innerLock)
                {
                    if(enabled == value)
                    {
                        return;
                    }

                    changingEnabled = true;
                    this.Trace("About to wait for unlatching the time handle");
                    innerLock.WaitWhile(() => latchLevel > 0, "Waiting for unlatching the time handle");

                    this.Trace($"Enabled value changed: {enabled} -> {value}");
                    enabled = value;
                    DeferredEnabled = value;
                    changingEnabled = false;
                    if(!enabled)
                    {
                        Monitor.PulseAll(innerLock);

                        // we have just disabled the handle - it needs to be reset it to a state like after `ReportBackAndContinue` with not time left
                        if(isBlocking)
                        {
                            slaveTimeResiduum = TimeInterval.Empty;
                            reportedTimeResiduum = TimeInterval.Empty;
                            intervalToReport = intervalGranted;
                            reportPending = true;
                            isBlocking = false;

                            Monitor.PulseAll(innerLock);
                            this.Trace();
                        }
                    }
                    else
                    {
                        TimeSource.ReportHandleActive();
                        RequestStart();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the value indicating if this handle is active from the source perspective, i.e., will source grant new time in the nearest future.
        /// </summary>
        public bool SourceSideActive
        {
            get
            {
                return sourceSideActive;
            }

            set
            {
                lock(innerLock)
                {
                    this.Trace($"{value}");
                    sourceSideActive = value;
                    if(!sourceSideActive)
                    {
                        // there is a code that waits for a change of `SourceSideActive` value using `WaitWhile`, so we must call `PulseAll` here
                        Monitor.PulseAll(innerLock);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the value indicating if this handle is active from the sink perspective, i.e., will sink be requesting a time grant in the nearest future.
        /// </summary>
        /// <remarks>
        /// As long as the handle is not active from the sink perspective all <see cref="WaitUntilDone"> calls will return immediately with 'false'.
        /// </remarks>
        public bool SinkSideActive
        {
            get
            {
                return sinkSideActive;
            }

            set
            {
                lock(innerLock)
                {
                    DebugHelper.Assert(!sinkSideInProgress, "Should not change sink side active state when sink is in progress");

                    this.Trace($"{value}");
                    sinkSideActive = value;
                    if(!sinkSideActive)
                    {
                        Monitor.PulseAll(innerLock);
                    }
                    else
                    {
                        // we must inform the source that we became active so it can spin its loop again
                        TimeSource.ReportHandleActive();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the flag indicating if the new time interval can be granted to this handle.
        /// </summary>
        /// <remarks>
        /// In order for a handle to be ready to accept a new time grant, following conditions must be met:
        /// * previously granted time must be completely used,
        /// * detaching must not be requested.
        /// </remarks>
        public bool IsReadyForNewTimeGrant
        {
            get
            {
                lock(innerLock)
                {
                    var res = !sourceSideInProgress && !DetachRequested;
                    this.Trace($"Reading IsReadyForNewTimeGrant: {res}; sourceSideInProgress={sourceSideInProgress}, DetachRequested={DetachRequested}");
                    return res;
                }
            }
        }

        /// <summary>
        /// This flag set guarantees the next call to <see cref="UnblockHandle"> to succeed.
        /// </summary>
        public bool IsReadyToBeUnblocked => waitsToBeUnblocked || !enabled;

        /// <summary>
        /// Gets the flag indicating if this time handle is disposed and ready to be removed from its time source.
        /// </summary>
        public bool DetachRequested { get; private set; }

        /// <summary>
        /// Gets the reference to the time source associated with this handle.
        /// </summary>
        public ITimeSource TimeSource { get; private set; }

        /// <summary>
        /// Gets the reference to the time sink associated with this handle.
        /// </summary>
        public ITimeSink TimeSink { get; private set; }

        /// <summary>
        /// Gets the amount of virtual time that passed from the perspective of this handle.
        /// </summary>
        public TimeInterval TotalElapsedTime { get; private set; }

        /// <summary>
        /// The value of the enabled property that will be set on the nearest call to <see cref="Unlatch"> method.
        /// </summary>
        public bool DeferredEnabled { get; set; }

        /// <summary>
        /// Delay time grant to sink by one call to <see cref="RequestTimeInterval"> when waiting for a time grant from source.
        /// </summary>
        public bool DelayGrant
        {
            get
            {
                return delayGrant;
            }

            set
            {
                lock(innerLock)
                {
                    delayGrant = value;
                }
            }
        }

        /// <summary>
        /// Is set by the source, indicates whether the sink has used all of the time interval available to the source.
        /// </summary>
        public bool IsDone
        {
            get
            {
                return isDone;
            }

            set
            {
                isDone = value;
            }
        }

        /// <summary>
        /// Informs the sink that the source wants to pause its execution.
        /// </summary>
        /// <remarks>
        /// The sink can react to it in the middle of a granted period and pause instantly.
        /// </remarks>
        public event Action PauseRequested;

        /// <summary>
        /// Informs the sink that the source is about to (re)start its execution, so it should start the dispatcher thread and get ready for new grants.
        /// </summary>
        public event Action StartRequested;

        /// <summary>
        /// Call when the sink calls ReportBackAndContinue or ReportBackAndBreak.
        /// </summary>
        public event Action ReportedBack;

        [Antmicro.Migrant.Hooks.PreSerialization]
        private void VerifyStateBeforeSerialization()
        {
            lock(innerLock)
            {
                DebugHelper.Assert(!sinkSideInProgress, "Trying to save a time handle that processes a time grant");
            }
        }

        /// <summary>
        /// Indicates that there is a time granted, but not yet successfully waited for (i.e., with 'true' result).
        /// </summary>
        private bool sourceSideInProgress;
        private bool isBlocking;
        /// <summary>
        /// Indicates that there is a new time granted but not yet requested.
        /// </summary>
        private bool grantPending;
        private bool sinkSideInProgress;
        private bool reportPending;

        /// <summary>
        /// The amount of time granted last time.
        /// </summary>
        private TimeInterval intervalGranted;
        /// <summary>
        /// The amount of time to return on next <see cref="WaitUntilDone"/>.
        /// </summary>
        private TimeInterval intervalToReport;
        /// <summary>
        /// The amount of time left from previous grant that was not used but reported back in <see cref="WaitUntilDone"/>.
        /// </summary>
        private TimeInterval slaveTimeResiduum;
        /// <summary>
        /// The amount of time left from previous grant that was reported in <see cref="ReportProgress"/>.
        /// </summary>
        private TimeInterval reportedTimeResiduum;
        /// <summary>
        /// Flag is set when the handle is actively waiting to be unblocked
        /// </summary>
        private bool waitsToBeUnblocked;
        ///<summary>
        /// The amount of time already reported since last WaitUntilDone.
        ///</summary>
        private TimeInterval reportedSoFar;

        private bool enabled;
        private bool sinkSideActive;
        private bool sourceSideActive;

        private bool changingEnabled;
        private int latchLevel;
        private bool deferredUnlatch;
        private bool recentlyUnblocked;
        private bool delayGrant;
        private bool interrupt;
        private volatile bool isDone;

        private readonly object innerLock;

        public struct WaitResult
        {
            public WaitResult(bool isDone, bool isUnblockedRecently) : this()
            {
                IsDone = isDone;
                IsUnblockedRecently = isUnblockedRecently;
            }

            public override string ToString()
            {
                return $"[WaitResult(isDone: {IsDone}, isActivatedRecently: {IsUnblockedRecently})]";
            }

            public bool IsDone { get; private set; }
            public bool IsUnblockedRecently { get; private set; }
        }
    }
}
