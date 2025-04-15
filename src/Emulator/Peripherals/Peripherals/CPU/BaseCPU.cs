//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using ELFSharp.ELF;
using ELFSharp.UImage;
using Machine = Antmicro.Renode.Core.Machine;

namespace Antmicro.Renode.Peripherals.CPU
{
    /// <summary>
    /// <see cref="BaseCPU"/> implements <see cref="ICluster{T}"/> interface
    /// to seamlessly handle either cluster or CPU as a parameter to different methods.
    /// </summary>
    public abstract class BaseCPU : CPUCore, ICluster<BaseCPU>, ICPU, IDisposable, ITimeSink, IInitableCPU
    {
        protected BaseCPU(uint id, string cpuType, IMachine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
            : base(id)
        {
            if(cpuType == null)
            {
                throw new ConstructionException("cpuType was null");
            }

            Endianness = endianness;
            PerformanceInMips = 100;
            this.Model = cpuType;
            this.machine = machine;
            this.bitness = bitness;
            isPaused = true;

            singleStepSynchronizer = new Synchronizer();
            EmulationManager.Instance.CurrentEmulation.SingleStepBlockingChanged += UpdateHaltedState;

            Clustered = new BaseCPU[] { this };
        }

        public virtual void InitFromElf(IELF elf)
        {
            if(elf.GetBitness() > (int)bitness)
            {
                throw new RecoverableException($"Unsupported ELF format - trying to load a {elf.GetBitness()}-bit ELF on a {(int)bitness}-bit machine");
            }

            this.Log(LogLevel.Info, "Setting PC value to 0x{0:X}.", elf.GetEntryPoint());
            SetPCFromEntryPoint(elf.GetEntryPoint());
        }

        public virtual void InitFromUImage(UImage uImage)
        {
            this.Log(LogLevel.Info, "Setting PC value to 0x{0:X}.", uImage.EntryPoint);
            SetPCFromEntryPoint(uImage.EntryPoint);
        }

        public ulong Step(int count = 1)
        {
            if(IsHalted)
            {
                this.Log(LogLevel.Warning, "Ignoring stepping on a halted CPU");
                return PC;
            }

            lock(singleStepSynchronizer.Guard)
            {
                ExecutionMode = ExecutionMode.SingleStep;

                // Starting emulation has to be done after changing ExecutionMode. Otherwise, continuous CPUs won't be waiting for step command.
                var emulation = EmulationManager.Instance.CurrentEmulation;
                if(!emulation.IsStarted)
                {
                    var emulationCPUs = emulation.Machines.SelectMany(x => x.SystemBus.GetCPUs());
                    var continuousCPUs = emulationCPUs.Where(x => x.ExecutionMode == ExecutionMode.Continuous);
                    if(continuousCPUs.Any())
                    {
                        this.Log(LogLevel.Info, "Automatically starting all CPUs due to '{0}' including CPUs with {1} {2}: {3}", nameof(Step),
                            ExecutionMode.Continuous, nameof(ExecutionMode), Misc.PrettyPrintCollection(continuousCPUs, x => x.GetName())
                            );
                    }
                    emulation.StartAll();
                }
                else
                {
                    Resume();
                }

                this.Log(LogLevel.Noisy, "Stepping {0} step(s)", count);

                var th = TimeHandle;
                if(th != null)
                {
                    th.DeferredEnabled = true;
                }

                // Invoking this to allow virtual time to be granted, without setting currentHaltedState to false
                if(!IsSingleStepBlocking)
                {
                    UpdateHaltedState(ignoreExecutionMode: true);
                }

                singleStepSynchronizer.CommandStep(count);
                singleStepSynchronizer.WaitForStepFinished();

                UpdateHaltedState();

                return PC;
            }
        }

        public void SkipTime(TimeInterval amountOfTime)
        {
            var instructions = amountOfTime.ToCPUCycles(PerformanceInMips, out var residuum);
            if(residuum > 0)
            {
                // We want to execute instructions for at least required amount of time, so we should add
                // instructions += ceil(residuum / TicksPerMicrosecond) * PerformanceInMips
                // As residuum < TicksPerMicrosecond by definition, ceiling of it will be always 1
                instructions += PerformanceInMips;
                var newInterval = TimeInterval.FromCPUCycles(instructions, PerformanceInMips, out var _);
                this.Log(LogLevel.Warning, "Conversion from time to instructions is not exact, real time skipped: {0}", newInterval);
            }
            SkipInstructions += instructions;
        }

        public virtual void Dispose()
        {
            DisposeInner();
        }

        public virtual void Reset()
        {
            isAborted = false;
            Pause();
            EmulationState = EmulationCPUState.InReset;
        }

        public virtual void SyncTime()
        {
            // by default do nothing
        }

        public abstract ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions);

