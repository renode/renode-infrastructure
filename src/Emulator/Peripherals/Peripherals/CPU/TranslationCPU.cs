//
// Copyright (c) 2010-2018 Antmicro
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
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
    public abstract class TranslationCPU : IdentifiableObject, IGPIOReceiver, ICpuSupportingGdb, IDisposable, IDisassemblable, ITimeSink
    {
        public Endianess Endianness { get; protected set; }

        protected TranslationCPU(string cpuType, Machine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
		: this(0, cpuType, machine, endianness, bitness)
        {
        }

        protected TranslationCPU(uint id, string cpuType, Machine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
        {
            Id = id;

            if(cpuType == null)
            {
                throw new RecoverableException(new ArgumentNullException("cpuType"));
            }

            Endianness = endianness;
            PerformanceInMips = 100;
            this.cpuType = cpuType;
            this.translationCacheSize = DefaultTranslationCacheSize;
            this.machine = machine;
            this.bitness = bitness;
            started = false;
            isHalted = false;
            translationCacheSync = new object();
            pagesAccessedByIo = new HashSet<ulong>();
            pauseGuard = new CpuThreadPauseGuard(this);
            decodedIrqs = new Dictionary<Interrupt, HashSet<int>>();
            hooks = new Dictionary<ulong, HookDescriptor>();
            currentMappings = new List<SegmentMapping>();
            isPaused = true;
            InitializeRegisters();
            Init();
            InitDisas();
        }

        public bool TbCacheEnabled
        {
            get
            {
                return TlibGetTbCacheEnabled() != 0;
            }

            set
            {
                TlibSetTbCacheEnabled(value ? 1u : 0u);
            }
        }

        public bool ChainingEnabled
        {
            get
            {
                return TlibGetChainingEnabled() != 0;
            }

            set
            {
                TlibSetChainingEnabled(value ? 1u : 0u);
            }
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

        public ulong TranslationCacheSize
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

        public int MaximumBlockSize
        {
            get
            {
                return checked((int)TlibGetMaximumBlockSize());
            }
            set
            {
                TlibSetMaximumBlockSize(checked((uint)value));
                ClearTranslationCache();
            }
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

        public bool ThreadSentinelEnabled { get; set; }

        private bool logTranslationBlockFetchEnabled;

        public ulong ExecutedInstructions { get; private set; }

        public int Slot { get{if(!slot.HasValue) slot = machine.SystemBus.GetCPUId(this); return slot.Value;} private set {slot = value;} }
        private int? slot;

        public void ClearTranslationCache()
        {
            using(machine.ObtainPausedState())
            {
                TlibInvalidateTranslationCache();
            }
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

        private void SubmitTranslationCacheSizeUpdate()
        {
            lock(translationCacheSync)
            {
                var currentTCacheSize = translationCacheSize;
                // disabled until segmentation fault will be resolved
                currentTimer = new Timer(x => UpdateTranslationCacheSize(currentTCacheSize), null, -1, -1);
            }
        }

        private void UpdateTranslationCacheSize(ulong sizeAtThatTime)
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
            var statePtr = TlibExportState();
            BeforeSave(statePtr);
            cpuState = new byte[TlibGetStateSize()];
            Marshal.Copy(statePtr, cpuState, 0, cpuState.Length);
        }

        [PostSerialization]
        private void FreeState()
        {
            cpuState = null;
        }

        [LatePostDeserialization]
        private void RestoreState()
        {
            Init();
            // TODO: state of the reset events
            FreeState();
        }

        public ExecutionMode ExecutionMode
        {
            get
            {
                lock(singleStepSynchronizer.Guard)
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

                lock(singleStepSynchronizer.Guard)
                {
                    executionMode = value;
                    singleStepSynchronizer.Enabled = (executionMode == ExecutionMode.SingleStep);
                    UpdateBlockBeginHookPresent();
                    IsHalted = (executionMode == ExecutionMode.SingleStep);
                }
            }
        }

        [Transient]
        private ExecutionMode executionMode;

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

        public SystemBus Bus
        {
            get
            {
                return machine.SystemBus;
            }
        }

        public void Pause()
        {
            InnerPause(new HaltArguments(HaltReason.Pause, Id));
        }

        private void RequestPause()
        {
            lock(pauseLock)
            {
                isPaused = true;
                this.Trace("Requesting pause");
                TlibSetReturnRequest();
            }
        }

        private void InnerPause(HaltArguments haltArgs)
        {
            if(isAborted || isPaused)
            {
                // cpu is already paused or aborted
                return;
            }

            lock(pauseLock)
            {
                // cpuThread can get null as a result of `RequestPause` call
                var cpuThreadCopy = cpuThread;
                RequestPause();

                if(cpuThreadCopy != null && Thread.CurrentThread.ManagedThreadId != cpuThreadCopy.ManagedThreadId)
                {
                    singleStepSynchronizer.Enabled = false;
                    this.NoisyLog("Waiting for thread to pause.");
                    cpuThreadCopy?.Join();
                    this.NoisyLog("Paused.");
                }
                // calling pause from block begin/end hook is safe and we should not check pauseGuard in this context
                else if(!insideBlockHook)
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
                if(isAborted || !isPaused)
                {
                    return;
                }
                started = true;
                singleStepSynchronizer.Enabled = (ExecutionMode == ExecutionMode.SingleStep);
                isPaused = false;
                StartCPUThread();
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
                if(ThreadSentinelEnabled)
                {
                    CheckIfOnSynchronizedThread();
                }
                this.NoisyLog("IRQ {0}, value {1}", number, value);
                // as we are waiting for an interrupt we should, obviously, not mask it
                if(started && (lastTlibResult == ExecutionResult.WaitingForInterrupt || !(DisableInterruptsWhileStepping && executionMode == ExecutionMode.SingleStep)))
                {
                    TlibSetIrqWrapped(number, value);
                }
            }
        }

        public virtual RegisterValue PC
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
            if(segment.StartingOffset > bitness.GetMaxAddress() || segment.Size > bitness.GetMaxAddress())
            {
                throw new RecoverableException("Could not map memory segment: starting offset or size are too high");
            }

            using(machine.ObtainPausedState())
            {
                currentMappings.Add(new SegmentMapping(segment));
                RegisterMemoryChecked(segment.StartingOffset, segment.Size);
                checked
                {
                    TranslationCacheSize = 0;
                    foreach(var mapping in currentMappings)
                    {
                        TranslationCacheSize += mapping.Segment.Size;
                    }
                    TranslationCacheSize /= 4;
                }
            }
        }

        public void UnmapMemory(Range range)
        {
            using(machine.ObtainPausedState())
            {
                var startAddress = range.StartAddress;
                var endAddress = range.EndAddress - 1;
                ValidateMemoryRangeAndThrow(startAddress, range.Size);

                // when unmapping memory, two things has to be done
                // first is to flag address range as no-memory (that is, I/O)
                TlibUnmapRange(startAddress, endAddress);

                // and second is to remove mappings that are not used anymore
                currentMappings = currentMappings.
                    Where(x => TlibIsRangeMapped(x.Segment.StartingOffset, x.Segment.StartingOffset + x.Segment.Size) == 1).ToList();
            }
        }

        public void SetPageAccessViaIo(ulong address)
        {
            pagesAccessedByIo.Add(address & ~(TlibGetPageSize() - 1));
            TlibFlushPage(address);
        }

        public void ClearPageAccessViaIo(ulong address)
        {
            pagesAccessedByIo.Remove(address & ~(TlibGetPageSize() - 1));
            TlibFlushPage(address);
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
            // using string builder here is due to performance reasons: test shows that string.Format is much slower
            var messageBuilder = new StringBuilder(256);

            SetInternalHookAtBlockBegin((pc, size) =>
            {
                var name = Bus.FindSymbolAt(pc);

                if(spaceSeparatedPrefixes != "" && (name == null || !prefixesAsArray.Any(name.StartsWith)))
                {
                    return;
                }
                messageBuilder.Clear();
                this.Log(LogLevel.Info, messageBuilder.Append("Entering function ").Append(name ?? "without name").Append(" at 0x").Append(pc.ToString("X")).ToString());
            });
        }

        // TODO: improve this when backend/analyser stuff is done

        public bool UpdateContextOnLoadAndStore { get; set; }

        protected abstract Interrupt DecodeInterrupt(int number);

        public void ClearHookAtBlockBegin()
        {
            SetHookAtBlockBegin(null);
        }

        public void SetHookAtBlockBegin(Action<ulong, uint> hook)
        {
            using(machine.ObtainPausedState())
            {
                if((hook == null) ^ (blockBeginUserHook == null))
                {
                    ClearTranslationCache();
                }
                blockBeginUserHook = hook;
                UpdateBlockBeginHookPresent();
            }
        }

        public void SetHookAtBlockEnd(Action<ulong, uint> hook)
        {
            using(machine.ObtainPausedState())
            {
                if((hook == null) ^ (blockFinishedHook == null))
                {
                    ClearTranslationCache();
                    TlibSetBlockFinishedHookPresent(hook != null ? 1u : 0u);
                }
                blockFinishedHook = hook;
            }
        }

        [Export]
        protected uint ReadByteFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForReading(offset, SysbusAccessWidth.Byte))
            {
                return machine.SystemBus.ReadByte(offset);
            }
        }

        [Export]
        protected uint ReadWordFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForReading(offset, SysbusAccessWidth.Word))
            {
                return machine.SystemBus.ReadWord(offset);
            }
        }

        [Export]
        protected uint ReadDoubleWordFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForReading(offset, SysbusAccessWidth.DoubleWord))
            {
                return machine.SystemBus.ReadDoubleWord(offset);
            }
        }

        [Export]
        protected void WriteByteToBus(ulong offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForWriting(offset, SysbusAccessWidth.Byte, value))
            {
                machine.SystemBus.WriteByte(offset, unchecked((byte)value));
            }
        }

        [Export]
        protected void WriteWordToBus(ulong offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForWriting(offset, SysbusAccessWidth.Word, value))
            {
                machine.SystemBus.WriteWord(offset, unchecked((ushort)value));
            }
        }

        [Export]
        protected void WriteDoubleWordToBus(ulong offset, uint value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                UpdateContext();
            }
            using(ObtainPauseGuardForWriting(offset, SysbusAccessWidth.DoubleWord, value))
            {
                machine.SystemBus.WriteDoubleWord(offset, value);
            }
        }

        public abstract void SetRegisterUnsafe(int register, ulong value);

        public abstract RegisterValue GetRegisterUnsafe(int register);

        public abstract IEnumerable<CPURegister> GetRegisters();

        private void SetInternalHookAtBlockBegin(Action<ulong, uint> hook)
        {
            using(machine.ObtainPausedState())
            {
                if((hook == null) ^ (blockBeginInternalHook == null))
                {
                    ClearTranslationCache();
                }
                blockBeginInternalHook = hook;
                UpdateBlockBeginHookPresent();
            }
        }

        private void CheckIfOnSynchronizedThread()
        {
            if(Thread.CurrentThread.ManagedThreadId != cpuThread.ManagedThreadId)
            {
                this.Log(LogLevel.Warning, "An interrupt from the unsynchronized thread.");
            }
        }

        private void RegisterMemoryChecked(ulong offset, ulong size)
        {
            checked
            {
                ValidateMemoryRangeAndThrow(offset, size);
                TlibMapRange(offset, size);
                this.NoisyLog("Registered memory at 0x{0:X}, size 0x{1:X}.", offset, size);
            }
        }

        private void ValidateMemoryRangeAndThrow(ulong startAddress, ulong size)
        {
            var pageSize = TlibGetPageSize();
            if((startAddress % pageSize) != 0)
            {
                throw new RecoverableException("Memory offset has to be aligned to guest page size.");
            }
            if(size % pageSize != 0)
            {
                throw new RecoverableException("Memory size has to be aligned to guest page size.");
            }
        }

        private void SetPCFromEntryPoint(ulong entryPoint)
        {
            var what = machine.SystemBus.WhatIsAt(entryPoint);
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

        private void InvokeInCpuThreadSafely(Action a)
        {
            actionsToExecuteOnCpuThread.Enqueue(a);
        }

        private ConcurrentQueue<Action> actionsToExecuteOnCpuThread = new ConcurrentQueue<Action>();
        private ExecutionResult lastTlibResult;

        // TODO
        private object lck = new object();

        protected virtual bool IsSecondary
        {
            get
            {
                return Slot > 0;
            }
        }

        private bool insideBlockHook;

        [Export]
        private uint OnBlockBegin(ulong address, uint size)
        {
            ReactivateHooks();

            using(DisposableWrapper.New(() => insideBlockHook = false))
            {
                insideBlockHook = true;

                blockBeginInternalHook?.Invoke(address, size);
                blockBeginUserHook?.Invoke(address, size);
            }

            return (isHalted || isPaused) ? 0 : 1u;
        }

        [Export]
        private void OnBlockFinished(ulong pc, uint executedInstructions)
        {
            using(DisposableWrapper.New(() => insideBlockHook = false))
            {
                insideBlockHook = true;
                blockFinishedHook?.Invoke(pc, executedInstructions);
            }
        }

        protected virtual void InitializeRegisters()
        {
        }

        protected readonly Machine machine;

        private void OnTranslationBlockFetch(ulong offset)
        {
            this.DebugLog(() => {
                string info = Bus.FindSymbolAt(offset);
                if (info != string.Empty) info = "- " + info;
                return string.Format("Fetching block @ 0x{0:X8} {1}", offset, info);
            });
        }

        [Export]
        private void OnTranslationCacheSizeChange(ulong realSize)
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

        public ulong Step(int count = 1)
        {
            lock(singleStepSynchronizer.Guard)
            {
                ExecutionMode = ExecutionMode.SingleStep;
                Resume();

                this.Log(LogLevel.Noisy, "Stepping {0} step(s)", count);
                if(IsHalted)
                {
                    IsHalted = false;
                }
                singleStepSynchronizer.CommandStep(count);
                singleStepSynchronizer.WaitForStepFinished();
                IsHalted = true;

                return PC;
            }
        }

        public void AddHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook)
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

        public void RemoveHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook)
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
            if(disposing)
            {
                return;
            }
            TlibInvalidateTranslationBlocks(start, end);
        }

        public void RemoveHooksAt(ulong addr)
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
            ExecutionMode = ExecutionMode.SingleStep;
            InvokeHalted(args);
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
            disposing = true;
            if(!silent)
            {
                this.NoisyLog("About to dispose CPU.");
            }
            InnerPause(new HaltArguments(HaltReason.Abort, Id));
            TimeHandle.Dispose();
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
        }

        [Export]
        private void ReportAbort(string message)
        {
            this.Log(LogLevel.Error, "CPU abort [PC=0x{0:X}]: {1}.", PC.RawValue, message);
            throw new CpuAbortException(message);
        }

        [Export]
        private int IsIoAccessed(ulong address)
        {
            return pagesAccessedByIo.Contains(address & ~(TlibGetPageSize() - 1)) ? 1 : 0;
        }

        public abstract string Architecture { get; }

        public abstract string GDBArchitecture { get; }

        public bool DebuggerConnected { get; set; }

        public uint Id { get; }

        public string Name => this.GetCPUThreadName(machine);

        private void Init()
        {
            memoryManager = new SimpleMemoryManager(this);
            isPaused = true;
            singleStepSynchronizer = new Synchronizer();
            haltedLock = new object();

            onTranslationBlockFetch = OnTranslationBlockFetch;

            var libraryResource = string.Format("Antmicro.Renode.translate-{0}-{1}.so", Architecture, Endianness == Endianess.BigEndian ? "be" : "le");
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
            TlibAtomicMemoryStateInit(checked((int)this.Id), machine.AtomicMemoryStatePointer);
            HandleRamSetup();
            foreach(var hook in hooks)
            {
                TlibAddBreakpoint(hook.Key);
            }
        }

        private void InvokeHalted(HaltArguments arguments)
        {
            var halted = Halted;
            if(halted != null)
            {
                halted(arguments);
            }
        }

        [Transient]
        private ActionUInt64 onTranslationBlockFetch;
        private string cpuType;
        private byte[] cpuState;
        private bool isHalted;
        private bool isAborted;
        private bool isPaused;

        [Transient]
        private volatile bool started;

        [Transient]
        private bool disposing;

        [Transient]
        private Thread cpuThread;

        [Transient]
        private string libraryFile;

        [Transient]
        private Synchronizer singleStepSynchronizer;

        private ulong translationCacheSize;
        private readonly object translationCacheSync;

        [Transient]
        // the reference here is necessary for the timer to not be garbage collected
        #pragma warning disable 0414
        private Timer currentTimer;
        #pragma warning restore 0414

        [Transient]
        private SimpleMemoryManager memoryManager;

        private object haltedLock;

        public uint IRQ{ get { return TlibIsIrqSet(); } }

        [Export]
        private void TouchHostBlock(ulong offset)
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
                    .Select(x => new HostMemoryBlock { Start = x.StartingOffset, Size = x.Size, HostPointer = x.Pointer })
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
                cpu.InvalidateTranslationBlocks(start, end);
            }
        }

        private CpuThreadPauseGuard ObtainPauseGuardForReading(ulong address, SysbusAccessWidth width)
        {
            pauseGuard.InitializeForReading(address, width);
            return pauseGuard;
        }

        private CpuThreadPauseGuard ObtainPauseGuardForWriting(ulong address, SysbusAccessWidth width, uint value)
        {
            pauseGuard.InitializeForWriting(address, width, value);
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

        private Action<ulong, uint> blockBeginInternalHook;
        private Action<ulong, uint> blockBeginUserHook;
        private Action<ulong, uint> blockFinishedHook;

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
                parent.NoisyLog("Allocated is now {0}B.", Misc.NormalizeBinary(Interlocked.Read(ref allocated)));
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

            public void Initialize()
            {
                guard.Value = new object();
            }

            public void InitializeForWriting(ulong address, SysbusAccessWidth width, uint value)
            {
                Initialize(address, width, value);
            }

            public void InitializeForReading(ulong address, SysbusAccessWidth width)
            {
                Initialize(address, width, null);
            }

            private void Initialize(ulong address, SysbusAccessWidth width, uint? value)
            {
                Initialize();
                if(!parent.machine.SystemBus.TryGetWatchpointsAt(address, value.HasValue ? Access.Write : Access.Read, out var watchpoints))
                {
                    return;
                }

                /*
                    * In general precise pause works as follows:
                    * - translation libraries execute an instruction that reads/writes to/from memory
                    * - the execution is then transferred to the system bus (to process memory access)
                    * - we check whether there are any hooks registered for the accessed address (TryGetWatchpointsAt)
                    * - if there are (and we hit them for the first time) we call them and then invalidate the block and issue retranslation of the code at current PC
                    * - we exit the cpu loop so that newly translated block will be executed now
                    * - the next time we hit them we do nothing
                */

                var anyEnabled = false;
                var alreadyUpdated = false;
                foreach(var enabledWatchpoint in watchpoints.Where(x => x.Enabled))
                {
                    enabledWatchpoint.Enabled = false;
                    if(!alreadyUpdated && parent.UpdateContextOnLoadAndStore)
                    {
                        parent.UpdateContext();
                        alreadyUpdated = true;
                    }

                    // for reading value is always set to 0
                    enabledWatchpoint.Invoke(parent, address, width, value ?? 0);
                    anyEnabled = true;
                }

                if(anyEnabled)
                {
                    // TODO: think if we have to tlib restart at all? if there is no pausing in watchpoint hook than maybe it's not necessary at all?
                    parent.TlibRestartTranslationBlock();
                    // note that on the line above we effectively exit the function so the stuff below is not executed
                }
                else
                {
                    foreach(var disabledWatchpoint in watchpoints)
                    {
                        disabledWatchpoint.Enabled = true;
                    }
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
            public ulong Start;
            public ulong Size;
            public IntPtr HostPointer;
            public int HostBlockStart;
        }

        #region IDisassemblable implementation

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

        public ulong TranslateAddress(ulong logicalAddress)
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
                return isHalted;
            }
            set
            {
                lock(haltedLock)
                {
                    this.Trace();
                    if(value == isHalted)
                    {
                        return;
                    }
                    this.Trace();
                    isHalted = value;
                    if(TimeHandle != null)
                    {
                        this.Trace();
                        // defer disabling to the moment of unlatch, otherwise we could deadlock (e.g., in block begin hook)
                        TimeHandle.DeferredEnabled = !value;
                    }
                    if(isHalted)
                    {
                        TlibSetReturnRequest();
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

        private void UpdateBlockBeginHookPresent()
        {
            TlibSetBlockBeginHookPresent((blockBeginInternalHook != null || blockBeginUserHook != null || ExecutionMode == ExecutionMode.SingleStep || isAnyInactiveHook) ? 1u : 0u);
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private ActionUInt32 TlibSetChainingEnabled;

        [Import]
        private FuncUInt32 TlibGetChainingEnabled;

        [Import]
        private ActionUInt32 TlibSetTbCacheEnabled;

        [Import]
        private FuncUInt32 TlibGetTbCacheEnabled;

        [Import]
        private FuncInt32String TlibInit;

        [Import]
        private Action TlibDispose;

        [Import]
        private Action TlibReset;

        [Import]
        private FuncInt32Int32 TlibExecute;

        [Import]
        protected Action TlibRestartTranslationBlock;

        [Import]
        protected Action TlibSetReturnRequest;

        [Import]
        private ActionInt32IntPtr TlibAtomicMemoryStateInit;

        [Import]
        private FuncUInt32 TlibGetPageSize;

        [Import]
        private ActionUInt64UInt64 TlibMapRange;

        [Import]
        private ActionUInt64UInt64 TlibUnmapRange;

        [Import]
        private FuncUInt32UInt64UInt64 TlibIsRangeMapped;

        [Import]
        private ActionIntPtrIntPtr TlibInvalidateTranslationBlocks;

        [Import]
        protected FuncUInt64UInt64 TlibTranslateToPhysicalAddress;

        [Import]
        private ActionIntPtrInt32 RenodeSetHostBlocks;

        [Import]
        private Action RenodeFreeHostBlocks;

        [Import]
        private ActionInt32Int32 TlibSetIrq;

        [Import]
        private FuncUInt32 TlibIsIrqSet;

        [Import]
        private ActionUInt64 TlibAddBreakpoint;

        [Import]
        private ActionUInt64 TlibRemoveBreakpoint;

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
        private FuncInt32 TlibRestoreContext;

        [Import]
        private FuncIntPtr TlibExportState;

        [Import]
        private FuncInt32 TlibGetStateSize;

        [Import]
        protected FuncUInt64 TlibGetExecutedInstructions;

        [Import]
        private ActionUInt32 TlibSetBlockFinishedHookPresent;

        [Import]
        private ActionUInt32 TlibSetBlockBeginHookPresent;

        [Import]
        private ActionUInt64 TlibFlushPage;

        #pragma warning restore 649

        private readonly HashSet<ulong> pagesAccessedByIo;

        protected const int DefaultTranslationCacheSize = 32 * 1024 * 1024;

        [Export]
        private void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        private void LogDisassembly(ulong pc, uint count, uint flags)
        {
            DisasEngine.LogSymbol(pc, count, flags);
        }

        [Export]
        private int GetCpuIndex()
        {
            return Slot;
        }

        public string DisassembleBlock(uint addr, uint flags = 0)
        {
            var block = DisasEngine.Disassemble(addr, true, 10 * 4, flags);
            return block != null ? block.Replace("\n", "\r\n") : string.Empty;
        }

        [Transient]
        protected DisassemblyEngine DisasEngine;

        protected static readonly Exception InvalidInterruptNumberException = new InvalidOperationException("Invalid interrupt number.");

        private const int DefaultMaximumBlockSize = 0x7FF;

        private void ExecuteHooks(ulong address)
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

        private void DeactivateHooks(ulong address)
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

        public void ActivateNewHooks()
        {
            lock(hooks)
            {
                foreach(var newHook in hooks.Where(x => x.Value.IsNew))
                {
                    newHook.Value.Activate();
                }
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
                UpdateBlockBeginHookPresent();
            }
        }

        private TimeHandle timeHandle;

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
                    timeHandle.Enabled = !isHalted;
                    timeHandle.PauseRequested += RequestPause;
                    timeHandle.StartRequested += StartCPUThread;
                }
            }
        }

        private ulong executedResiduum;

        private bool CpuThreadBodyInner(bool singleStep)
        {
            if(!TimeHandle.RequestTimeInterval(out var interval))
            {
                this.Trace();
                return false;
            }

            this.Trace($"CPU thread body running... granted {interval.Ticks} ticks");
            var instructionsToExecuteThisRound = interval.ToCPUCycles(PerformanceInMips, out ulong ticksResiduum);
            if(singleStep && instructionsToExecuteThisRound >= 1)
            {
                instructionsToExecuteThisRound = 1;
            }
            var instructionsLeftThisRound = instructionsToExecuteThisRound;

            while(!isPaused && !isHalted && instructionsLeftThisRound > 0)
            {
                this.Trace($"CPU thread body in progress; {instructionsLeftThisRound} instructions left...");

                var nearestLimitIn = ((BaseClockSource)machine.ClockSource).NearestLimitIn;
                var instructionsToNearestLimit = nearestLimitIn.ToCPUCycles(PerformanceInMips, out var unused);
                if(instructionsToNearestLimit != ulong.MaxValue && (nearestLimitIn.Ticks == 0 || unused > 0))
                {
                    // we must check for `ulong.MaxValue` as otherwise it would overflow
                    instructionsToNearestLimit++;
                }

                // this puts a limit on instructions to execute in one round
                // and makes timers update independent of the current quantum
                var toExecute = Math.Min(instructionsToNearestLimit, instructionsLeftThisRound);

                ActivateNewHooks();
                this.Trace($"Asking CPU to execute {toExecute} instructions");
                var result = ExecuteInstructions(toExecute, out var executed);
                this.Trace($"CPU executed {executed} instructions and returned {result}");
                instructionsLeftThisRound -= executed;
                ExecutedInstructions += (ulong)executed;
                if(executed > 0)
                {
                    // report how much time elapsed so far
                    var elapsed = TimeInterval.FromCPUCycles(executed + executedResiduum, PerformanceInMips, out executedResiduum);
                    TimeHandle.ReportProgress(elapsed);
                }

                ExecutionFinished(result);

                if(result == ExecutionResult.StoppedAtBreakpoint)
                {
                    this.Trace();
                    ExecuteHooks(PC);
                    // it is necessary to deactivate hooks installed on this PC before
                    // calling `tlib_execute` again to avoid a loop;
                    // we need to do this because creating a breakpoint has caused special
                    // exception-rising, block-breaking `trap` instruction to be
                    // generated by the tcg;
                    // in order to execute code after the breakpoint we must first remove
                    // this `trap` and retranslate the code right after it;
                    // this is achieved by deactivating the breakpoint (i.e., unregistering
                    // from tlib, but keeping it in C#), executing the beginning of the next
                    // block and registering the breakpoint again in the OnBlockBegin hook
                    DeactivateHooks(PC);
                    break;
                }
                else if(result == ExecutionResult.WaitingForInterrupt)
                {
                    this.Trace();
                    // here we test if the nearest scheduled interrupt from timers will happen in this time period:
                    // if so, we simply jump directly to this moment reporting progress;
                    // otherwise we immediately finish the execution of this period
                    nearestLimitIn = ((BaseClockSource)machine.ClockSource).NearestLimitIn;
                    instructionsToNearestLimit = nearestLimitIn.ToCPUCycles(PerformanceInMips, out unused);

                    if(instructionsToNearestLimit >= instructionsLeftThisRound)
                    {
                        this.Trace($"Instructions to nearest limit are: {instructionsToNearestLimit}");
                        instructionsLeftThisRound = 0;
                        break;
                    }
                    instructionsLeftThisRound -= instructionsToNearestLimit;
                    TimeHandle.ReportProgress(nearestLimitIn);
                }
                else if(result == ExecutionResult.Aborted || result == ExecutionResult.ReturnRequested || result == ExecutionResult.StoppedAtWatchpoint)
                {
                    this.Trace(result.ToString());
                    break;
                }
            }

            this.Trace("CPU thread body finished");

            if(isAborted)
            {
                this.Trace("aborted, reporting continue");
                TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
                return false;
            }
            else if(isHalted)
            {
                this.Trace("halted, reporting continue");
                TimeHandle.ReportBackAndContinue(TimeInterval.Empty);
            }
            else if(isPaused || instructionsLeftThisRound > 0)
            {
                this.Trace("reporting break");
                var ticksLeft = instructionsToExecuteThisRound > 0 ? (instructionsLeftThisRound * (interval.Ticks - ticksResiduum)) / instructionsToExecuteThisRound : 0;
                TimeHandle.ReportBackAndBreak(TimeInterval.FromTicks(ticksLeft + ticksResiduum));
            }
            else
            {
                this.Trace("finished, reporting continue");
                TimeHandle.ReportBackAndContinue(TimeInterval.FromTicks(ticksResiduum));
            }

            return instructionsLeftThisRound != instructionsToExecuteThisRound;
        }

        protected virtual void ExecutionFinished(ExecutionResult result)
        {
            // the default implementation intentionally does nothing
        }

        private void CpuThreadBody()
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
                                singleStep = (executionMode == ExecutionMode.SingleStep);
                                if(singleStep)
                                {
                                    // we become incactive as we wait for step command
                                    using(this.ObtainSinkInactiveState())
                                    {
                                        this.Log(LogLevel.Noisy, "Waiting for a step instruction (PC=0x{0:X8}).", PC.RawValue);
                                        InvokeHalted(new HaltArguments(HaltReason.Step, Id));
                                        if(!singleStepSynchronizer.WaitForStepCommand())
                                        {
                                            this.Trace();
                                            continue;
                                        }
                                        this.Trace();
                                    }
                                }
                            }

                            var anythingExecuted = CpuThreadBodyInner(singleStep);

                            if(singleStep && anythingExecuted)
                            {
                                this.Trace();
                                singleStepSynchronizer.StepFinished();
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

        private void StartCPUThread()
        {
            this.Trace();
            lock(pauseLock)
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

        private void TlibSetIrqWrapped(int number, bool state)
        {
            var decodedInterrupt = DecodeInterrupt(number);
            if(!decodedIrqs.TryGetValue(decodedInterrupt, out var irqs))
            {
                irqs = new HashSet<int>();
                decodedIrqs.Add(decodedInterrupt, irqs);
            }
            this.Log(LogLevel.Noisy, "Setting CPU IRQ #{0} to {1}", number, state);
            if(state)
            {
                irqs.Add(number);
                TlibSetIrq((int)decodedInterrupt, 1);
            }
            else
            {
                irqs.Remove(number);
                if(irqs.Count == 0)
                {
                    TlibSetIrq((int)decodedInterrupt, 0);
                }
            }
        }

        protected enum ExecutionResult : ulong
        {
            Ok = 0x10000,
            WaitingForInterrupt = 0x10001,
            StoppedAtBreakpoint = 0x10002,
            StoppedAtWatchpoint = 0x10004,
            ReturnRequested = 0x10005,
            // tlib returns int32, so this value won't overlap with an actual result
            Aborted = ulong.MaxValue
        }

        private ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            try
            {
                while(actionsToExecuteOnCpuThread.TryDequeue(out var queuedAction))
                {
                    queuedAction();
                }

                pauseGuard.Enter();
                lastTlibResult = (ExecutionResult)TlibExecute(checked((int)numberOfInstructionsToExecute));
                pauseGuard.Leave();
            }
            catch(CpuAbortException)
            {
                this.NoisyLog("CPU abort detected, halting.");
                isAborted = true;
                InvokeHalted(new HaltArguments(HaltReason.Abort, Id));
                return ExecutionResult.Aborted;
            }
            finally
            {
                numberOfExecutedInstructions = TlibGetExecutedInstructions();
                if(numberOfExecutedInstructions == 0)
                {
                    this.Trace($"Asked tlib to execute {numberOfInstructionsToExecute}, but did nothing");
                }
                DebugHelper.Assert(numberOfExecutedInstructions <= numberOfInstructionsToExecute, "tlib executed more instructions than it was asked to");
            }

            return lastTlibResult;
        }

        private bool isAnyInactiveHook;
        private Dictionary<ulong, HookDescriptor> hooks;
        private Dictionary<Interrupt, HashSet<int>> decodedIrqs;
        private readonly CpuBitness bitness;
        private bool dispatcherRestartRequested;
        private readonly object cpuThreadBodyLock = new object();

        private class HookDescriptor
        {
            public HookDescriptor(TranslationCPU cpu, ulong address)
            {
                this.cpu = cpu;
                this.address = address;
                callbacks = new HashSet<Action<ICpuSupportingGdb, ulong>>();
                IsNew = true;
            }

            public void ExecuteCallbacks()
            {
                foreach(var callback in callbacks)
                {
                    callback(cpu, address);
                }
            }

            public void AddCallback(Action<ICpuSupportingGdb, ulong> action)
            {
                callbacks.Add(action);
            }

            public bool RemoveCallback(Action<ICpuSupportingGdb, ulong> action)
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
                IsNew = false;
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
            public bool IsNew { get; private set; }

            private readonly ulong address;
            private readonly TranslationCPU cpu;
            private readonly HashSet<Action<ICpuSupportingGdb, ulong>> callbacks;
        }

        private class Synchronizer
        {
            public Synchronizer()
            {
                guard = new object();
            }

            public bool Enabled
            {
                get
                {
                    return enabled;
                }

                set
                {
                    lock(guard)
                    {
                        enabled = value;
                        if(!enabled)
                        {
                            Monitor.PulseAll(guard);
                        }
                    }
                }
            }

            public void StepFinished()
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
                }
            }

            public void CommandStep(int steps = 1)
            {
                lock(guard)
                {
                    counter = steps;
                    Monitor.Pulse(guard);
                }
            }

            public bool WaitForStepCommand()
            {
                lock(guard)
                {
                    while(enabled && counter == 0)
                    {
                        Monitor.Wait(guard);
                    }

                    return enabled;
                }
            }
            public void WaitForStepFinished()
            {
                lock(guard)
                {
                    while(counter > 0)
                    {
                        Monitor.Wait(guard);
                    }
                }
            }

            public object Guard
            {
                get
                {
                    return guard;
                }
            }

            private bool enabled;
            private int counter;
            private readonly object guard;
        }
    }
}

