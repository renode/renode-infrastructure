//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Machine = Antmicro.Renode.Core.Machine;
using Antmicro.Migrant;
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Time;
using System.Threading.Tasks;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Peripherals.CPU.Registers;
using ELFSharp.ELF;
using ELFSharp.UImage;
using System.Diagnostics;
using System.Net.Sockets;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class TranslationCPU : IGPIOReceiver, ICpuSupportingGdb, IDisposable, IDisassemblable, IClockSource
    {
        public Endianess Endianness { get; protected set; }

        protected TranslationCPU(string cpuType, Machine machine, Endianess endianness)
        {
            if(cpuType == null)
            {
                throw new RecoverableException(new ArgumentNullException("cpuType"));
            }

            oldMaximumBlockSize = -1;

            Endianness = endianness;
            PerformanceInMips = 100;
            currentCountThreshold = 5000;
            this.cpuType = cpuType;
            ClockSource = new BaseClockSource();
            ClockSource.NumberOfEntriesChanged += (oldValue, newValue) =>
            {
                if(oldValue > newValue)
                {
                    Misc.Swap(ref oldValue, ref newValue);
                }
                if(oldValue == 0 && newValue != 0)
                {
                    ClearTranslationCache();
                }
            };
            this.translationCacheSize = DefaultTranslationCacheSize;
            this.machine = machine;
            started = false;
            isHalted = false;
            translationCacheSync = new object();
            pagesAccessedByIo = new HashSet<long>();
            pauseGuard = new CpuThreadPauseGuard(this);
            InitializeRegisters();
            InitInterruptEvents();
            Init();
            InitDisas();
        }

        public void StartGdbServer(int port, bool autostartEmulation = false)
        {
            if(IsGdbServerCreated)
            {
                throw new RecoverableException(string.Format("GDB server already started for this cpu on port: {0}", stub.Port));
            }

            try
            {
                stub = new GdbStub(port, this, autostartEmulation);
            }
            catch(SocketException e)
            {
                throw new RecoverableException(string.Format("Could not start GDB server: {0}", e.Message));
            }
        }

        public void StopGdbServer()
        {
            if(!IsGdbServerCreated)
            {
                return;
            }

            stub.Dispose();
            stub = null;
        }

        public virtual void InitFromElf(ELF<uint> elf)
        {
            this.Log(LogLevel.Info, "Setting PC value to 0x{0:X}.", elf.EntryPoint);
            SetPCFromEntryPoint(elf.EntryPoint);
        }

        public virtual void InitFromUImage(UImage uImage)
        {
            this.Log(LogLevel.Info, "Setting PC value to 0x{0:X}.", uImage.EntryPoint);
            SetPCFromEntryPoint(uImage.EntryPoint);
        }

        void IClockSource.ExecuteInLock(Action action)
        {
            ClockSource.ExecuteInLock(action);
        }

        void IClockSource.AddClockEntry(ClockEntry entry)
        {
            ClockSource.AddClockEntry(entry);
        }

        void IClockSource.ExchangeClockEntryWith(Action handler, Func<ClockEntry, ClockEntry> visitor,
            Func<ClockEntry> factorIfNonExistant)
        {
            ClockSource.ExchangeClockEntryWith(handler, visitor, factorIfNonExistant);
        }

        ClockEntry IClockSource.GetClockEntry(Action handler)
        {
            return ClockSource.GetClockEntry(handler);
        }

        void IClockSource.GetClockEntryInLockContext(Action handler, Action<ClockEntry> visitor)
        {
            ClockSource.GetClockEntryInLockContext(handler, visitor);
        }

        IEnumerable<ClockEntry> IClockSource.GetAllClockEntries()
        {
            return ClockSource.GetAllClockEntries();
        }

        bool IClockSource.RemoveClockEntry(Action handler)
        {
            return ClockSource.RemoveClockEntry(handler);
        }

        ulong IClockSource.CurrentValue
        {
            get
            {
                return ClockSource.CurrentValue;
            }
        }

        IEnumerable<ClockEntry> IClockSource.EjectClockEntries()
        {
            return ClockSource.EjectClockEntries();
        }

        void IClockSource.AddClockEntries(IEnumerable<ClockEntry> entries)
        {
            ClockSource.AddClockEntries(entries);
        }

        public int TranslationCacheSize
        {
            get
            {
                return translationCacheSize;
            }
            set
            {
                if(value == translationCacheSize)
                {
                    return;
                }
                translationCacheSize = value;
                SubmitTranslationCacheSizeUpdate();
            }
        }

        public int CountThreshold
        {
            get
            {
                return currentCountThreshold;
            }
            set
            {
                currentCountThreshold = value;
                RenodeSetCountThreshold(currentCountThreshold);
            }
        }

        public int MaximumBlockSize
        {
            get
            {
                return checked((int)TlibGetMaximumBlockSize());
            }
            set
            {
                SetMaximumBlockSize(checked((uint)value));
            }
        }

        private void SetMaximumBlockSize(uint value, bool skipSync = false)
        {
            TlibSetMaximumBlockSize(value);
            ClearTranslationCache(skipSync);
        }

        public bool LogTranslationBlockFetch
        {
            set
            {
                if(value)
                {
                    RenodeAttachLogTranslationBlockFetch(Marshal.GetFunctionPointerForDelegate(onTranslationBlockFetch));
                }
                else
                {
                    RenodeAttachLogTranslationBlockFetch(IntPtr.Zero);
                }
                logTranslationBlockFetchEnabled = value;
            }
            get
            {
                return logTranslationBlockFetchEnabled;
            }
        }

        public bool AdvanceImmediately { get; set; }

        public bool ThreadSentinelEnabled { get; set; }

        public bool ClocksourceBlockTrimming
        {
            get
            {
                return RenodeGetBlockTrimming() != 0;
            }
            set
            {
                RenodeSetBlockTrimming(value ? 1 : 0);
            }
        }

        private bool logTranslationBlockFetchEnabled;

        public ulong ExecutedInstructions { get; private set; }

        public int Slot { get{if(!slot.HasValue) slot = machine.SystemBus.GetCPUId(this); return slot.Value;} private set {slot = value;} }
        private int? slot;

        public void ClearTranslationCache(bool skipSync = false)
        {
            if(skipSync)
            {
                TlibInvalidateTranslationCache();
            }
            else
            {
                using(machine.ObtainPausedState())
                {
                    TlibInvalidateTranslationCache();
                }
            }
        }

        public void MeasureExecutionRate()
        {
            new Task(() =>
                     {
                var lastCount = 0UL;
                while(true)
                {
                    var executedInstructions = ExecutedInstructions;
                    double diff = executedInstructions - lastCount;
                    if(lastCount != 0)
                    {
                        this.Log(LogLevel.Info, "Execution rate: {0}IPS", Misc.NormalizeDecimal(diff));
                    }
                    lastCount = executedInstructions;
                    Thread.Sleep(1000);
                }

            }).Start();
        }

        /// <summary>
        /// Gets the registers values.
        /// </summary>
        /// <returns>The table of registers values.</returns>
        public virtual string[,] GetRegistersValues()
        {
            var result = new Dictionary<string, ulong>();
            var properties = GetType().GetProperties();

            //uint may be marked with [Register]
            var registerInfos = properties.Where(x => x.CanRead && x.GetCustomAttributes(false).Any(y => y is RegisterAttribute));
            foreach(var registerInfo in registerInfos)
            {
                result.Add(registerInfo.Name, (ulong)((dynamic)registerInfo.GetGetMethod().Invoke(this, null)));
            }

            //every field that is IRegister, contains properties interpreted as registers.
            var compoundRegisters = properties.Where(x => typeof(IRegisters).IsAssignableFrom(x.PropertyType));
            foreach(var register in compoundRegisters)
            {
                var compoundRegister = (IRegisters)register.GetGetMethod().Invoke(this, null);
                foreach(var key in compoundRegister.Keys)
                {
                    result.Add("{0}{1}".FormatWith(register.Name, key), (ulong)(((dynamic)compoundRegister)[key]));
                }

            }
            var table = new Table().AddRow("Name", "Value");
            table.AddRows(result, x => x.Key, x => "0x{0:X}".FormatWith(x.Value));
            return table.ToArray();
        }

        public void UpdateContext()
        {
            TlibRestoreContext();
        }

        protected void ExtendWaitHandlers(WaitHandle handle)
        {
            var tmp = new WaitHandle[waitHandles.Length + 1];
            Array.Copy(waitHandles, tmp, waitHandles.Length);
            tmp[tmp.Length - 1] = handle;

            waitHandles = tmp;
        }

        private void SubmitTranslationCacheSizeUpdate()
        {
            lock(translationCacheSync)
            {
                var currentTCacheSize = translationCacheSize;
                // disabled until segmentation fault will be resolved
                currentTimer = new Timer(x => UpdateTranslationCacheSize(currentTCacheSize), null, -1, -1);
            }
        }

        private void UpdateTranslationCacheSize(int sizeAtThatTime)
        {
            lock(translationCacheSync)
            {
                if(sizeAtThatTime != translationCacheSize)
                {
                    // another task will take care
                    return;
                }
                currentTimer = null;
                using(machine.ObtainPausedState())
                {
                    PrepareState();
                    DisposeInner(true);
                    RestoreState();
                }
            }
        }

        [PreSerialization]
        private void PrepareState()
        {
            interruptState = interruptEvents.Select(x => x.WaitOne(0)).ToArray();

            var statePtr = TlibExportState();
            BeforeSave(statePtr);
            cpuState = new byte[TlibGetStateSize()];
            Marshal.Copy(statePtr, cpuState, 0, cpuState.Length);
        }

        [PostSerialization]
        private void FreeState()
        {
            interruptState = null;
            cpuState = null;
        }

        [LatePostDeserialization]
        private void RestoreState()
        {
            InitInterruptEvents();
            Init();
            // TODO: state of the reset events
            FreeState();
        }

        public ExecutionMode ExecutionMode
        {
            get
            {
                lock(sync.Guard)
                {
                    return executionMode;
                }
            }

            set
            {
                if(executionMode == value)
                {
                    return;
                }

                lock(sync.Guard)
                {
                    executionMode = value;
                    if(executionMode == ExecutionMode.Continuous)
                    {
                        sync.Pass();
                    }
                    InvokeInCpuThreadSafely(AdjustBlockSize);
                }
            }
        }

        [Transient]
        private ExecutionMode executionMode;

        private void AdjustBlockSize()
        {
            // to avoid locking, step mode must be checked just once
            switch(executionMode)
            {
            case ExecutionMode.SingleStep:
                if(oldMaximumBlockSize == -1)
                {
                    oldMaximumBlockSize = MaximumBlockSize;
                    SetMaximumBlockSize(1, true);
                }
                break;
            case ExecutionMode.Continuous:
                if(oldMaximumBlockSize != -1)
                {
                    SetMaximumBlockSize((uint)oldMaximumBlockSize, true);
                    oldMaximumBlockSize = -1;
                }
                break;
            default:
                throw new ArgumentException("Unsupported execution mode");
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

        public virtual void Start()
        {
            Resume();
        }

        public string LogFile
        {
            get { return DisasEngine.LogFile; }
            set { DisasEngine.LogFile = value; }
        }

        public bool IsSetEvent(int number)
        {
            return interruptEvents[number].WaitOne(0);
        }

        public SystemBus Bus
        {
            get
            {
                return machine.SystemBus;
            }
        }

        public void Pause()
        {
            InnerPause(new HaltArguments(HaltReason.Pause));
        }

        private void InnerPause(HaltArguments haltArgs)
        {
            if(isAborted || PauseEvent.WaitOne(0))
            {
                // cpu is already paused or aborted
                return;
            }

            lock(pauseLock)
            {
                PauseEvent.Set();
                TlibSetPaused();

                if(Thread.CurrentThread.ManagedThreadId != cpuThread.ManagedThreadId)
                {
                    sync.Pass();
                    if(Thread.CurrentThread.ManagedThreadId != machine.HostTimeClockSource.UpdateThreadId)
                    {
                        this.NoisyLog("Waiting for thread to pause.");
                        cpuThread.Join();
                        this.NoisyLog("Paused.");
                    }
                    cpuThread = null;
                }
                else
                {
                    pauseGuard.OrderPause();
                }
            }

            InvokeHalted(haltArgs);
        }

        public virtual void Resume()
        {
            lock(pauseLock)
            {
                if(isAborted || !PauseEvent.WaitOne(0))
                {
                    return;
                }
                started = true;
                this.NoisyLog("Resuming.");
                cpuThread = new Thread(CpuLoop)
                {
                    IsBackground = true,
                    Name = this.GetCPUThreadName(machine)
                };
                PauseEvent.Reset();
                cpuThread.Start();
                TlibClearPaused();
                this.NoisyLog("Resumed.");
            }
        }

        public virtual void Reset()
        {
            isAborted = false;
            Pause();
            HandleRamSetup();
            TlibReset();
        }

        public virtual void OnGPIO(int number, bool value)
        {
            lock(lck)
            {
                var decodedInterrupt = DecodeInterrupt(number);
                if(ThreadSentinelEnabled)
                {
                    CheckIfOnSynchronizedThread();
                }
                this.NoisyLog("IRQ {0}, value {1}", number, value);
                // halted result means that cpu waits on WFI
                // in such case we should, obviously, not mask interrupts
                if(started && (lastTlibResult == HaltedResult || !(DisableInterruptsWhileStepping && executionMode == ExecutionMode.SingleStep)))
                {
                    TlibSetIrq((int)decodedInterrupt, value ? 1 : 0);
                }
                if(value)
                {
                    interruptEvents[number].Set();
                }
                else
                {
                    interruptEvents[number].Reset();
                }
            }
        }

        public virtual uint PC
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public event Action<HaltArguments> Halted;

        public void MapMemory(IMappedSegment segment)
        {
            using(machine.ObtainPausedState())
            {
                currentMappings.Add(new SegmentMapping(segment));
                RegisterMemoryChecked(segment.StartingOffset, segment.Size);
                checked
                {
                    TranslationCacheSize = (int)(currentMappings.Sum(x => x.Segment.Size) / 4);
                }
            }
        }

        public void UnmapMemory(Range range)
        {
            using(machine.ObtainPausedState())
            {
                var startAddress = checked((uint)range.StartAddress);
                var endAddress = checked((uint)(range.EndAddress - 1));
                ValidateMemoryRangeAndThrow(startAddress, (uint)range.Size);

                // when unmapping memory, two things has to be done
                // first is to flag address range as no-memory (that is, I/O)
                TlibUnmapRange(startAddress, endAddress);

                // and second is to remove mappings that are not used anymore
                currentMappings = currentMappings.
                    Where(x => TlibIsRangeMapped((uint)x.Segment.StartingOffset, (uint)(x.Segment.StartingOffset + x.Segment.Size)) == 1).ToList();
            }
        }

        public void SetPageAccessViaIo(long address)
        {
            pagesAccessedByIo.Add(address & TlibGetPageSize());
        }

        public void ClearPageAccessViaIo(long address)
        {
            pagesAccessedByIo.Remove(address & TlibGetPageSize());
        }

        public bool DisableInterruptsWhileStepping { get; set; }
        public uint PerformanceInMips { get; set; }

        public void LogFunctionNames(bool value, string spaceSeparatedPrefixes = "")
        {
            if(!value)
            {
                SetInternalHookAtBlockBegin(null);
                return;
            }
            var prefixesAsArray = spaceSeparatedPrefixes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var pc_cache = new LRUCache<uint, string>(10000);
            // using string builder here is due to performance reasons: test shows that string.Format is much slower
            var messageBuilder = new StringBuilder(256);

            SetInternalHookAtBlockBegin((pc, size) =>
            {
                string name;
                if(!pc_cache.TryGetValue(pc, out name))
                {
                    name = Bus.FindSymbolAt(pc);
                    pc_cache.Add(pc, name);
                }

                if(spaceSeparatedPrefixes != "" && !prefixesAsArray.Any(name.StartsWith))
                {
                    return;
                }
                messageBuilder.Clear();
                this.Log(LogLevel.Info, messageBuilder.Append("Entering function ").Append(name).Append(" at 0x").Append(pc.ToString("X")).ToString());
            });
        }

        // TODO: improve this when backend/analyser stuff is done
        [field: Transient]
        public virtual event Action<bool> IsHaltedChanged;

        public bool UpdateContextOnLoadAndStore { get; set; }

        public bool IsGdbServerCreated { get { return stub != null; } }

        private GdbStub stub;

        protected abstract Interrupt DecodeInterrupt(int number);

        public void ClearHookAtBlockBegin()
        {
            SetHookAtBlockBegin(null);
        }

        public void SetHookAtBlockBegin(Action<uint, uint> hook)
        {
            using(machine.ObtainPausedState())
            {
                if((hook == null) ^ (blockBeginUserHook == null))
                {
                    ClearTranslationCache();
                }
                blockBeginUserHook = hook;
            }
        }

        [Export]
        protected uint ReadByteFromBus(uint offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(true, offset))
            {
                return machine.SystemBus.ReadByte(offset);
            }
        }

        [Export]
        protected uint ReadWordFromBus(uint offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(true, offset))
            {
                return machine.SystemBus.ReadWord(offset);
            }
        }

        [Export]
        protected uint ReadDoubleWordFromBus(uint offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(true, offset))
            {
                return machine.SystemBus.ReadDoubleWord(offset);
            }
        }

        [Export]
        protected void WriteByteToBus(uint offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(false, offset))
            {
                machine.SystemBus.WriteByte(offset, unchecked((byte)value));
            }
        }

        [Export]
        protected void WriteWordToBus(uint offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(false, offset))
            {
                machine.SystemBus.WriteWord(offset, unchecked((ushort)value));
            }
        }

        [Export]
        protected void WriteDoubleWordToBus(uint offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuard(false, offset))
            {
                machine.SystemBus.WriteDoubleWord(offset, value);
            }
        }

        public abstract void SetRegisterUnsafe(int register, uint value);

        public abstract uint GetRegisterUnsafe(int register);

        public abstract int[] GetRegisters();

        private void SetInternalHookAtBlockBegin(Action<uint, uint> hook)
        {
            using(machine.ObtainPausedState())
            {
                if((hook == null) ^ (blockBeginInternalHook == null))
                {
                    ClearTranslationCache();
                }
                blockBeginInternalHook = hook;
            }
        }

        private void CheckIfOnSynchronizedThread()
        {
            if(Thread.CurrentThread.ManagedThreadId != cpuThread.ManagedThreadId
                && !machine.SyncDomain.OnSyncPointThread)
            {
                this.Log(LogLevel.Warning, "An interrupt from the unsynchronized thread.");
            }
        }

        private void RegisterMemoryChecked(long offset, long size)
        {
            checked
            {
                var uintOffset = (uint)offset;
                var uintSize = (uint)size;
                ValidateMemoryRangeAndThrow(uintOffset, uintSize);
                TlibMapRange(uintOffset, uintSize);
                this.NoisyLog("Registered memory at 0x{0:X}, size 0x{1:X}.", uintOffset, uintSize);
            }
        }

        private void ValidateMemoryRangeAndThrow(uint startAddress, uint uintSize)
        {
            var pageSize = TlibGetPageSize();
            if((startAddress % pageSize) != 0)
            {
                throw new RecoverableException("Memory offset has to be aligned to guest page size.");
            }
            if(uintSize % pageSize != 0)
            {
                throw new RecoverableException("Memory size has to be aligned to guest page size.");
            }
        }

        private void SetPCFromEntryPoint(uint entryPoint)
        {
            var what = machine.SystemBus.WhatIsAt((long)entryPoint);
            if(what != null)
            {
                if(((what.Peripheral as IMemory) == null) && ((what.Peripheral as Redirector) != null))
                {
                    var redirector = what.Peripheral as Redirector;
                    var newValue = redirector.TranslateAbsolute(entryPoint);
                    this.Log(LogLevel.Info, "Fixing PC address from 0x{0:X} to 0x{1:X}", entryPoint, newValue);
                    entryPoint = (uint)newValue;
                }
            }
            PC = entryPoint;
        }

        private void InvokeInCpuThreadSafely(Action a)
        {
            actionsToExecuteInCpuThread.Enqueue(a);
        }

        private ConcurrentQueue<Action> actionsToExecuteInCpuThread = new ConcurrentQueue<Action>();
        private int lastTlibResult;

        private void CpuLoop()
        {
            if(ClockSource.HasEntries && advanceShouldBeRestarted)
            {
                try
                {
                    ClockSource.Advance(0, true);
                    advanceShouldBeRestarted = false;
                }
                catch(OperationCanceledException)
                {
                    return;
                }
            }

            while(true)
            {
                // halted result means that cpu waits on WFI
                // in such case we should, obviously, not mask interrupts
                if(!((DisableInterruptsWhileStepping && executionMode == ExecutionMode.SingleStep) || lastTlibResult == HaltedResult) && TlibIsIrqSet() == 0 && interruptEvents.Any(x => x.WaitOne(0)))
                {
                    for(var i = 0; i < interruptEvents.Length; i++)
                    {
                        var decodedInterrupt = DecodeInterrupt(i);
                        TlibSetIrq((int)decodedInterrupt, interruptEvents[i].WaitOne(0) ? 1 : 0);
                    }
                    //this.NoisyLog("IRQ not active while event set, added.");
                }
                try
                {
                    bool doIteration;
                    lock(haltedFinishedEvent)
                    {
                        doIteration = !isHalted;
                    }
                    if(doIteration)
                    {
                        Action queuedAction;
                        while(actionsToExecuteInCpuThread.TryDequeue(out queuedAction))
                        {
                            queuedAction();
                        }

                        HandleStepping(true);

                        pauseGuard.Enter();
                        skipNextStepping = true;
                        lastTlibResult = TlibExecute();
                        pauseGuard.Leave();
                    }
                }
                catch(CpuAbortException)
                {
                    this.NoisyLog("CPU abort detected, halting.");
                    isAborted = true;
                    InvokeHalted(new HaltArguments(HaltReason.Abort));
                    break;
                }
                catch(OperationCanceledException)
                {
                    advanceShouldBeRestarted = true;
                    break;
                }

                if(lastTlibResult == BreakpointResult)
                {
                    ExecuteHooks(PC);
                    // it is necessary to deactivate hooks installed on this PC before
                    // calling `tlib_execute` again to avoid a loop;
                    // we need to do this because creating a breakpoint has caused special
                    // exeption-rising, block-breaking `trap` instruction to be
                    // generated by the tcg;
                    // in order to execute code after the breakpoint we must first remove
                    // this `trap` and retranslate the code right after it;
                    // this is achieved by deactivating the breakpoint (i.e., unregistering
                    // from tlib, but keeping it in C#), executing the beginning of the next
                    // block and registering the breakpoint again in the OnBlockBegin hook
                    DeactivateHooks(PC);
                }

                if(PauseEvent.WaitOne(0))
                {
                    break;
                }

                if(CheckHalted())
                {
                    if(ClockSource.HasEntries)
                    {
                        try
                        {
                            var timeToSleep = new TimeSpan(Time.Consts.TimeQuantum.Ticks * (long)ClockSource.NearestLimitIn);
                            var timeToSleepInMs = Math.Min(int.MaxValue, (int)timeToSleep.TotalMilliseconds);
                            if(timeToSleepInMs > 0)
                            {
                                if(!AdvanceImmediately)
                                {
                                    WaitHandle.WaitAny(waitHandles, timeToSleepInMs);
                                }
                                ClockSource.Advance((ulong)Time.Utilities.SecondsToTicks(timeToSleepInMs / 1000.0));
                            }
                            else
                            {
                                ClockSource.Advance(ClockSource.NearestLimitIn);
                            }
                        }
                        catch(OperationCanceledException)
                        {
                            advanceShouldBeRestarted = true;
                            break;
                        }
                    }
                    else
                    {
                        WaitHandle.WaitAny(waitHandles);
                    }
                }
            }
        }

        // TODO
        private object lck = new object();

        protected virtual bool IsSecondary
        {
            get
            {
                return Slot > 0;
            }
        }

        protected void ResetInterruptEvent(int number)
        {
            interruptEvents[number].Reset();
        }

        private void HandleStepping(bool force = false)
        {
            lock(sync.Guard)
            {
                if(ExecutionMode != ExecutionMode.SingleStep || (!force && skipNextStepping))
                {
                    return;
                }

                this.NoisyLog("Waiting for another step (PC=0x{0:X8}).", PC);
                InvokeHalted(new HaltArguments(HaltReason.Step));
                sync.SignalAndWait();
            }
        }

        [Export]
        private void OnBlockBegin(uint address, uint size)
        {
            ReactivateHooks();
            HandleStepping();
            skipNextStepping = false;

            var bbInternalHook = blockBeginInternalHook;
            if(bbInternalHook != null)
            {
                bbInternalHook(address, size);
            }
            var bbUserHook = blockBeginUserHook;
            if(bbUserHook != null)
            {
                bbUserHook(address, size);
            }
        }

        protected virtual void InitializeRegisters()
        {
        }

        protected readonly Machine machine;

        protected Symbol DoLookupSymbolInner(uint offset)
        {
            Symbol symbol;
            if(machine.SystemBus.Lookup.TryGetSymbolByAddress(offset, out symbol))
            {
                return symbol;
            }
            return null;
        }

        private string GetSymbolName(uint offset)
        {
            var info = string.Empty;
            var s = DoLookupSymbolInner(offset);
            if(s != null && !string.IsNullOrEmpty(s.Name))
            {
                info = s.ToStringRelative(offset);
            }
            return info;
        }

        private void OnTranslationBlockFetch(uint offset)
        {
            this.DebugLog(() => {
                string info = GetSymbolName(offset);
                if (info != string.Empty) info = "- " + info;
                return string.Format("Fetching block @ 0x{0:X8} {1}", offset, info);
            });
        }

        [Export]
        private void OnTranslationCacheSizeChange(int realSize)
        {
            if(realSize != translationCacheSize)
            {
                translationCacheSize = realSize;
                this.Log(LogLevel.Warning, "Translation cache size was corrected to {0}B ({1}B).", Misc.NormalizeBinary(realSize), realSize);
            }
        }

        private void HandleRamSetup()
        {
            foreach(var mapping in currentMappings)
            {
                checked
                {
                    RegisterMemoryChecked(mapping.Segment.StartingOffset, mapping.Segment.Size);
                }
            }
        }

        public void Step(int count = 1, bool wait = true)
        {
            lock(sync.Guard)
            {
                if(ExecutionMode != ExecutionMode.SingleStep)
                {
                    throw new RecoverableException("Stepping is available in single step execution mode only.");
                }

                if(wait)
                {
                    sync.PassAndWait(count);
                }
                else
                {
                    sync.Pass(count);
                }
            }
        }

        public void AddHook(uint addr, Action<uint> hook)
        {
            lock(hooks)
            {
                if(!hooks.ContainsKey(addr))
                {
                    hooks[addr] = new HookDescriptor(this, addr);
                }

                hooks[addr].AddCallback(hook);
                this.DebugLog("Added hook @ 0x{0:X}", addr);
            }
        }

        public void RemoveHook(uint addr, Action<uint> hook)
        {
            lock(hooks)
            {
                HookDescriptor descriptor;
                if(!hooks.TryGetValue(addr, out descriptor) || !descriptor.RemoveCallback(hook))
                {
                    this.Log(LogLevel.Warning, "Tried to remove not existing hook from address 0x{0:x}", addr);
                    return;
                }
                if(descriptor.IsEmpty)
                {
                    hooks.Remove(addr);
                }
            }
        }

        public void InvalidateTranslationBlocks(IntPtr start, IntPtr end)
        {
            TlibInvalidateTranslationBlocks(start, end);
        }

        public void RemoveHooksAt(uint addr)
        {
            lock(hooks)
            {
                if(hooks.Remove(addr))
                {
                    TlibRemoveBreakpoint(addr);
                }
            }
        }

        [Conditional("DEBUG")]
        private void CheckCpuThreadId()
        {
            if(Thread.CurrentThread != cpuThread)
            {
                throw new ArgumentException(
                    string.Format("Method called from a wrong thread. Expected {0}, but got {1}",
                                  cpuThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId));
            }
        }

        public void EnterSingleStepModeSafely(HaltArguments args)
        {
            // this method should only be called from CPU thread,
            // but we should check it anyway
            CheckCpuThreadId();

            TlibSetPaused();
            InvokeInCpuThreadSafely(() =>
            {
                ExecutionMode = ExecutionMode.SingleStep;
                TlibClearPaused();
                if(args != null)
                {
                    InvokeHalted(args);
                }
            });
        }

        private readonly object pauseLock = new object();

        public string Model
        {
            get
            {
                return cpuType;
            }
        }

        public virtual void Dispose()
        {
            DisposeInner();
        }

        void DisposeInner(bool silent = false)
        {
            if(!silent)
            {
                this.NoisyLog("About to dispose CPU.");
            }
            if(!PauseEvent.WaitOne(0))
            {
                if(!silent)
                {
                    this.NoisyLog("Halting CPU.");
                }
                InnerPause(new HaltArguments(HaltReason.Abort));
            }
            started = false;
            if(!silent)
            {
                this.NoisyLog("Disposing translation library.");
            }
            RemoveAllHooks();
            TlibDispose();
            RenodeFreeHostBlocks();
            binder.Dispose();
            File.Delete(libraryFile);
            memoryManager.CheckIfAllIsFreed();
            StopGdbServer();
        }

        [Export]
        private void ReportAbort(string message)
        {
            this.Log(LogLevel.Error, "CPU abort [PC=0x{0:X}]: {1}.", PC, message);
            throw new CpuAbortException(message);
        }

        [Export]
        private int IsIoAccessed(uint address)
        {
            return pagesAccessedByIo.Contains(address & TlibGetPageSize()) ? 1 : 0;
        }

        public abstract string Architecture { get; }

        private void InitInterruptEvents()
        {
            var gpioAttr = GetType().GetCustomAttributes(true).First(x => x is GPIOAttribute) as GPIOAttribute;
            var numberOfGPIOInputs = gpioAttr.NumberOfInputs;
            interruptEvents = new ManualResetEvent[numberOfGPIOInputs];
            for(var i = 0; i < interruptEvents.Length; i++)
            {
                interruptEvents[i] = new ManualResetEvent(interruptState != null && interruptState[i]);
            }
        }

        private void Init()
        {
            memoryManager = new SimpleMemoryManager(this);
            PauseEvent = new ManualResetEvent(true);
            hooks = hooks ?? new Dictionary<uint, HookDescriptor>();
            sync = new Synchronizer();
            haltedFinishedEvent = new AutoResetEvent(false);
            waitHandles = interruptEvents.Cast<WaitHandle>().Union(new EventWaitHandle[] { PauseEvent, haltedFinishedEvent }).ToArray();

            if(currentMappings == null)
            {
                currentMappings = new List<SegmentMapping>();
            }
            onTranslationBlockFetch = OnTranslationBlockFetch;

            var libraryResource = string.Format("Antmicro.Renode.translate_{0}-{1}-{2}.so", IntPtr.Size * 8, Architecture, Endianness == Endianess.BigEndian ? "be" : "le");
            libraryFile = GetType().Assembly.FromResourceToTemporaryFile(libraryResource);

            binder = new NativeBinder(this, libraryFile);
            TlibSetTranslationCacheSize(checked((IntPtr)translationCacheSize));
            MaximumBlockSize = DefaultMaximumBlockSize;
            var result = TlibInit(cpuType);
            if(result == -1)
            {
                throw new InvalidOperationException("Unknown cpu type");
            }
            if(cpuState != null)
            {
                var statePtr = TlibExportState();
                Marshal.Copy(cpuState, 0, statePtr, cpuState.Length);
                AfterLoad(statePtr);
            }
            HandleRamSetup();
            foreach(var hook in hooks)
            {
                TlibAddBreakpoint(hook.Key);
            }
            RenodeSetCountThreshold(currentCountThreshold);
        }

        private void InvokeHalted(HaltArguments arguments)
        {
            var halted = Halted;
            if(halted != null)
            {
                halted(arguments);
            }
        }

        [Export]
        private void UpdateInstructionCounter(uint value)
        {
            ExecutedInstructions += value;
            var instructionsThisTurn = value + instructionCountResiduum;
            instructionCountResiduum = instructionsThisTurn % PerformanceInMips;
            // timer update can result in a pause; it should be precise at this point
            // because it happens after executing instructions in this block and those
            // instructions are accounted for at this point
            pauseGuard.Leave();
            ClockSource.Advance(instructionsThisTurn / PerformanceInMips);
            pauseGuard.Enter();
        }

        [Export]
        private uint IsBlockBeginEventEnabled()
        {
            return (blockBeginInternalHook != null || blockBeginUserHook != null || executionMode == ExecutionMode.SingleStep || isAnyInactiveHook) ? 1u : 0u;
        }

        private int oldMaximumBlockSize;

        [Transient]
        private ActionUInt32 onTranslationBlockFetch;
        private string cpuType;
        private byte[] cpuState;
        private bool isHalted;
        private bool isAborted;

        [Transient]
        private volatile bool started;

        [Transient]
        private Thread cpuThread;

        [Transient]
        protected ManualResetEvent PauseEvent;

        [Transient]
        private string libraryFile;

        [Transient]
        private Synchronizer sync;

        private int translationCacheSize;
        private readonly object translationCacheSync;

        [Transient]
        // the reference here is necessary for the timer to not be garbage collected
        #pragma warning disable 0414
        private Timer currentTimer;
        #pragma warning restore 0414

        [Transient]
        private ManualResetEvent[] interruptEvents;

        [Transient]
        private WaitHandle[] waitHandles;

        [Transient]
        private SimpleMemoryManager memoryManager;

        [Transient]
        private AutoResetEvent haltedFinishedEvent;

        public uint IRQ{ get { return TlibIsIrqSet(); } }

        [Export]
        private void TouchHostBlock(uint offset)
        {
            this.NoisyLog("Trying to find the mapping for offset 0x{0:X}.", offset);
            var mapping = currentMappings.FirstOrDefault(x => x.Segment.StartingOffset <= offset && offset < x.Segment.StartingOffset + x.Segment.Size);
            if(mapping == null)
            {
                throw new InvalidOperationException(string.Format("Could not find mapped segment for offset 0x{0:X}.", offset));
            }
            mapping.Segment.Touch();
            mapping.Touched = true;
            RebuildMemoryMappings();
        }

        private void RebuildMemoryMappings()
        {
            checked
            {
                var hostBlocks = currentMappings.Where(x => x.Touched).Select(x => x.Segment)
                    .Select(x => new HostMemoryBlock { Start = (uint)x.StartingOffset, Size = (uint)x.Size, HostPointer = x.Pointer })
                    .OrderBy(x => x.HostPointer.ToInt64()).ToArray();
                for(var i = 0; i < hostBlocks.Length; i++)
                {
                    var j = i;
                    hostBlocks[i].HostBlockStart = Array.FindIndex(hostBlocks, x => x.HostPointer == hostBlocks[j].HostPointer);
                }
                var blockBuffer = memoryManager.Allocate(Marshal.SizeOf(typeof(HostMemoryBlock))*hostBlocks.Length);
                BlitArray(blockBuffer, hostBlocks.OrderBy(x => x.HostPointer.ToInt64()).Cast<dynamic>().ToArray());
                RenodeSetHostBlocks(blockBuffer, hostBlocks.Length);
                memoryManager.Free(blockBuffer);
                this.NoisyLog("Memory mappings rebuilt, there are {0} host blocks now.", hostBlocks.Length);
            }
        }

        private void BlitArray(IntPtr targetPointer, dynamic[] structures)
        {
            var count = structures.Count();
            if(count == 0)
            {
                return;
            }
            var structureSize = Marshal.SizeOf(structures.First());
            var currentPtr = targetPointer;
            for(var i = 0; i < count; i++)
            {
                Marshal.StructureToPtr(structures[i], currentPtr + i*structureSize, false);
            }
        }

        [Export]
        private void InvalidateTbInOtherCpus(IntPtr start, IntPtr end)
        {
            var otherCpus = machine.SystemBus.GetCPUs().OfType<TranslationCPU>().Where(x => x != this);
            foreach(var cpu in otherCpus)
            {
                cpu.TlibInvalidateTranslationBlocks(start, end);
            }
        }

        private CpuThreadPauseGuard ObtainPauseGuard(bool forReading, long address)
        {
            pauseGuard.Initialize(forReading, address);
            return pauseGuard;
        }

        #region Memory trampolines

        [Export]
        private IntPtr Allocate(int size)
        {
            return memoryManager.Allocate(size);
        }

        [Export]
        private IntPtr Reallocate(IntPtr oldPointer, int newSize)
        {
            return memoryManager.Reallocate(oldPointer, newSize);
        }

        [Export]
        private void Free(IntPtr pointer)
        {
            memoryManager.Free(pointer);
        }

        #endregion

        protected readonly BaseClockSource ClockSource;

        private bool[] interruptState;
        private int currentCountThreshold;
        private Action<uint, uint> blockBeginInternalHook;
        private Action<uint, uint> blockBeginUserHook;

        private List<SegmentMapping> currentMappings;

        private readonly CpuThreadPauseGuard pauseGuard;

        [Transient]
        private NativeBinder binder;

        protected class RegisterAttribute : Attribute
        {

        }

        private class SimpleMemoryManager
        {
            public SimpleMemoryManager(TranslationCPU parent)
            {
                this.parent = parent;
                ourPointers = new ConcurrentDictionary<IntPtr, int>();
            }

            public IntPtr Allocate(int size)
            {
                parent.NoisyLog("Trying to allocate {0}B.", Misc.NormalizeBinary(size));
                var ptr = Marshal.AllocHGlobal(size);
                if(!ourPointers.TryAdd(ptr, size))
                {
                    throw new InvalidOperationException("Allocated pointer already exists is memory database.");
                }
                Interlocked.Add(ref allocated, size);
                PrintAllocated();
                return ptr;
            }

            public IntPtr Reallocate(IntPtr oldPointer, int newSize)
            {
                if(oldPointer == IntPtr.Zero)
                {
                    return Allocate(newSize);
                }
                if(newSize == 0)
                {
                    Free(oldPointer);
                    return IntPtr.Zero;
                }
                int oldSize;
                if(!ourPointers.TryRemove(oldPointer, out oldSize))
                {
                    throw new InvalidOperationException("Trying to reallocate pointer which wasn't allocated by this memory manager.");
                }
                parent.NoisyLog("Trying to reallocate: old size {0}B, new size {1}B.", Misc.NormalizeBinary(newSize), Misc.NormalizeBinary(oldSize));
                var ptr = Marshal.ReAllocHGlobal(oldPointer, (IntPtr)newSize); // before asking WTF here look at msdn
                Interlocked.Add(ref allocated, newSize - oldSize);
                ourPointers.TryAdd(ptr, newSize);
                return ptr;
            }

            public void Free(IntPtr ptr)
            {
                int oldSize;
                if(!ourPointers.TryRemove(ptr, out oldSize))
                {
                    throw new InvalidOperationException("Trying to free pointer \"{0}\" which wasn't allocated by this memory manager.".FormatWith(ptr));
                }
                Marshal.FreeHGlobal(ptr);
                Interlocked.Add(ref allocated, -oldSize);
            }

            public long Allocated
            {
                get
                {
                    return allocated;
                }
            }

            public void CheckIfAllIsFreed()
            {
                if(!ourPointers.IsEmpty)
                {
                    throw new InvalidOperationException("Some memory allocated by the translation library was not freed.");
                }
            }

            private void PrintAllocated()
            {
                Logger.LogAs(this, LogLevel.Noisy, "Allocated is now {0}B.", Misc.NormalizeBinary(Interlocked.Read(ref allocated)));
            }

            private ConcurrentDictionary<IntPtr, int> ourPointers;
            private long allocated;
            private readonly TranslationCPU parent;
        }

        private sealed class CpuThreadPauseGuard : IDisposable
        {
            public CpuThreadPauseGuard(TranslationCPU parent)
            {
                guard = new ThreadLocal<object>();
                blockRestartReached = new ThreadLocal<bool>();
                this.parent = parent;
            }

            public void Enter()
            {
                active = true;
            }

            public void Leave()
            {
                active = false;
            }

            public void Initialize(bool forReading, long address)
            {
                guard.Value = new object();
                if(parent.machine.SystemBus.IsWatchpointAt(address, forReading ? Access.Read : Access.Write))
                {
                    /*
                     * In general precise pause works as follows:
                     * - translation libraries execute an instruction that reads/writes to/from memory
                     * - the execution is then transferred to the system bus (to process memory access)
                     * - we check whether the accessed address can contain hook (IsWatchpointAt)
                     * - if it can, we invalidate the block and issue retranslation of the code at current PC - but limiting block size to 1 instruction
                     * - we exit the cpu loop so that newly translated block will be executed now
                     * - because the mentioned memory access is executed again, we reach this point for the second time
                     * - but now we can simply do nothing; because the executed block is of size 1, the pause will be precise
                     */
                    var wasReached = blockRestartReached.Value;
                    blockRestartReached.Value = true;
                    if(!wasReached)
                    {
                        // we're here for the first time
                        parent.TlibRestartTranslationBlock();
                        // note that on the line above we effectively exit the function so the stuff below is not executed
                    }
                    // since the translation block is now short, we can simply continue
                    blockRestartReached.Value = false;
                }
            }

            public void OrderPause()
            {
                if(active && guard.Value == null)
                {
                    throw new InvalidOperationException("Trying to order pause without prior guard initialization on this thread.");
                }
            }

            void IDisposable.Dispose()
            {
                guard.Value = null;
            }

            [Constructor]
            private readonly ThreadLocal<object> guard;

            [Constructor]
            private readonly ThreadLocal<bool> blockRestartReached;

            private readonly TranslationCPU parent;
            private bool active;
        }

        protected enum Interrupt
        {
            Hard = 0x02,
            TargetExternal0 = 0x08,
            TargetExternal1 = 0x10
        }

        private class SegmentMapping
        {
            public IMappedSegment Segment { get; private set; }
            public bool Touched { get; set; }

            public SegmentMapping(IMappedSegment segment)
            {
                Segment = segment;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HostMemoryBlock
        {
            public uint Start;
            public uint Size;
            public IntPtr HostPointer;
            public int HostBlockStart;
        }

        #region IDisassemblable implementation

        public Symbol SymbolLookup(uint addr)
        {
            return DoLookupSymbolInner(addr);
        }

        private bool logTranslatedBlocks;
        public bool LogTranslatedBlocks
        {
            get
            {
                return logTranslatedBlocks;
            }

            set
            {
                if (LogFile == null)
                {
                    throw new RecoverableException("Log file not set. Nothing will be logged.");
                }
                logTranslatedBlocks = value;
                TlibSetOnBlockTranslationEnabled(value ? 1 : 0);
            }
        }

        public string Disassembler
        {
            get
            {
                return DisasEngine.CurrentDisassemblerType;
            }

            set
            {
                if(!TrySetDisassembler(value))
                {
                    throw new RecoverableException(string.Format("Could not create disassembler of type: {0}. Are you missing an extension library or a plugin?", value));
                }
            }
        }

        private bool TrySetDisassembler(string type)
        {
            IDisassembler disas = null;
            if(!string.IsNullOrEmpty(type))
            {
                disas = DisassemblerManager.Instance.CreateDisassembler(type, this);
                if(disas == null)
                {
                    return false;
                }
            }

            DisasEngine.SetDisassembler(disas);
            return true;
        }

        public string[] AvailableDisassemblers
        {
            get { return DisassemblerManager.Instance.GetAvailableDisassemblers(Architecture); }
        }

        public uint TranslateAddress(uint logicalAddress)
        {
            return TlibTranslateToPhysicalAddress(logicalAddress);
        }

        [PostDeserialization]
        protected void InitDisas()
        {
            DisasEngine = new DisassemblyEngine(this, TranslateAddress);
            var diss = AvailableDisassemblers;
            if (diss.Length > 0)
            {
                TrySetDisassembler(diss[0]);
            }
        }

        #endregion

        public bool IsStarted
        {
            get
            {
                return started;
            }
        }

        public virtual bool IsHalted
        {
            get
            {
                lock(haltedFinishedEvent)
                {
                    return isHalted;
                }
            }
            set
            {
                lock(haltedFinishedEvent)
                {
                    if(value == isHalted)
                    {
                        return;
                    }
                    var isHaltedChanged = IsHaltedChanged;
                    if(isHaltedChanged != null)
                    {
                        isHaltedChanged(value);
                    }
                    isHalted = value;
                    if(isHalted)
                    {
                        InvokeHalted(new HaltArguments(HaltReason.Pause));
                    }
                    if(!value)
                    {
                        haltedFinishedEvent.Set();
                    }
                }
            }
        }

        protected virtual void BeforeSave(IntPtr statePtr)
        {
        }

        protected virtual void AfterLoad(IntPtr statePtr)
        {
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private FuncInt32String TlibInit;

        [Import]
        private Action TlibDispose;

        [Import]
        private Action TlibReset;

        [Import]
        private FuncInt32 TlibExecute;

        [Import]
        protected Action TlibRestartTranslationBlock;

        [Import]
        private Action TlibSetPaused;

        [Import]
        private Action TlibClearPaused;

        [Import]
        private FuncInt32 TlibIsWfi;

        [Import]
        private FuncUInt32 TlibGetPageSize;

        [Import]
        private ActionUInt32UInt32 TlibMapRange;

        [Import]
        private ActionUInt32UInt32 TlibUnmapRange;

        [Import]
        private FuncUInt32UInt32UInt32 TlibIsRangeMapped;

        [Import]
        private ActionIntPtrIntPtr TlibInvalidateTranslationBlocks;

        [Import]
        protected FuncUInt32UInt32 TlibTranslateToPhysicalAddress;

        [Import]
        private ActionIntPtrInt32 RenodeSetHostBlocks;

        [Import]
        private Action RenodeFreeHostBlocks;

        [Import]
        private ActionInt32 RenodeSetCountThreshold;

        [Import]
        private ActionInt32Int32 TlibSetIrq;

        [Import]
        private FuncUInt32 TlibIsIrqSet;

        [Import]
        private ActionUInt32 TlibAddBreakpoint;

        [Import]
        private ActionUInt32 TlibRemoveBreakpoint;

        [Import]
        private ActionIntPtr RenodeAttachLogTranslationBlockFetch;

        [Import]
        private ActionInt32 TlibSetOnBlockTranslationEnabled;

        [Import]
        private ActionIntPtr TlibSetTranslationCacheSize;

        [Import]
        private Action TlibInvalidateTranslationCache;

        [Import]
        private FuncUInt32UInt32 TlibSetMaximumBlockSize;

        [Import]
        private FuncUInt32 TlibGetMaximumBlockSize;

        [Import]
        private Action TlibRestoreContext;

        [Import]
        private FuncIntPtr TlibExportState;

        [Import]
        private FuncInt32 TlibGetStateSize;

        [Import]
        private ActionInt32 RenodeSetBlockTrimming;

        [Import]
        private FuncInt32 RenodeGetBlockTrimming;
        #pragma warning restore 649

        private readonly HashSet<long> pagesAccessedByIo;

        protected const int DefaultTranslationCacheSize = 32 * 1024 * 1024;

        [Export]
        private void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        private void LogDisassembly(uint pc, uint count, uint flags)
        {
            DisasEngine.LogSymbol(pc, count, flags);
        }

        [Export]
        private int GetCpuIndex()
        {
            return Slot;
        }

        private bool CheckHalted()
        {
            lock(haltedFinishedEvent)
            {
                return isHalted || TlibIsWfi() > 0;
            }
        }

        public string DisassembleBlock(uint addr, uint flags = 0)
        {
            var block = DisasEngine.Disassemble(addr, true, 10 * 4, flags);
            return block != null ? block.Replace("\n", "\r\n") : string.Empty;
        }

        [Transient]
        protected DisassemblyEngine DisasEngine;

        private bool advanceShouldBeRestarted;

        // Execution of a code in CPU is performed by tlib (implemented in C) that is called
        // from C# in TranslationCPU.CpuLoop using TlibExecute method. For better performance,
        // tlib groups instructions in so-called blocks that are executed atomically from C# code
        // perspective (with some exceptions regarding hooks, of course). Sometimes it is even
        // possible for multiple blocks to be executed one-by-one without leaving single TlibExecute call.
        //
        // In order to achieve code execution with precision up to a single instruction, maximum
        // block size must be set to one.This, however, does not prevent block chaining from happening.
        // As a result, there is still no guarantee that TlibExecute finishes after each instruction,
        // even with minimal possible block size.
        //
        // Fortunately, there is OnBlockBegin hook that is called before executing instructions from
        // each block - as a result, C# is notified about each block even when chaining is active.
        //
        // SingleStepping and halting on Hooks (both watchpoints and breakpoints) is achieved by
        // blocking CpuLoop on sync guard located in OnBlockBegin hook. Thanks to that we are
        // able to control an execution of CPU precisely with block chaining being active.
        //
        // Unfortunately, there is one problem with this approach when it comes to breakpoints.
        // When it is inserted, the whole instruction block is re-translated in such a way that it ends
        // with special trap instruction generated at the breakpoint's address. As a result, it is naturally
        // guaranteed to leave C-code when hitting it. Removing breakpoint requires another retranslation
        // to remove this trap instruction.
        //
        // Adding/removing breakpoints when tlib is not executing is safe and works well, but problems
        // arise when managing them from within hooks.As mentioned earlier, CpuLoop is halted on
        // OnBlockBegin hook, which means that the block is already executing. As a result removing
        // breakpoint at this moment will not prevent executing trap instruction in this block again
        // leading to breaking on non-existing breakpoint.
        //
        // To solve this problem method HandleStepping is executed two times:
        //   (1) before entering tlib code (to handle breakpoints)
        //   (2) at the beginning of each block (to handle stepping)
        //
        // As a result, it is executed twice for the first block.To avoid it we have special flag
        // skipNextStepping which is set to true after(1) and cleared after first(2).
        private bool skipNextStepping;

        protected static readonly Exception InvalidInterruptNumberException = new InvalidOperationException("Invalid interrupt number.");

        private const int DefaultMaximumBlockSize = 0x7FF;
        private const int BreakpointResult = 0x10002;
        private const int HaltedResult = 0x10003;

        private void ExecuteHooks(uint address)
        {
            lock(hooks)
            {
                HookDescriptor hookDescriptor;
                if(!hooks.TryGetValue(address, out hookDescriptor))
                {
                    return;
                }

                this.DebugLog("Executing hooks registered at address 0x{0:X8}", address);
                hookDescriptor.ExecuteCallbacks();
            }
        }

        private void DeactivateHooks(uint address)
        {
            lock(hooks)
            {
                HookDescriptor hookDescriptor;
                if(!hooks.TryGetValue(address, out hookDescriptor))
                {
                    return;
                }
                hookDescriptor.Deactivate();
                isAnyInactiveHook = true;
            }
        }

        private void ReactivateHooks()
        {
            lock(hooks)
            {
                foreach(var inactive in hooks.Where(x => !x.Value.IsActive))
                {
                    inactive.Value.Activate();
                }
                isAnyInactiveHook = false;
            }
        }

        public void RemoveAllHooks()
        {
            lock(hooks)
            {
                foreach(var hook in hooks)
                {
                    TlibRemoveBreakpoint(hook.Key);
                }
                hooks.Clear();
                isAnyInactiveHook = false;
            }
        }

        private bool isAnyInactiveHook;
        private Dictionary<uint, HookDescriptor> hooks;

        private class HookDescriptor
        {
            public HookDescriptor(TranslationCPU cpu, uint address)
            {
                this.cpu = cpu;
                this.address = address;
                callbacks = new HashSet<Action<uint>>();
            }

            public void ExecuteCallbacks()
            {
                foreach(var callback in callbacks)
                {
                    callback(address);
                }
            }

            public void AddCallback(Action<uint> action)
            {
                callbacks.Add(action);
                Activate();
            }

            public bool RemoveCallback(Action<uint> action)
            {
                var result = callbacks.Remove(action);
                if(result && IsEmpty)
                {
                    Deactivate();
                }
                return result;
            }

            /// <summary>
            /// Activates the hook by installing it in tlib.
            /// </summary>
            public void Activate()
            {
                if(IsActive)
                {
                    return;
                }

                cpu.TlibAddBreakpoint(address);
                IsActive = true;
            }

            /// <summary>
            /// Deactivates the hook by removing it from tlib.
            /// </summary>
            public void Deactivate()
            {
                if(!IsActive)
                {
                    return;
                }

                cpu.TlibRemoveBreakpoint(address);
                IsActive = false;
            }

            public bool IsEmpty { get { return !callbacks.Any(); } }
            public bool IsActive { get; private set; }

            private readonly uint address;
            private readonly TranslationCPU cpu;
            private readonly HashSet<Action<uint>> callbacks;
        }

        private class Synchronizer
        {
            public Synchronizer()
            {
                guard = new object();
            }

            public void SignalAndWait()
            {
                lock(guard)
                {
                    if(counter > 0)
                    {
                        counter--;
                    }
                    if(counter == 0)
                    {
                        Monitor.Pulse(guard);
                    }
                    do
                    {
                        Monitor.Wait(guard);
                    }
                    while(counter == 0);
                }
            }

            public void PassAndWait(int steps = 1)
            {
                lock(guard)
                {
                    counter = steps;
                    Monitor.Pulse(guard);

                    do
                    {
                        Monitor.Wait(guard);
                    }
                    while(counter > 0);
                }
            }

            public void Pass(int steps = 1)
            {
                lock(guard)
                {
                    counter = steps;
                    Monitor.Pulse(guard);
                }
            }

            public void Wait()
            {
                lock(guard)
                {
                    Monitor.Wait(guard);
                }
            }

            public object Guard
            {
                get
                {
                    return guard;
                }
            }

            private int counter;
            private readonly object guard;
        }
    }
}