        public abstract string Architecture { get; }

        public Endianess Endianness { get; }

        public IBusController Bus => machine.SystemBus;

        public IEnumerable<ICluster<BaseCPU>> Clusters { get; } = new List<ICluster<BaseCPU>>(0);

        public IEnumerable<BaseCPU> Clustered { get; }

        public string Model { get; }

        public uint PerformanceInMips
        {
            get => performanceInMips.Value;
            set => performanceInMips.Value = value;
        }

        //The debug mode disables interrupt handling in the emulated CPU
        //Additionally, some instructions, suspending execution, until an interrupt arrives (e.g. HLT on x86 or WFI on ARM) are treated as NOP
        public virtual bool ShouldEnterDebugMode
        {
            get => shouldEnterDebugMode;
            set
            {
                if(value == true && !(DebuggerConnected && IsSingleStepMode))
                {
                    this.Log(LogLevel.Warning, "The debug mode now has no effect - connect a debugger, and switch to stepping mode.");
                }
                shouldEnterDebugMode = value;
            }
        }

        public bool OnPossessedThread
        {
            get
            {
                var cpuThreadLocal = cpuThread;
                return cpuThreadLocal != null && Thread.CurrentThread.ManagedThreadId == cpuThreadLocal.ManagedThreadId;
            }
        }

        public bool DebuggerConnected { get; set; }

        public override bool IsHalted
        {
            get
            {
                return isHaltedRequested;
            }
            set
            {
                this.Trace();
                if(value == isHaltedRequested)
                {
                    return;
                }

                lock(pauseLock)
                {
                    this.Trace();
                    isHaltedRequested = value;
                    UpdateHaltedState();

                    if(value)
                    {
                        if(started && !isPaused)
                        {
                            wasRunningWhenHalted = true;
                            Pause(new HaltArguments(HaltReason.Pause, this), checkPauseGuard: false);
                        }
                    }
                    else
                    {
                        if(EmulationState == EmulationCPUState.InReset)
                        {
                            EmulationState = EmulationCPUState.Running;
                        }

                        if(wasRunningWhenHalted)
                        {
                            Resume();
                        }
                    }
                }
            }
        }

        /// <remarks><c>StateChanged</c> is invoked when the value gets changed.</remarks>
        public EmulationCPUState EmulationState
        {
            get => state;

            private set
            {
                var oldState = state;
                if(oldState == value)
                {
                    return;
                }
                state = value;
                if(oldState == EmulationCPUState.InReset)
                {
                    OnLeavingResetState();
                }
                StateChanged?.Invoke(this, oldState, value);
            }
        }

        public TimeHandle TimeHandle
        {
            get
            {
                return timeHandle;
            }
            set
            {
                this.Trace("Setting a new time handle");
                timeHandle?.Dispose();
                lock(haltedLock)
                {
                    timeHandle = value;
                    timeHandle.Enabled = !currentHaltedState;
                    timeHandle.PauseRequested += RequestPause;
                    timeHandle.StartRequested += StartCPUThreadTimeHandle;
                }
            }
        }

        public ulong SkippedInstructions { get; private set; }

        public virtual ExecutionMode ExecutionMode
        {
            get
            {
                return executionMode;
            }

            set
            {
                lock(singleStepSynchronizer.Guard)
                {
                    if(executionMode == value)
                    {
                        return;
                    }

                    executionMode = value;

                    singleStepSynchronizer.Enabled = IsSingleStepMode;
                    UpdateHaltedState();
                }
            }
        }

        public event Action<HaltArguments> Halted;

        /// <remarks>The arguments passed are: <c>StateChanged(cpu, oldState, newState)</c>.</remarks>
        public event Action<ICPU, EmulationCPUState, EmulationCPUState> StateChanged;

        public abstract ulong ExecutedInstructions { get; }
        public abstract RegisterValue PC { get; set; }

        public bool IsPaused => isPaused;

