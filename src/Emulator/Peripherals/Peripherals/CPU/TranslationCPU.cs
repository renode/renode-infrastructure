//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using Antmicro.Renode.Hooks;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using ELFSharp.ELF;
using Machine = Antmicro.Renode.Core.Machine;
using Antmicro.Renode.Disassembler.LLVM;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class TranslationCPUHooksExtensions
    {
        public static void SetHookAtBlockBegin(this TranslationCPU cpu, [AutoParameter]IMachine m, string pythonScript)
        {
            var engine = new BlockPythonEngine(m, cpu, pythonScript);
            cpu.SetHookAtBlockBegin(engine.HookWithSize);
        }

        public static void SetHookAtBlockEnd(this TranslationCPU cpu, [AutoParameter]IMachine m, string pythonScript)
        {
            var engine = new BlockPythonEngine(m, cpu, pythonScript);
            cpu.SetHookAtBlockEnd(engine.HookWithSize);
        }
    }

    public abstract partial class TranslationCPU : BaseCPU, IGPIOReceiver, ICpuSupportingGdb, ICPUWithExternalMmu, ICPUWithMMU, INativeUnwindable, IDisassemblable, ICPUWithMetrics, ICPUWithMappedMemory, ICPUWithRegisters, ICPUWithMemoryAccessHooks
    {
        protected TranslationCPU(string cpuType, IMachine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
        : this(0, cpuType, machine, endianness, bitness)
        {
        }

        protected TranslationCPU(uint id, string cpuType, IMachine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
            : base(id, cpuType, machine, endianness, bitness)
        {
            this.translationCacheSize = DefaultTranslationCacheSize;
            translationCacheSync = new object();
            pauseGuard = new CpuThreadPauseGuard(this);
            decodedIrqs = new Dictionary<Interrupt, HashSet<int>>();
            hooks = new Dictionary<ulong, HookDescriptor>();
            currentMappings = new List<SegmentMapping>();
            InitializeRegisters();
            Init();
            InitDisas();
            externalMmuWindowsCount = TlibGetMmuWindowsCount();
        }

        public abstract string GDBArchitecture { get; }

        public abstract List<GDBFeatureDescriptor> GDBFeatures { get; }

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

        /// <summary>
        /// The value is used to convert instructions count to cycles, e.g.:
        /// * for RISC-V CYCLE and MCYCLE CSRs
        /// * in ARM Performance Monitoring Unit
        /// </summary>
        public decimal CyclesPerInstruction
        {
            get
            {
                return checked(TlibGetMillicyclesPerInstruction() / 1000m);
            }
            set
            {
                if(value <= 0)
                {
                    throw new RecoverableException("Value must be a positive number.");
                }
                var millicycles = value * 1000m;
                if(millicycles % 1m != 0)
                {
                    throw new RecoverableException("Value's precision can't be greater than 0.001");
                }
                TlibSetMillicyclesPerInstruction(checked((uint)millicycles));
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
       
        // This value should only be read in CPU hooks (during execution of translated code).
        public uint CurrentBlockDisassemblyFlags => TlibGetCurrentTbDisasFlags();

        public uint ExternalMmuWindowsCount => externalMmuWindowsCount;

        public bool ThreadSentinelEnabled { get; set; }

        private bool logTranslationBlockFetchEnabled;

        public override ulong ExecutedInstructions { get {return TlibGetTotalExecutedInstructions(); } }

        public int Slot { get{if(!slot.HasValue) slot = machine.SystemBus.GetCPUId(this); return slot.Value;} private set {slot = value;} }
        private int? slot;

        public override string ToString()
        {
            return $"[CPU: {this.GetCPUThreadName(machine)}]";
        }

        public void RequestReturn()
        {
            TlibSetReturnRequest();
        }

        public void FlushTlbPage(UInt64 address)
        {
            TlibFlushPage(address);
        }

        public void ClearTranslationCache()
        {
            using(machine?.ObtainPausedState(true))
            {
                TlibInvalidateTranslationCache();
            }
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
                using(machine?.ObtainPausedState(true))
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
            if(memoryAccessHook != null)
            {
                // Repeat memory hook enable to make sure that the tcg context is set not to use the tlb
                TlibOnMemoryAccessEventEnabled(1);
            }
        }

        public override ExecutionMode ExecutionMode
        {
            get
            {
                return base.ExecutionMode;
            }

            set
            {
                base.ExecutionMode = value;
                UpdateBlockBeginHookPresent();
            }
        }

        public override void SyncTime()
        {
            if(!OnPossessedThread)
            {
                this.Log(LogLevel.Error, "Syncing time should be done from CPU thread only. Ignoring the operation");
                return;
            }

            var numberOfExecutedInstructions = TlibGetExecutedInstructions();
            this.Trace($"CPU executed {numberOfExecutedInstructions} instructions and time synced");
            ReportProgress(numberOfExecutedInstructions);
        }

        public string LogFile
        {
            get { return logFile; }
            set
            {
                logFile = value;
                LogTranslatedBlocks = (value != null);

                try
                {
                    // truncate the file
                    File.WriteAllText(logFile, string.Empty);
                }
                catch(Exception e)
                {
                    throw new RecoverableException($"There was a problem when preparing the log file {logFile}: {e.Message}");
                }
            }
        }

        protected override void OnLeavingResetState()
        {
            base.OnLeavingResetState();
            TlibOnLeavingResetState();
        }

        protected override void RequestPause()
        {
            base.RequestPause();
            TlibSetReturnRequest();
        }

        protected override void InnerPause(bool onCpuThread, bool checkPauseGuard)
        {
            base.InnerPause(onCpuThread, checkPauseGuard);

            if(!onCpuThread && checkPauseGuard)
            {
                pauseGuard.OrderPause();
            }
        }

        public override void Reset()
        {
            base.Reset();
            isInterruptLoggingEnabled = false;
            HandleRamSetup();
            TlibReset();
            ResetOpcodesCounters();
            profiler?.Dispose();
        }

        public bool RequestTranslationBlockRestart(bool quiet = false)
        {
            if(!OnPossessedThread)
            {
                if(!quiet)
                {
                    this.Log(LogLevel.Error, "Translation block restart should be requested from CPU thread only. Ignoring the operation.");
                }
                return false;
            }
            return pauseGuard.RequestTranslationBlockRestart(quiet);
        }

        public void RaiseException(uint exceptionId)
        {
            TlibRaiseException(exceptionId);
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
                if(started && (lastTlibResult == TlibExecutionResult.WaitingForInterrupt || !(DisableInterruptsWhileStepping && IsSingleStepMode)))
                {
                    TlibSetIrqWrapped(number, value);
                    if(EmulationManager.Instance.CurrentEmulation.Mode != Emulation.EmulationMode.SynchronizedIO)
                    {
                        sleeper.Interrupt();
                    }
                }
            }
        }

        public override RegisterValue PC
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

        public void MapMemory(IMappedSegment segment)
        {
            if(segment.StartingOffset > bitness.GetMaxAddress() || segment.Size > bitness.GetMaxAddress())
            {
                throw new RecoverableException("Could not map memory segment: starting offset or size are too high");
            }

            using(machine?.ObtainPausedState(true))
            {
                currentMappings.Add(new SegmentMapping(segment));
                RegisterMemoryChecked(segment.StartingOffset, segment.Size);
                checked
                {
                    TranslationCacheSize += segment.Size / 4;
                }
            }
        }

        public void UnmapMemory(Range range)
        {
            using(machine?.ObtainPausedState(true))
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
                checked
                {
                    TranslationCacheSize -= range.Size / 4;
                }
                RebuildMemoryMappings();
            }
        }

        public void SetPageAccessViaIo(ulong address)
        {
            TlibSetPageIoAccessed(address);
        }

        public void ClearPageAccessViaIo(ulong address)
        {
            TlibClearPageIoAccessed(address);
        }

        public bool DisableInterruptsWhileStepping { get; set; }

        // this is just for easier usage in Monitor
        public void LogFunctionNames(bool value, bool removeDuplicates = false)
        {
            LogFunctionNames(value, string.Empty, removeDuplicates);
        }

        public ulong GetCurrentInstructionsCount()
        {
            return TlibGetTotalExecutedInstructions();
        }

        public void LogFunctionNames(bool value, string spaceSeparatedPrefixes = "", bool removeDuplicates = false)
        {
            if(!value)
            {
                SetInternalHookAtBlockBegin(null);
                return;
            }

            var prefixesAsArray = spaceSeparatedPrefixes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // using string builder here is due to performance reasons: test shows that string.Format is much slower
            var messageBuilder = new StringBuilder(256);

            Symbol previousSymbol = null;

            SetInternalHookAtBlockBegin((pc, size) =>
            {
                if(Bus.TryFindSymbolAt(pc, out var name, out var symbol))
                {
                    if(removeDuplicates && symbol == previousSymbol)
                    {
                        return;
                    }
                    previousSymbol = symbol;
                }

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
            using(machine?.ObtainPausedState(true))
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
            using(machine?.ObtainPausedState(true))
            {
                if((hook == null) ^ (blockFinishedHook == null))
                {
                    ClearTranslationCache();
                    TlibSetBlockFinishedHookPresent(hook != null ? 1u : 0u);
                }
                blockFinishedHook = hook;
            }
        }

        public void SetHookAtMemoryAccess(Action<ulong, MemoryOperation, ulong> hook)
        {
            TlibOnMemoryAccessEventEnabled(hook != null ? 1 : 0);
            memoryAccessHook = hook;
        }

        public void AddHookAtInterruptBegin(Action<ulong> hook)
        {
            if(interruptBeginHook == null)
            {
                TlibSetInterruptBeginHookPresent(1u);
            }
            interruptBeginHook += hook;
        }

        public void AddHookOnMmuFault(Action<ulong, AccessType, int> hook)
        {
            mmuFaultHook += hook;
        }

        public void AddHookAtInterruptEnd(Action<ulong> hook)
        {
            if(!Architecture.Contains("riscv") && !Architecture.Contains("arm"))
            {
                throw new RecoverableException("Hooks at the end of interrupt are supported only in the RISC-V and ARM architectures");
            }

            if(interruptEndHook == null)
            {
                TlibSetInterruptEndHookPresent(1u);
            }
            interruptEndHook += hook;
        }

        public void LogCpuInterrupts(bool isEnabled)
        {
            if(isEnabled)
            {
                if(!isInterruptLoggingEnabled)
                {
                    AddHookAtInterruptBegin(LogCpuInterruptBegin);
                    AddHookAtInterruptEnd(LogCpuInterruptEnd);
                    isInterruptLoggingEnabled = true;
                }
            }
            else
            {
                RemoveHookAtInterruptBegin(LogCpuInterruptBegin);
                RemoveHookAtInterruptEnd(LogCpuInterruptEnd);
                isInterruptLoggingEnabled = false;
            }
        }

        public void AddHookAtWfiStateChange(Action<bool> hook)
        {
            if(wfiStateChangeHook == null)
            {
                TlibSetCpuWfiStateChangeHookPresent(1);
            }
            wfiStateChangeHook += hook;
        }

        public void RemoveHookAtWfiStateChange(Action<bool> hook)
        {
            wfiStateChangeHook -= hook;
            if(wfiStateChangeHook == null)
            {
                TlibSetCpuWfiStateChangeHookPresent(0);
            }
        }

        [Export]
        protected virtual ulong ReadByteFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForReading(offset, SysbusAccessWidth.Byte))
            {
                var res = guard.InterruptTransaction
                    ? 0
                    : (ulong)machine.SystemBus.ReadByte(offset, this);
                return res;
            }
        }

        [Export]
        protected virtual ulong ReadWordFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForReading(offset, SysbusAccessWidth.Word))
            {
                return guard.InterruptTransaction
                    ? 0
                    : (ulong)machine.SystemBus.ReadWord(offset, this);
            }
        }

        [Export]
        protected virtual ulong ReadDoubleWordFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForReading(offset, SysbusAccessWidth.DoubleWord))
            {
                return guard.InterruptTransaction
                    ? 0
                    : machine.SystemBus.ReadDoubleWord(offset, this);
            }
        }

        [Export]
        protected virtual ulong ReadQuadWordFromBus(ulong offset)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForReading(offset, SysbusAccessWidth.QuadWord))
            {
                return guard.InterruptTransaction
                    ? 0
                    : machine.SystemBus.ReadQuadWord(offset, this);
            }
        }

        [Export]
        protected virtual void WriteByteToBus(ulong offset, ulong value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForWriting(offset, SysbusAccessWidth.Byte, value))
            {
                if(!guard.InterruptTransaction)
                {
                    machine.SystemBus.WriteByte(offset, unchecked((byte)value), this);
                }
            }
        }

        [Export]
        protected virtual void WriteWordToBus(ulong offset, ulong value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForWriting(offset, SysbusAccessWidth.Word, value))
            {
                if(!guard.InterruptTransaction)
                {
                    machine.SystemBus.WriteWord(offset, unchecked((ushort)value), this);
                }
            }
        }

        [Export]
        protected virtual void WriteDoubleWordToBus(ulong offset, ulong value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForWriting(offset, SysbusAccessWidth.DoubleWord, value))
            {
                if(!guard.InterruptTransaction)
                {
                    machine.SystemBus.WriteDoubleWord(offset, (uint)value, this);
                }
            }
        }

        [Export]
        protected void WriteQuadWordToBus(ulong offset, ulong value)
        {
            if(UpdateContextOnLoadAndStore)
            {
                TlibRestoreContext();
            }
            using(var guard = ObtainPauseGuardForWriting(offset, SysbusAccessWidth.QuadWord, value))
            {
                if(!guard.InterruptTransaction)
                {
                    machine.SystemBus.WriteQuadWord(offset, value, this);
                }
            }
        }

        protected virtual string GetExceptionDescription(ulong exceptionIndex)
        {
            return $"Undecoded {exceptionIndex}";
        }

        public abstract void SetRegisterUnsafe(int register, RegisterValue value);

        public abstract RegisterValue GetRegisterUnsafe(int register);

        public abstract IEnumerable<CPURegister> GetRegisters();

        private void LogCpuInterruptBegin(ulong exceptionIndex)
        {
            this.Log(LogLevel.Info, "Begin of the interrupt: {0}", GetExceptionDescription(exceptionIndex));
        }

        private void LogCpuInterruptEnd(ulong exceptionIndex)
        {
            this.Log(LogLevel.Info, "End of the interrupt: {0}", GetExceptionDescription(exceptionIndex));
        }

        private void SetInternalHookAtBlockBegin(Action<ulong, uint> hook)
        {
            using(machine?.ObtainPausedState(true))
            {
                if((hook == null) ^ (blockBeginInternalHook == null))
                {
                    ClearTranslationCache();
                }
                blockBeginInternalHook = hook;
                UpdateBlockBeginHookPresent();
            }
        }

        private bool AssertMmuEnabled()
        {
            if(!externalMmuEnabled)
            {
                throw new RecoverableException("External MMU not enabled");
            }
            return externalMmuEnabled;
        }

        private bool AssertMmuEnabledAndWindowInRange(uint index)
        {
            var windowInRange = index < externalMmuWindowsCount;
            if(!windowInRange)
            {
                throw new RecoverableException($"Window index to high, maximum number: {externalMmuWindowsCount - 1}, got {index}");
            }
            return AssertMmuEnabled() && windowInRange;
        }

        /* Currently, due to the used types, 64 bit targets will always pass this check.
        Also on such platforms unary overflow is not possible */
        private bool AssertMmuWindowAddressInRange(ulong address, bool inclusiveRange = false)
        {
            ulong maxValue;
            switch(this.bitness)
            {
                case CpuBitness.Bits32:
                    maxValue = UInt32.MaxValue;
                    break;
                case CpuBitness.Bits64:
                    maxValue = UInt64.MaxValue;
                    break;
                default:
                    throw new ArgumentException("Unexpected value of the CpuBitness");
            }

            if(inclusiveRange && address != 0)
            {
                address -= 1;
            }

            if(address > maxValue)
            {
                throw new RecoverableException($"Address is outside of the possible range. Maximum value: {maxValue}");
            }

            return true;
        }

        public void EnableExternalWindowMmu(bool value)
        {
            TlibEnableExternalWindowMmu(value ? 1u : 0u);
            externalMmuEnabled = value;
        }

        public int AcquireExternalMmuWindow(uint type)
        {
            return AssertMmuEnabled() ? TlibAcquireMmuWindow(type) : -1;
        }

        public void ResetMmuWindow(uint index)
        {
            if(AssertMmuEnabledAndWindowInRange(index))
            {
                TlibResetMmuWindow(index);
            }
        }

        public void SetMmuWindowAddend(uint index, ulong addend)
        {
            if(AssertMmuEnabledAndWindowInRange(index))
            {
                TlibSetMmuWindowAddend(index, addend);
            }
        }

        public void SetMmuWindowStart(uint index, ulong startAddress)
        {
            if(AssertMmuEnabledAndWindowInRange(index) && AssertMmuWindowAddressInRange(startAddress))
            {
                TlibSetMmuWindowStart(index, startAddress);
            }
        }

        public void SetMmuWindowEnd(uint index, ulong endAddress)
        {
            if(AssertMmuEnabledAndWindowInRange(index) && AssertMmuWindowAddressInRange(endAddress, inclusiveRange: true))
            {
                bool useInclusiveEndRange= false;
                // Overflow on 64bits currently not possible due to type constraints
                if(this.bitness == CpuBitness.Bits32)
                {
                    useInclusiveEndRange = ((endAddress - 1) == UInt32.MaxValue); 
                }

                if(useInclusiveEndRange)
                {
                    endAddress -= 1;
                }

                this.DebugLog("Setting range end to {0} addr 0x{1:x}", useInclusiveEndRange ? "inclusive" : "exclusive", endAddress);
                TlibSetMmuWindowEnd(index, endAddress, useInclusiveEndRange? 1u : 0u);
            }
        }

        public void SetMmuWindowPrivileges(uint index, uint permissions)
        {
            if(AssertMmuEnabledAndWindowInRange(index))
            {
                TlibSetWindowPrivileges(index, permissions);
            }
        }

        public ulong GetMmuWindowAddend(uint index)
        {
            return AssertMmuEnabledAndWindowInRange(index) ? TlibGetMmuWindowAddend(index) : 0;
        }

        public ulong GetMmuWindowStart(uint index)
        {
            return AssertMmuEnabledAndWindowInRange(index) ? TlibGetMmuWindowStart(index) : 0;
        }

        public ulong GetMmuWindowEnd(uint index)
        {
            return AssertMmuEnabledAndWindowInRange(index) ? TlibGetMmuWindowEnd(index) : 0;
        }

        public uint GetMmuWindowPrivileges(uint index)
        {
            return AssertMmuEnabledAndWindowInRange(index) ? TlibGetWindowPrivileges(index) : 0;
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

        private void InvokeInCpuThreadSafely(Action a)
        {
            actionsToExecuteOnCpuThread.Enqueue(a);
        }

        private void RemoveHookAtInterruptBegin(Action<ulong> hook)
        {
            interruptBeginHook -= hook;
            if(interruptBeginHook == null)
            {
                TlibSetInterruptBeginHookPresent(0u);
            }
        }

        private void RemoveHookAtInterruptEnd(Action<ulong> hook)
        {
            interruptEndHook -= hook;
            if(interruptEndHook == null)
            {
                TlibSetInterruptEndHookPresent(0u);
            }
        }

        private ConcurrentQueue<Action> actionsToExecuteOnCpuThread = new ConcurrentQueue<Action>();
        private TlibExecutionResult lastTlibResult;

        // TODO
        private object lck = new object();

        protected virtual bool IsSecondary
        {
            get
            {
                return Slot > 0;
            }
        }

        /// <remarks>
        /// This method should be called from tlib only, and never from C#, since it uses `ObtainPauseGuardForHook`
        /// see: <see cref="ObtainPauseGuardForHook" /> for more information.
        /// </remarks>
        [Export]
        private void OnWfiStateChange(int isInWfi)
        {
            using(ObtainPauseGuardForHook())
            {
                wfiStateChangeHook?.Invoke(isInWfi > 0);
            }
        }

        /// <remarks>
        /// This method should be called from tlib only, and never from C#, since it uses `ObtainPauseGuardForHook`
        /// see: <see cref="ObtainPauseGuardForHook" /> for more information.
        /// </remarks>
        [Export]
        private uint OnBlockBegin(ulong address, uint size)
        {
            ReactivateHooks();

            using(ObtainPauseGuardForHook())
            {
                blockBeginInternalHook?.Invoke(address, size);
                blockBeginUserHook?.Invoke(address, size);
            }

            return (currentHaltedState || isPaused) ? 0 : 1u;
        }

        /// <remarks>
        /// This method should be called from tlib only, and never from C#, since it uses `ObtainPauseGuardForHook`
        /// see: <see cref="ObtainPauseGuardForHook" /> for more information.
        /// </remarks>
        [Export]
        private void OnBlockFinished(ulong pc, uint executedInstructions)
        {
            using(ObtainPauseGuardForHook())
            {
                blockFinishedHook?.Invoke(pc, executedInstructions);
            }
        }

        [Export]
        private void OnInterruptBegin(ulong interruptIndex)
        {
            interruptBeginHook?.Invoke(interruptIndex);
        }

        [Export]
        private void MmuFaultExternalHandler(ulong address, int accessType, int windowIndex)
        {
            this.Log(LogLevel.Noisy, "External MMU fault at 0x{0:X} when trying to access as {1}", address, (AccessType)accessType);

            if(windowIndex == -1)
            {
                this.Log(LogLevel.Error, "MMU fault - the address 0x{0:X} is not specified in any of the existing ranges", address);
            }
            mmuFaultHook?.Invoke(address, (AccessType)accessType, windowIndex);
        }

        [Export]
        private void OnInterruptEnd(ulong interruptIndex)
        {
            interruptEndHook?.Invoke(interruptIndex);
        }

        [Export]
        private void OnMemoryAccess(ulong pc, uint operation, ulong address)
        {
            memoryAccessHook?.Invoke(pc, (MemoryOperation)operation, address);
        }

        [Export]
        private void OnMassBroadcastDirty(IntPtr arrayStart, int size)
        {
            var tempArray = new long[size];
            Marshal.Copy(arrayStart, tempArray, 0, size);
            machine.AppendDirtyAddresses(Id, tempArray);
        }

        [Transient]
        private IntPtr dirtyAddressesPtr = IntPtr.Zero;

        [Export]
        private IntPtr GetDirty(IntPtr size)
        {
            var dirtyAddressesList = machine.GetNewDirtyAddressesForCore(Id); 
            var newAddressesCount = dirtyAddressesList.Length;

            if(newAddressesCount > 0)
            {
                dirtyAddressesPtr = memoryManager.Reallocate(dirtyAddressesPtr, new IntPtr(newAddressesCount * 8));
                Marshal.Copy(dirtyAddressesList, 0, dirtyAddressesPtr, newAddressesCount);
            }      
            Marshal.WriteInt64(size, newAddressesCount);

            return dirtyAddressesPtr;
        }

        protected virtual void InitializeRegisters()
        {
        }

        private void OnTranslationBlockFetch(ulong offset)
        {
            var info = Bus.FindSymbolAt(offset);
            if(info != string.Empty)
            {
                info = " - " + info;
            }
            this.Log(LogLevel.Info, "Fetching block @ 0x{0:X8}{1}", offset, info);
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
                if(!hooks.Any(x => !x.Value.IsActive))
                {
                    isAnyInactiveHook = false;
                }
                UpdateBlockBeginHookPresent();
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
                if(!hooks.Any(x => !x.Value.IsActive))
                {
                    isAnyInactiveHook = false;
                }
                UpdateBlockBeginHookPresent();
            }
        }

        public void EnterSingleStepModeSafely(HaltArguments args, bool? blocking = null)
        {
            // this method should only be called from CPU thread,
            // but we should check it anyway
            CheckCpuThreadId();
            ChangeExecutionModeToSingleStep(blocking);

            UpdateHaltedState();
            InvokeHalted(args);
        }

        public override void Dispose()
        {
            base.Dispose();
            profiler?.Dispose();
        }

        protected override void DisposeInner(bool silent = false)
        {
            base.DisposeInner(silent);
            TimeHandle.Dispose();
            RemoveAllHooks();
            TlibDispose();
            RenodeFreeHostBlocks();
            binder.Dispose();
            if(!EmulationManager.DisableEmulationFilesCleanup)
            {
                File.Delete(libraryFile);
            }
            if(dirtyAddressesPtr != IntPtr.Zero)
            {
                memoryManager.Free(dirtyAddressesPtr);
            }
            memoryManager.CheckIfAllIsFreed();
        }

        [Export]
        private void ReportAbort(string message)
        {
            this.Log(LogLevel.Error, "CPU abort [PC=0x{0:X}]: {1}.", PC.RawValue, message);
            throw new CpuAbortException(message);
        }

        /*
            Increments each time a new translation library resource is created.
            This counter marks each new instance of a translation library with a new number, which is used in file names to avoid collisions.
            It has to survive emulation reset, so the file names remain unique.
        */
        private static int CpuCounter = 0;
        
        protected override bool UpdateHaltedState(bool ignoreExecutionMode = false)
        {
            if(!base.UpdateHaltedState(ignoreExecutionMode))
            {
                return false;
            }

            if(currentHaltedState)
            {
                TlibSetReturnRequest();
            }

            return true;
        }

        private void Init()
        {
            memoryManager = new SimpleMemoryManager(this);
            isPaused = true;

            onTranslationBlockFetch = OnTranslationBlockFetch;

            // PowerPC always uses the big-endian translation library
            var endianSuffix = (Endianness == Endianess.BigEndian || Architecture.StartsWith("ppc")) ? "be" : "le";
            var libraryResource = string.Format("Antmicro.Renode.translate-{0}-{1}.so", Architecture, endianSuffix);
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if(assembly.TryFromResourceToTemporaryFile(libraryResource, out libraryFile, $"{CpuCounter}-{libraryResource}"))
                {
                    break;
                }
            }

            Interlocked.Increment(ref CpuCounter);

            if(libraryFile == null)
            {
                throw new ConstructionException($"Cannot find library {libraryResource}");
            }

            binder = new NativeBinder(this, libraryFile);
            TlibSetTranslationCacheSize(checked((IntPtr)translationCacheSize));
            MaximumBlockSize = DefaultMaximumBlockSize;
            var result = TlibInit(Model);
            if(result == -1)
            {
                throw new ConstructionException("Unknown CPU type");
            }
            if(cpuState != null)
            {
                var statePtr = TlibExportState();
                Marshal.Copy(cpuState, 0, statePtr, cpuState.Length);
                AfterLoad(statePtr);
            }
            if(machine != null)
            {
                TlibAtomicMemoryStateInit(checked((int)this.Id), machine.AtomicMemoryStatePointer);
            }
            HandleRamSetup();
            foreach(var hook in hooks)
            {
                TlibAddBreakpoint(hook.Key);
            }
            CyclesPerInstruction = 1;
        }

        protected override ulong SkipInstructions
        {
            get => base.SkipInstructions;
            set
            {
                if(!OnPossessedThread)
                {
                    this.Log(LogLevel.Error, "Changing SkipInstructions should be only done on CPU thread, ignoring");
                    return;
                }

                base.SkipInstructions = value;
                // This will be imprecise when we change SkipInstructions before end of the translation block
                // as TlibSetReturnRequest doesn't finish current translation block
                TlibSetReturnRequest();
            }
        }

        [Transient]
        private ActionUInt64 onTranslationBlockFetch;
        private byte[] cpuState;
        
        [Transient]
        private string libraryFile;

        private ulong translationCacheSize;
        private readonly object translationCacheSync;

        [Transient]
        // the reference here is necessary for the timer to not be garbage collected
        #pragma warning disable 0414
        private Timer currentTimer;
        #pragma warning restore 0414

        [Transient]
        private SimpleMemoryManager memoryManager;

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
                if(hostBlocks.Length > 0)
                {
                    var blockBuffer = memoryManager.Allocate(new IntPtr(Marshal.SizeOf(typeof(HostMemoryBlock)) * hostBlocks.Length));
                    BlitArray(blockBuffer, hostBlocks.OrderBy(x => x.HostPointer.ToInt64()).Cast<dynamic>().ToArray());
                    RenodeSetHostBlocks(blockBuffer, hostBlocks.Length);
                    memoryManager.Free(blockBuffer);
                    this.NoisyLog("Memory mappings rebuilt, there are {0} host blocks now.", hostBlocks.Length);
                }
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

        private CpuThreadPauseGuard ObtainPauseGuardForWriting(ulong address, SysbusAccessWidth width, ulong value)
        {
            pauseGuard.InitializeForWriting(address, width, value);
            return pauseGuard;
        }

        /// <remarks>
        /// Be careful when using this method - the PauseGuard is used to verify if the precise pause is possible in a given context
        /// and as such, we should only obtain the guard when we for certain know it is.
        /// For example, precise pause is always possible if called from CPU loop in tlib
        /// </remarks>
        private CpuThreadPauseGuard ObtainPauseGuardForHook()
        {
            pauseGuard.InitializeForHook();
            return pauseGuard;
        }

        #region Memory trampolines

        [Export]
        private IntPtr Allocate(IntPtr size)
        {
            return memoryManager.Allocate(size);
        }

        [Export]
        private IntPtr Reallocate(IntPtr oldPointer, IntPtr newSize)
        {
            return memoryManager.Reallocate(oldPointer, newSize);
        }

        [Export]
        protected void Free(IntPtr pointer)
        {
            memoryManager.Free(pointer);
        }

        #endregion

        private Action<ulong, uint> blockBeginInternalHook;
        private Action<ulong, uint> blockBeginUserHook;
        private Action<ulong, uint> blockFinishedHook;
        private Action<ulong> interruptBeginHook;
        private Action<ulong> interruptEndHook;
        private Action<ulong, AccessType, int> mmuFaultHook;
        private Action<ulong, MemoryOperation, ulong> memoryAccessHook;
        private Action<bool> wfiStateChangeHook;

        private List<SegmentMapping> currentMappings;

        private readonly CpuThreadPauseGuard pauseGuard;

        [Transient]
        private NativeBinder binder;

        private class SimpleMemoryManager
        {
            public SimpleMemoryManager(TranslationCPU parent)
            {
                this.parent = parent;
                ourPointers = new ConcurrentDictionary<IntPtr, long>();
            }

            public IntPtr Allocate(IntPtr size)
            {
                var ptr = Marshal.AllocHGlobal(size);
                var sizeNormalized = Misc.NormalizeBinary((double)size);
                if(!ourPointers.TryAdd(ptr, (long)size))
                {
                    throw new InvalidOperationException($"Trying to allocate a {sizeNormalized}B pointer that already exists is the memory database.");
                }
                Interlocked.Add(ref allocated, (long)size);
                parent.NoisyLog("Allocated {0}B pointer at 0x{1:X}.", sizeNormalized, ptr);
                PrintAllocated();
                return ptr;
            }

            public IntPtr Reallocate(IntPtr oldPointer, IntPtr newSize)
            {
                if(oldPointer == IntPtr.Zero)
                {
                    return Allocate(newSize);
                }
                if(newSize == IntPtr.Zero)
                {
                    Free(oldPointer);
                    return IntPtr.Zero;
                }
                if(!ourPointers.TryRemove(oldPointer, out var oldSize))
                {
                    throw new InvalidOperationException($"Trying to reallocate a pointer at 0x{oldPointer:X} which wasn't allocated by this memory manager.");
                }
                var ptr = Marshal.ReAllocHGlobal(oldPointer, newSize);
                parent.NoisyLog("Reallocated a pointer: old size {0}B at 0x{1:X}, new size {2}B at 0x{3:X}.", Misc.NormalizeBinary(oldSize), oldPointer, Misc.NormalizeBinary((double)newSize), ptr);
                Interlocked.Add(ref allocated, (long)newSize - oldSize);
                ourPointers.TryAdd(ptr, (long)newSize);
                return ptr;
            }

            public void Free(IntPtr ptr)
            {
                if(!ourPointers.TryRemove(ptr, out var oldSize))
                {
                    throw new InvalidOperationException($"Trying to free a pointer at 0x{ptr:X} which wasn't allocated by this memory manager.");
                }
                parent.NoisyLog("Deallocated a {0}B pointer at 0x{1:X}.", Misc.NormalizeBinary(oldSize), ptr);
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
                    parent.Log(LogLevel.Warning, "Some memory allocated by the translation library was not freed - {0}B left allocated. This might indicate a memory leak. Cleaning up...", Misc.NormalizeBinary(allocated));
                    foreach(var ptr in ourPointers.Keys)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }

            private void PrintAllocated()
            {
                parent.NoisyLog("Allocated is now {0}B.", Misc.NormalizeBinary(Interlocked.Read(ref allocated)));
            }

            private ConcurrentDictionary<IntPtr, long> ourPointers;
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

            public void InitializeForHook()
            {
                Initialize();
            }

            public void InitializeForWriting(ulong address, SysbusAccessWidth width, ulong value)
            {
                InterruptTransaction = !ExecuteWatchpoints(address, width, value);
            }

            public void InitializeForReading(ulong address, SysbusAccessWidth width)
            {
                InterruptTransaction = !ExecuteWatchpoints(address, width, null);
            }

            private void Initialize()
            {
                guard.Value = new object();
            }

            private bool ExecuteWatchpoints(ulong address, SysbusAccessWidth width, ulong? value)
            {
                Initialize();
                if(!parent.machine.SystemBus.TryGetWatchpointsAt(address, value.HasValue ? Access.Write : Access.Read, out var watchpoints))
                {
                    return true;
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
                        parent.TlibRestoreContext();
                        alreadyUpdated = true;
                    }

                    // for reading value is always set to 0
                    enabledWatchpoint.Invoke(parent, address, width, value ?? 0);
                    anyEnabled = true;
                }

                if(anyEnabled)
                {
                    parent.TlibRequestTranslationBlockInterrupt(1);

                    // tell sysbus to cancel the current transaction and return immediately
                    return false;
                }
                else
                {
                    foreach(var disabledWatchpoint in watchpoints)
                    {
                        disabledWatchpoint.Enabled = true;
                    }
                }

                return true;
            }

            public void OrderPause()
            {
                if(active && guard.Value == null)
                {
                    throw new InvalidOperationException("Trying to order pause without prior guard initialization on this thread.");
                }
            }

            public bool RequestTranslationBlockRestart(bool quiet = false)
            {
                if(guard.Value == null)
                {
                    if(!quiet)
                    {
                        parent.Log(LogLevel.Error, "Trying to request translation block restart without prior guard initialization on this thread.");
                    }
                    return false;
                }
                restartTranslationBlock = true;
                return true;
            }

            void IDisposable.Dispose()
            {
                if(restartTranslationBlock)
                {
                    restartTranslationBlock = false;
                    if(parent.UpdateContextOnLoadAndStore)
                    {
                        parent.TlibRestoreContext();
                    }
                    parent.TlibRequestTranslationBlockInterrupt(0);
                    return;
                }
                guard.Value = null;
            }

            public bool InterruptTransaction { get; private set; }

            [Constructor]
            private readonly ThreadLocal<object> guard;

            private readonly TranslationCPU parent;
            private bool active;
            private bool restartTranslationBlock;
        }

        protected enum Interrupt
        {
            Hard            = 1 << 1,
            TargetExternal0 = 1 << 3,
            TargetExternal1 = 1 << 4,
            TargetExternal2 = 1 << 6,
            TargetExternal3 = 1 << 9,
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
                if(LogFile == null && value)
                {
                    throw new RecoverableException("Log file not set. Nothing will be logged.");
                }
                logTranslatedBlocks = value;
                TlibSetOnBlockTranslationEnabled(value ? 1 : 0);
            }
        }

        public ulong TranslateAddress(ulong logicalAddress, MpuAccess accessType)
        {
            return TlibTranslateToPhysicalAddress(logicalAddress, (uint)accessType);
        }

        public void NativeUnwind()
        {
            TlibUnwind();
        }

        [PostDeserialization]
        protected void InitDisas()
        {
            try
            {
                disassembler = new LLVMDisassembler(this);
            }
            catch(ArgumentOutOfRangeException)
            {
                this.Log(LogLevel.Warning, "Could not initialize disassembly engine");
            }
            dirtyAddressesPtr = IntPtr.Zero;
        }

        #endregion

        public uint PageSize
        {
            get
            {
                return TlibGetPageSize();
            }
        }

        protected virtual void BeforeSave(IntPtr statePtr)
        {
        }

        protected virtual void AfterLoad(IntPtr statePtr)
        {
        }

        [Export]
        private uint IsInDebugMode()
        {
            return InDebugMode ? 1u : 0u;
        }

        private void UpdateBlockBeginHookPresent()
        {
            TlibSetBlockBeginHookPresent((blockBeginInternalHook != null || blockBeginUserHook != null || IsSingleStepMode || isAnyInactiveHook) ? 1u : 0u);
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
        protected ActionInt32 TlibRequestTranslationBlockInterrupt;

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
        protected FuncUInt64UInt64UInt32 TlibTranslateToPhysicalAddress;

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
        private ActionUInt64 TlibFlushPage;

        [Import]
        private FuncUInt32 TlibGetMaximumBlockSize;

        [Import]
        private ActionUInt32 TlibSetMillicyclesPerInstruction;

        [Import]
        private FuncUInt32 TlibGetMillicyclesPerInstruction;

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
        protected ActionUInt64 TlibResetExecutedInstructions;

        [Import]
        private ActionUInt32 TlibSetInterruptBeginHookPresent;

        [Import]
        private ActionUInt32 TlibSetCpuWfiStateChangeHookPresent;

        [Import]
        private ActionUInt32 TlibSetInterruptEndHookPresent;

        [Import]
        private FuncUInt64 TlibGetTotalExecutedInstructions;

        [Import]
        private ActionInt32 TlibOnMemoryAccessEventEnabled;

        [Import]
        private Action TlibCleanWfiProcState;

        [Import]
        private ActionUInt64 TlibSetPageIoAccessed;

        [Import]
        private ActionUInt64 TlibClearPageIoAccessed;

        [Import]
        private FuncUInt32 TlibGetCurrentTbDisasFlags;

        [Import(UseExceptionWrapper = false)]
        private Action TlibUnwind;

        [Import]
        private FuncUInt32 TlibGetMmuWindowsCount;

        [Import]
        private ActionUInt32 TlibRaiseException;

        [Import]
        private ActionUInt32 TlibEnableExternalWindowMmu;

        [Import]
        private FuncInt32UInt32 TlibAcquireMmuWindow;

        [Import]
        private ActionUInt32 TlibResetMmuWindow;

        [Import]
        private ActionUInt32UInt64 TlibSetMmuWindowStart;

        [Import]
        private ActionUInt32UInt64UInt32 TlibSetMmuWindowEnd;

        [Import]
        private ActionUInt32UInt32 TlibSetWindowPrivileges;

        [Import]
        private ActionUInt32UInt64 TlibSetMmuWindowAddend;

        [Import]
        private FuncUInt64UInt32 TlibGetMmuWindowStart;

        [Import]
        private FuncUInt64UInt32 TlibGetMmuWindowEnd;

        [Import]
        private FuncUInt32UInt32 TlibGetWindowPrivileges;

        [Import]
        private FuncUInt64UInt32 TlibGetMmuWindowAddend;

        [Import]
        private ActionInt32 TlibSetBroadcastDirty;

        [Import]
        private Action TlibOnLeavingResetState;

#pragma warning restore 649

        protected const int DefaultTranslationCacheSize = 32 * 1024 * 1024;

        [Export]
        protected virtual void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        private void LogDisassembly(ulong pc, uint size, uint flags)
        {
            if(LogFile == null)
            {
                return;
            }
            if(Disassembler == null)
            {
                return;
            }

            var phy = TranslateAddress(pc, MpuAccess.InstructionFetch);
            var symbol = Bus.FindSymbolAt(pc);
            var tab = Bus.ReadBytes(phy, (int)size, true, context: this);
            Disassembler.DisassembleBlock(pc, tab, flags, out var disas);

            if(disas == null)
            {
                return;
            }

            using(var file = File.AppendText(LogFile))
            {
                file.WriteLine("-------------------------");
                if(size > 0)
                {
                    file.Write("IN: {0} ", symbol ?? string.Empty);
                    if(phy != pc)
                    {
                        file.WriteLine("(physical: 0x{0:x8}, virtual: 0x{1:x8})", phy, pc);
                    }
                    else
                    {
                        file.WriteLine("(address: 0x{0:x8})", phy);
                    }
                }
                else
                {
                    // special case when disassembling magic addresses in Cortex-M
                    file.WriteLine("Magic PC value detected: 0x{0:x8}", flags > 0 ? pc | 1 : pc);
                }

                file.WriteLine(string.IsNullOrWhiteSpace(disas) ? string.Format("Cannot disassemble from 0x{0:x8} to 0x{1:x8}", pc, pc + size)  : disas);
                file.WriteLine(string.Empty);
            }
        }

        /// <summary>
        /// This function returns <see cref="CPUCore.Id" /> instead of <see cref="Slot" />, since <see cref="Slot" /> is assigned at platform creation time.
        /// It's usually not what is needed to identify a core in multi-processing context - instead `cpuId` (passed in CPU constructor) is used.
        /// </summary>
        [Export]
        private uint GetMpIndex()
        {
            return Id;
        }

        public string DisassembleBlock(ulong addr = ulong.MaxValue, uint blockSize = 40, uint flags = 0)
        {
            if(Disassembler == null)
            {
                throw new RecoverableException("Disassembly engine not available");
            }
            if(addr == ulong.MaxValue)
            {
                addr = PC;
            }

            var translatedAddr = TranslateAddress(addr, MpuAccess.InstructionFetch);
            if(translatedAddr != ulong.MaxValue)
            {
                addr = translatedAddr;
            }

            var opcodes = Bus.ReadBytes(addr, (int)blockSize, true, context: this);
            Disassembler.DisassembleBlock(addr, opcodes, flags, out var result);
            return result;
        }

        [Transient]
        private LLVMDisassembler disassembler;

        public LLVMDisassembler Disassembler => disassembler;

        protected static readonly Exception InvalidInterruptNumberException = new InvalidOperationException("Invalid interrupt number.");

        private const int DefaultMaximumBlockSize = 0x7FF;
        private bool externalMmuEnabled;
        private readonly uint externalMmuWindowsCount;

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
                UpdateBlockBeginHookPresent();
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
                UpdateBlockBeginHookPresent();
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

        public void EnableProfiling()
        {
            AddHookAtInterruptBegin(exceptionIndex =>
            {
                machine.Profiler.Log(new ExceptionEntry(exceptionIndex));
            });

            SetHookAtMemoryAccess((_, operation, address) =>
            {
                switch(operation)
                {
                    case MemoryOperation.MemoryIORead:
                    case MemoryOperation.MemoryIOWrite:
                        machine.Profiler?.Log(new PeripheralEntry((byte)operation, address));
                        break;
                    case MemoryOperation.MemoryRead:
                    case MemoryOperation.MemoryWrite:
                        machine.Profiler?.Log(new MemoryEntry((byte)operation));
                        break;
                }
            });
        }

        protected override bool ExecutionFinished(ExecutionResult result)
        {
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
                return true;
            }
            else if(result == ExecutionResult.StoppedAtWatchpoint)
            {
                this.Trace();
                // If we stopped at a watchpoint we must've been in the process
                // of executing an instruction which accesses memory.
                // That means that if there have been any hooks added for the current PC,
                // they were already executed, and the PC has been moved back by one instruction.
                // We don't want to execute them again, so we disable them temporarily.
                DeactivateHooks(PC);
                return true;
            }
            else if(result == ExecutionResult.WaitingForInterrupt)
            {
                if(InDebugMode || neverWaitForInterrupt)
                {
                    // NIP always points to the next instruction, on all emulated cores. If this behavior changes, this needs to change as well.
                    this.Trace("Clearing WaitForInterrupt processor state.");
                    TlibCleanWfiProcState(); // Clean WFI state in the emulated core
                    return true;
                }
            }

            return false;
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

        protected enum TlibExecutionResult : ulong
        {
            Ok = 0x10000,
            WaitingForInterrupt = 0x10001,
            StoppedAtBreakpoint = 0x10002,
            StoppedAtWatchpoint = 0x10004,
            ReturnRequested = 0x10005,
            ExternalMmuFault = 0x10006,
        }

        protected override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            ActivateNewHooks();

            try
            {
                while(actionsToExecuteOnCpuThread.TryDequeue(out var queuedAction))
                {
                    queuedAction();
                }

                pauseGuard.Enter();
                lastTlibResult = (TlibExecutionResult)TlibExecute(checked((int)numberOfInstructionsToExecute));
                pauseGuard.Leave();
            }
            catch(CpuAbortException)
            {
                this.NoisyLog("CPU abort detected, halting.");
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

            switch(lastTlibResult)
            {
                case TlibExecutionResult.Ok:
                    return ExecutionResult.Ok;

                case TlibExecutionResult.WaitingForInterrupt:
                    return ExecutionResult.WaitingForInterrupt;

                case TlibExecutionResult.ExternalMmuFault:
                    return ExecutionResult.ExternalMmuFault;

                case TlibExecutionResult.StoppedAtBreakpoint:
                    return ExecutionResult.StoppedAtBreakpoint;

                case TlibExecutionResult.StoppedAtWatchpoint:
                    return ExecutionResult.StoppedAtWatchpoint;

                case TlibExecutionResult.ReturnRequested:
                    return ExecutionResult.Interrupted;

                default:
                    throw new Exception();
            }
        }

        public void SetBroadcastDirty(bool enable)
        {
            TlibSetBroadcastDirty(enable ? 1 : 0);
        }

        private string logFile;
        private bool isAnyInactiveHook;
        private Dictionary<ulong, HookDescriptor> hooks;
        private Dictionary<Interrupt, HashSet<int>> decodedIrqs;
        private bool isInterruptLoggingEnabled;

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
                // As hooks can be removed inside the callback, .ToList()
                // is required to avoid _Collection was modified_ exception.
                foreach(var callback in callbacks.ToList())
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
    }
}