        protected virtual void InnerPause(bool onCpuThread, bool checkPauseGuard)
        {
            RequestPause();

            if(!onCpuThread)
            {
                bool success = false;
                do
                {
                    const int startPauseDeadlockResolveTimeoutMs = 100;
                    TimeHandle.Interrupt(ref success, startPauseDeadlockResolveTimeoutMs);
                    if(!success)
                    {
                        /// We weren't able to interrupt the <see cref="TimeHandle"/>.
                        /// Check marker to distinguish a usual timeout from a deadlock.
                        if(pauseLockTimeHandleMarker)
                        {
                            /// We have a deadlock with other thread in <see cref="StartCPUThreadTimeHandle"/>.
                            /// Release <see cref="pauseLock"/> and try <see cref="TimeHandle.Interrupt"/> again later after the deadlock is resolved.
                            /// In rare cases we expect a deadlock as a result of starting and pausing cpu from different threads.
                            /// We use <see cref="Monitor.Pulse"/> on <see cref="pauseLock"/> only in <see cref="StartCPUThreadTimeHandle"/>
                            /// which is a verified case of the race between time source and Monitor thread.
                            /// Clear marker before releasing lock to select short path in <see cref="StartCPUThreadTimeHandle"/>.
                            pauseLockTimeHandleMarker = false;
                            Monitor.Wait(pauseLock);
                            /// We should get control back very soon due to short path in <see cref="StartCPUThreadTimeHandle"/>.
                            DebugHelper.Assert(pauseLockTimeHandleMarker);
                            pauseLockTimeHandleMarker = false;
                        }
                        else
                        {
                            /// We have a usual timeout. Try <see cref="TimeHandle.Interrupt"/> again.
                            /// Leave a warning here, as this may require special attention performance-wise.
                            this.Log(LogLevel.Warning, "Trying to stop CPU execution, but it is taking longer than expected...");
                        }
                    }
                }
                while(!success);
            }
        }

        protected virtual void Pause(HaltArguments haltArgs, bool checkPauseGuard)
        {
            if(isAborted || isPaused)
            {
                // cpu is already paused or aborted
                return;
            }

            lock(pauseLock)
            {
                // cpuThread can get null as a result of `InnerPause` call
                var cpuThreadCopy = cpuThread;
                var onCpuThread = (cpuThreadCopy != null && Thread.CurrentThread.ManagedThreadId == cpuThreadCopy.ManagedThreadId);

                InnerPause(onCpuThread, checkPauseGuard);

                if(!onCpuThread)
                {
                    singleStepSynchronizer.Enabled = false;
                    this.NoisyLog("Waiting for thread to pause.");
                    cpuThreadCopy?.Join();
                    this.NoisyLog("Paused.");
                }

                isPaused = true;
            }

            InvokeHalted(haltArgs);
        }

        protected void ReportProgress(ulong instructions)
        {
            if(instructions > 0)
            {
                instructionsLeftThisRound -= instructions;
                instructionsExecutedThisRound += instructions;
                // CPU is `executedResiduum` instructions ahead of the reported time and this value is smaller than the smallest positive possible amount to report,
                // so we report sum of currently executed/skipped instructions and residuum from previously reported progress.
                var intervalToReport = TimeInterval.FromCPUCycles(instructions + executedResiduum, PerformanceInMips, out executedResiduum);
                TimeHandle.ReportProgress(intervalToReport);
            }
        }

        protected virtual void OnLeavingResetState()
        {
            // Intentionally left blank.
        }

        protected override void OnResume()
        {
            if(EmulationState == EmulationCPUState.InReset && !currentHaltedState)
            {
                EmulationState = EmulationCPUState.Running;
            }
            singleStepSynchronizer.Enabled = IsSingleStepMode;
            StartCPUThread();
        }

        protected override void OnPause()
        {
            Pause(new HaltArguments(HaltReason.Pause, this), checkPauseGuard: true);
        }

        protected virtual void RequestPause()
        {
            lock(pauseLock)
            {
                isPaused = true;
                this.Trace("Requesting pause");
                sleeper.Interrupt();
            }
        }

        protected virtual void DisposeInner(bool silent = false)
        {
            // Take a copy of the CPU thread because it will be cleared at the end of its body
            var cpuThreadCopy = cpuThread;
            disposing = true;
            if(!silent)
            {
                this.NoisyLog("About to dispose CPU.");
            }
            started = false;
            Pause(new HaltArguments(HaltReason.Abort, this), checkPauseGuard: false);
            singleStepSynchronizer.Enabled = false;
            cpuThreadCopy?.Join();
            EmulationManager.Instance.CurrentEmulation.SingleStepBlockingChanged -= UpdateHaltedState;
        }

        protected void InvokeHalted(HaltArguments arguments)
        {
            var halted = Halted;
            if(halted != null)
            {
                halted(arguments);
            }
        }

        protected virtual void CpuThreadBody()
        {
            var isLocked = false;
            try
            {
#if DEBUG
                using(this.TraceRegion("CPU loop"))
#endif
                using(var activityTracker = (DisposableWrapper)this.ObtainSinkActiveState())
                using(TimeDomainsManager.Instance.RegisterCurrentThread(() => new TimeStamp(TimeHandle.TotalElapsedTime, TimeHandle.TimeSource.Domain)))
                {
                    try
                    {
restart:
                        while(!isPaused && !isAborted)
                        {
                            var singleStep = false;
                            // locking here is to ensure that execution mode does not change
                            // before calling `WaitForStepCommand` method
                            lock(singleStepSynchronizer.Guard)
                            {
                                singleStep = IsSingleStepMode;
                                if(singleStep)
                                {
                                    // we become incactive as we wait for step command
                                    using(this.ObtainSinkInactiveState())
                                    {
                                        this.Log(LogLevel.Noisy, "Waiting for a step instruction (PC=0x{0:X8}).", PC.RawValue);
                                        InvokeHalted(new HaltArguments(HaltReason.Step, this));
                                        if(!singleStepSynchronizer.WaitForStepCommand())
                                        {
                                            this.Trace();
                                            continue;
                                        }
                                        this.Trace();
                                    }
                                }
                            }

                            var cpuResult = CpuThreadBodyInner(singleStep);

                            if(singleStep)
                            {
                                switch(cpuResult)
                                {
                                    case CpuResult.NothingExecuted:
                                        break;
                                    case CpuResult.MmuFault:
                                        this.Trace("Interrupting stepping due to the external MMU fault");
                                        singleStepSynchronizer.StepInterrupted();
                                        break;
                                    default:
                                        this.Trace();
                                        singleStepSynchronizer.StepFinished();
                                        break;
                                }
                            }
                        }

                        this.Trace();
                        lock(cpuThreadBodyLock)
                        {
                            if(dispatcherRestartRequested)
                            {
                                dispatcherRestartRequested = false;
                                this.Trace();
                                goto restart;
                            }

                            this.Trace();
                            // the `locker` is re-acquired here to
                            // make sure that dispose-related code of all usings
                            // is executed before setting `dispatcherThread` to
                            // null (what allows to start new dispatcher thread);
                            // otherwise there could be a race condition when
                            // new thread enters usings (e.g., activates sink side)
                            // and then the old one exits them (deactivating sink
                            // side as a result)
                            Monitor.Enter(cpuThreadBodyLock, ref isLocked);
                        }
                    }
                    catch(Exception)
                    {
                        // being here means we are in trouble anyway,
                        // so we don't have to care about the time framework
                        // protocol that much;
                        // without disabling activity tracker
                        // it will try to disable the time handle
                        // which might in turn crash with it's own
                        // exception (hiding the original one)
                        activityTracker.Disable();
                        throw;
                    }
                }
            }
            finally
            {
                cpuThread = null;
                if(isLocked)
                {
                    this.Trace();
                    Monitor.Exit(cpuThreadBodyLock);
                }
                this.Trace();
            }
        }

        protected virtual bool ExecutionFinished(ExecutionResult result)
        {
            return false;
        }

        protected CpuResult CpuThreadBodyInner(bool singleStep)
        {
            if(!TimeHandle.RequestTimeInterval(out var interval))
            {
                this.Trace();
                return CpuResult.NothingExecuted;
            }

            using(performanceInMips.Seal())
            {
                this.Trace($"CPU thread body running... granted {interval.Ticks} ticks");
                var mmuFaultThrown = false;
                var initialExecutedResiduum = executedResiduum;
                var initialTotalElapsedTime = TimeHandle.TotalElapsedTime;
                TimeInterval virtualTimeAhead;

                var instructionsToExecuteThisRound = interval.ToCPUCycles(PerformanceInMips, out ulong ticksResiduum);
                if(instructionsToExecuteThisRound <= executedResiduum)
                {
                    this.Trace("not enough time granted, reporting continue");
                    TimeHandle.ReportBackAndContinue(interval);
                    return CpuResult.NothingExecuted;
                }
                instructionsLeftThisRound = Math.Min(instructionsToExecuteThisRound - executedResiduum, singleStep ? 1 : ulong.MaxValue);
                instructionsExecutedThisRound = executedResiduum;

                while(!isPaused && !currentHaltedState && instructionsLeftThisRound > 0)
                {
                    this.Trace($"CPU thread body in progress; {instructionsLeftThisRound} instructions left...");

                    // this puts a limit on instructions to execute in one round
                    // and makes timers update independent of the current quantum
                    var toExecute = Math.Min(InstructionsToNearestLimit(), instructionsLeftThisRound);

                    if(skipInstructions > 0)
                    {
                        var amountOfInstructions = Math.Min(skipInstructions, toExecute);
                        this.Trace($"Skipping {amountOfInstructions} instructions");

                        toExecute -= amountOfInstructions;
                        skipInstructions -= amountOfInstructions;
                        SkippedInstructions += amountOfInstructions;
                        ReportProgress(amountOfInstructions);
                        // We have to update progress immidietely, as we could potentially
                        // call SyncTime during ExecuteInstructions
                    }

                    // set upper limit on instructions to execute to `int.MaxValue`
                    // as otherwise it would overflow further down in ExecuteInstructions
                    toExecute = Math.Min(toExecute, int.MaxValue);
                    var result = ExecutionResult.Ok;
                    if(toExecute > 0)
                    {
                        this.Trace($"Asking CPU to execute {toExecute} instructions");

                        result = ExecuteInstructions(toExecute, out var executed);
                        this.Trace($"CPU executed {executed} instructions and returned {result}");
                        machine.Profiler?.Log(new InstructionEntry(machine.SystemBus.GetCPUSlot(this), ExecutedInstructions));
                        ReportProgress(executed);
                    }
                    if(ExecutionFinished(result))
                    {
                        break;
                    }

                    if(result == ExecutionResult.WaitingForInterrupt)
                    {
                        if(!InDebugMode && !neverWaitForInterrupt)
                        {
                            this.Trace();
                            var instructionsToSkip = Math.Min(InstructionsToNearestLimit(), instructionsLeftThisRound);

                            virtualTimeAhead = machine.LocalTimeSource.ElapsedVirtualHostTimeDifference;
                            if(!machine.LocalTimeSource.AdvanceImmediately && virtualTimeAhead.Ticks > 0 && instructionsToSkip > 0)
                            {
                                // Don't fall behind realtime by sleeping
                                var intervalToSleep = TimeInterval.FromCPUCycles(instructionsToSkip, PerformanceInMips, out var cyclesResiduum).WithTicksMin(virtualTimeAhead.Ticks);
                                sleeper.Sleep(intervalToSleep.ToTimeSpan(out var nsResiduum), out var intervalSlept);
                                // If we have a CPU of less than 10 MIPS, it might be the case that the interval slept (which is in units of 100 ns)
                                // is less than 1 instruction. Just round up it in this case.
                                instructionsToSkip = Math.Max(TimeInterval.FromTimeSpan(intervalSlept, nsResiduum).ToCPUCycles(PerformanceInMips, out var _) + cyclesResiduum, 1);
                            }

                            ReportProgress(instructionsToSkip);
                        }
                    }
                    else if(result == ExecutionResult.ExternalMmuFault)
                    {
                        this.Trace(result.ToString());
                        mmuFaultThrown = true;
                        break;
                    }
                    else if(result == ExecutionResult.Aborted)
                    {
                        this.Trace(result.ToString());
                        isAborted = true;
                        break;
                    }
                    else if(result == ExecutionResult.Interrupted || result == ExecutionResult.StoppedAtWatchpoint)
                    {
                        this.Trace(result.ToString());
                        break;
                    }
                }

                // If AdvanceImmediately is not enabled, and virtual time has surpassed host time,
                // sleep to make up the difference.
                // However, if pause was requested, we want to exit as soon as possible without sleeping,
                // because it was requested from interactive context (e.g. "pause" monitor command).
                virtualTimeAhead = machine.LocalTimeSource.ElapsedVirtualHostTimeDifference;
                if(!machine.LocalTimeSource.AdvanceImmediately && virtualTimeAhead.Ticks > 0 && !isPaused)
                {
                    // Ignore the return value, if the sleep is interrupted we'll make up any extra
                    // remaining difference next time. Preserve the interrupt request so that if this
                    // extra sleep is interrupted due to a CPU pause, it will be picked up by the WFI
                    // handling above.
                    sleeper.Sleep(virtualTimeAhead.ToTimeSpan(), out var _, preserveInterruptRequest: true);
                }

                this.Trace("CPU thread body finished");

                if(isAborted)
                {
                    this.Trace("aborted, reporting continue");
                    TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                    executedResiduum = 0;
                    EmulationState = EmulationCPUState.Aborted;
                    return CpuResult.Aborted;
                }
                else if(currentHaltedState)
                {
                    this.Trace("halted, reporting continue");
                    TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                    executedResiduum = 0;
                }
                else
                {
                    var instructionsLeft = instructionsToExecuteThisRound - instructionsExecutedThisRound;
                    // instructionsExecutedThisRound = reportedInstructions + executedResiduum
                    // reportedInstructions + executedResiduum + instructionsLeft = instructionsToExecuteThisRound
                    // reportedInstructions is divisible by instructionsPerTick and instructionsToExecuteThisRound is divisible by instructionsPerTick
                    // so instructionsLeft + executedResiduum is divisible by instructionsPerTick and residuum is 0
                    var timeLeft = TimeInterval.FromCPUCycles(instructionsLeft + executedResiduum, PerformanceInMips, out var residuum) + TimeInterval.FromMicroseconds(ticksResiduum);
                    DebugHelper.Assert(residuum == 0);
                    if(instructionsLeft > 0)
                    {
                        this.Trace("reporting break");
                        TimeHandle.ReportBackAndBreak(timeLeft);
                    }
                    else
                    {
                        DebugHelper.Assert(executedResiduum == 0);
                        // executedResiduum < instructionsPerTick so timeLeft is 0 + ticksResiduum
                        this.Trace("finished, reporting continue");
                        TimeHandle.ReportBackAndContinue(timeLeft);
                    }
                }

                if(mmuFaultThrown)
                {
                    return CpuResult.MmuFault;
                }
                else if(executedResiduum == initialExecutedResiduum && TimeHandle.TotalElapsedTime == initialTotalElapsedTime)
                {
                    return CpuResult.NothingExecuted;
                }
                return CpuResult.ExecutedInstructions;
            }
        }

        private void StartCPUThreadTimeHandle()
        {
            this.Trace();
            pauseLockTimeHandleMarker = true;
            lock(pauseLock)
            {
                if(!pauseLockTimeHandleMarker)
                {
                    /// Marker value got changed by other thread in <see cref="InnerPause"/>.
                    /// We escaped from a deadlock and we are going to be interrupted by <see cref="TimeHandle.Interrupt"/>.
                    /// It means there is a race between starting and pausing cpu from different threads.
                    /// We allow pausing to win in such case because pausing cpu is more "explicit" operation
                    /// than starting cpu as part of <see cref="TimeHandle.StartRequested"/>.
                    /// We were allowed to enter this critical region guarded by <see cref="pauseLock"/> due to special circumstances
                    /// despite <see cref="pauseLock"/> having previously been held by <see cref="Pause"/> + <see cref="InnerPause"/>.
                    /// Signal that we escaped deadlock condition and return control to the other thread waiting in <see cref="InnerPause"/>.
                    pauseLockTimeHandleMarker = true;
                    Monitor.Pulse(pauseLock);
                    return;
                }
                pauseLockTimeHandleMarker = false; // clear marker
                StartCPUThreadInner();
            }
        }

        protected void StartCPUThread()
        {
            this.Trace();
            lock(pauseLock)
            {
                StartCPUThreadInner();
            }
        }

        private void StartCPUThreadInner()
        {
            lock(cpuThreadBodyLock)
            {
                if(isAborted)
                {
                    return;
                }
                if(cpuThread == null)
                {
                    this.Trace();
                    cpuThread = new Thread(CpuThreadBody)
                    {
                        IsBackground = true,
                        Name = this.GetCPUThreadName(machine)
                    };
                    cpuThread.Start();
                }
                else
                {
                    this.Trace();
                    dispatcherRestartRequested = true;
                }
            }
        }

        protected void CheckIfOnSynchronizedThread()
        {
            if(Thread.CurrentThread.ManagedThreadId != cpuThread.ManagedThreadId)
            {
                this.Log(LogLevel.Warning, "An interrupt from the unsynchronized thread.");
            }
        }

        [Conditional("DEBUG")]
        protected void CheckCpuThreadId()
        {
            if(Thread.CurrentThread != cpuThread)
            {
                throw new ArgumentException(
                    string.Format("Method called from a wrong thread. Expected {0}, but got {1}",
                                  cpuThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId));
            }
        }

        protected virtual bool UpdateHaltedState(bool ignoreExecutionMode = false)
        {
            var shouldBeHalted = isHaltedRequested || (IsSingleStepMode && !IsSingleStepBlocking && !ignoreExecutionMode);

            if(shouldBeHalted == currentHaltedState)
            {
                return false;
            }

            lock(pauseLock)
            {
                this.Trace();
                currentHaltedState = shouldBeHalted;
                if(TimeHandle != null)
                {
                    this.Trace();
                    TimeHandle.DeferredEnabled = !shouldBeHalted;
                }
            }

            return true;
        }

        public virtual ulong SkipInstructions
        {
            get => skipInstructions;
            protected set => skipInstructions = value;
        }

        protected static bool IsSingleStepBlocking => EmulationManager.Instance.CurrentEmulation.SingleStepBlocking;

        protected bool InDebugMode => DebuggerConnected && ShouldEnterDebugMode && IsSingleStepMode;
        protected bool IsSingleStepMode => executionMode == ExecutionMode.SingleStep;

        protected bool shouldEnterDebugMode;
        protected bool neverWaitForInterrupt;
        protected bool dispatcherRestartRequested;
        protected bool isHaltedRequested;
        protected bool currentHaltedState;

        [Transient]
        protected ExecutionMode executionMode;

        [Transient]
        protected bool disposing;

        [Constructor]
        protected readonly Synchronizer singleStepSynchronizer;

        protected readonly Sleeper sleeper = new Sleeper();
        protected readonly CpuBitness bitness;
        protected readonly IMachine machine;

        protected enum CpuResult
        {
            ExecutedInstructions = 0,
            NothingExecuted = 1,
            MmuFault = 2,
            Aborted = 3,
        }

        protected class RegisterAttribute : Attribute
        {
        }

        private ulong InstructionsToNearestLimit()
        {
            var nearestLimitIn = ((BaseClockSource)machine.ClockSource).NearestLimitIn;
            var instructionsToNearestLimit = nearestLimitIn.ToCPUCycles(PerformanceInMips, out var unused);
            // the limit must be reached or surpassed for limit's owner to execute
            if(instructionsToNearestLimit <= executedResiduum)
            {
                return 1;
            }
            instructionsToNearestLimit -= executedResiduum;
            if(instructionsToNearestLimit != ulong.MaxValue && (nearestLimitIn.Ticks == 0 || unused > 0))
            {
                // we must check for `ulong.MaxValue` as otherwise it would overflow
                instructionsToNearestLimit++;
            }
            return instructionsToNearestLimit;
        }

        private void SetPCFromEntryPoint(ulong entryPoint)
        {
            var what = machine.SystemBus.WhatIsAt(entryPoint, this);
            if(what != null)
            {
                if(((what.Peripheral as IMemory) == null) && ((what.Peripheral as Redirector) != null))
                {
                    var redirector = what.Peripheral as Redirector;
                    var newValue = redirector.TranslateAbsolute(entryPoint);
                    this.Log(LogLevel.Info, "Fixing PC address from 0x{0:X} to 0x{1:X}", entryPoint, newValue);
                    entryPoint = newValue;
                }
            }
            PC = entryPoint;
        }

        // For use as an event callback to allow unregistering.
        private void UpdateHaltedState()
        {
            UpdateHaltedState(ignoreExecutionMode: false);
        }

        [Transient]
        private Thread cpuThread;

        private EmulationCPUState state = EmulationCPUState.InReset;
        private TimeHandle timeHandle;

        private bool wasRunningWhenHalted;
        private ulong executedResiduum;
        private ulong instructionsLeftThisRound;
        private ulong instructionsExecutedThisRound;
        private ulong skipInstructions;

        [Transient]
        private volatile bool pauseLockTimeHandleMarker;

        private readonly object cpuThreadBodyLock = new object();
        private readonly SealableValue<uint> performanceInMips = new SealableValue<uint>();
    }
}
