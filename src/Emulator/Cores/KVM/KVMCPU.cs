//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;

using ELFSharp.ELF;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class KVMCPU : BaseCPU, IGPIOReceiver, ICPUWithRegisters, IControllableCPU, ICPUWithMappedMemory
    {
        public KVMCPU(string cpuType, IMachine machine, Endianess endianess, CpuBitness cpuBitness, uint cpuId = 0)
            : base(cpuId, cpuType, machine, endianess, cpuBitness)
        {
            currentMappings = new List<SegmentMappingWithSlotNumber>();
            InitBinding();
            Init();
            machine.PeripheralsChanged += OnMachinePeripheralsChanged;
        }

        public virtual void OnGPIO(int number, bool value)
        {
            if(number < 0 || number > MaxRedirectionTableEntries)
            {
                throw new ArgumentOutOfRangeException(string.Format("IOAPIC has {0} interrupts, but {1} was triggered", MaxRedirectionTableEntries, number));
            }
            KvmSetIrq(value ? 1 : 0, number);
        }

        public void MapMemory(IMappedSegment segment)
        {
            if(segment.StartingOffset > bitness.GetMaxAddress() || segment.StartingOffset + (segment.Size - 1) > bitness.GetMaxAddress())
            {
                throw new RecoverableException($"Could not map memory segment: {segment.GetRange()} is not within addressable memory space");
            }

            using(machine?.ObtainPausedState(true))
            {
                var slotNumber = numberOfSegmentSlots++;
                var mapping = new SegmentMappingWithSlotNumber(segment, slotNumber);
                var range = segment.GetRange();
                mapping.Segment.Touch();
                currentMappings.Add(mapping);
                mappedMemory.Add(range);
                KvmMapRange(slotNumber, segment.StartingOffset, segment.Size, (ulong)mapping.Segment.Pointer);
                this.NoisyLog("Registered memory at {0}", range);
            }
        }

        public void SetMappedMemoryEnabled(Range range, bool enabled)
        {
            throw new RecoverableException("SetMappedMemoryEnabled is not implemented");
        }

        public void UnmapMemory(Range range)
        {
            using(machine?.ObtainPausedState(true))
            {
                if(!mappedMemory.ContainsWholeRange(range))
                {
                    throw new ConstructionException(
                        $"Memory range {range} requested to unmap doesn't fit into mapped memory range"
                    );
                }

                // In KVM, only whole segments may be unmapped. Check if range contains some segment, but only partially
                if(currentMappings.Any(x => range.Contains(x.Segment.StartingOffset) && !range.Contains(x.Segment.GetRange())))
                {
                    throw new ConstructionException(
                        "Only whole segments may be unmapped"
                    );
                }

                var mappingsToRemove = currentMappings.Where(x => range.Contains(x.Segment.GetRange())).ToList();
                mappingsToRemove.ForEach(x => KvmUnmapRange(x.SlotNumber));

                currentMappings.RemoveAll(x => range.Contains(x.Segment.GetRange()));

                mappedMemory.Remove(range);
            }
        }

        public void RegisterAccessFlags(ulong startAddress, ulong size, bool isIoMemory = false)
        {
            // all ArrayMemory is set as executable by default
            throw new ConstructionException($"Use of ArrayMemory with {nameof(KVMCPU)} core is not supported: Execution via IO accesses is not implemented");
        }

        public void SetPageAccessViaIo(ulong address)
        {
            throw new RecoverableException("SetPageAccessViaIo is not implemented");
        }

        public void ClearPageAccessViaIo(ulong address)
        {
            throw new RecoverableException("ClearPageAccessViaIo is not implemented");
        }

        public void SetBroadcastDirty(bool enable)
        {
            throw new RecoverableException("SetBroadcastDirty is not implemented");
        }

        public override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            if(ExecutionMode == ExecutionMode.SingleStep)
            {
                numberOfExecutedInstructions = 1;
                return (ExecutionResult)KvmExecuteSingleStep();
            }
            else
            {
                // Current implementation doesn't allow to running exact number of instructions, we just allow it to run for some time
                // that is proportional to expected instructions count.
                // Due to intricacies of modern CPUs this will also be non-deterministic between runs.
                var time = TimeInterval.FromCPUCycles(numberOfInstructionsToExecute, PerformanceInMips, out var cyclesResiduum).TotalMicroseconds;

                numberOfExecutedInstructions = numberOfInstructionsToExecute;
                return (ExecutionResult)KvmExecute((ulong)time);
            }
        }

        public void RequestReturn()
        {
            if(this.IsStarted)
            {
                KvmInterruptExecution();
            }
        }

        public override string ToString()
        {
            return $"[CPU: {this.GetCPUThreadName(machine)}]";
        }

        public abstract void SetRegister(int register, RegisterValue value);

        public abstract RegisterValue GetRegister(int register);

        public abstract IEnumerable<CPURegister> GetRegisters();

        public override ulong ExecutedInstructions => throw new RecoverableException("ExecutedInstructions property is not implemented");

        protected virtual void InitializeRegisters()
        {
        }

        protected override void DisposeInner(bool silent = false)
        {
            base.DisposeInner(silent);
            KvmDispose();
            TimeHandle.Dispose();
            binder.Dispose();
            if(!EmulationManager.DisableEmulationFilesCleanup)
            {
                File.Delete(libraryFile);
            }
        }

        protected override void RequestPause()
        {
            base.RequestPause();
            if(this.IsStarted)
            {
                KvmInterruptExecution();
            }
        }

        protected override bool ExecutionFinished(ExecutionResult result)
        {
            return false;
        }

        protected virtual void Init()
        {
            KvmInit();
        }

        protected virtual void OnMachinePeripheralsChanged(IMachine machine, PeripheralsChangedEventArgs args)
        {
            // We have another CPU in platform, currently we don't support it in KVM based emulation.
            if(args.Peripheral is ICPU && args.Peripheral != this)
            {
                throw new RegistrationException($"{this.GetType()} cpu doesn't support multicore setups");
            }
        }

        [PreSerialization]
        protected virtual void BeforeSerialization()
        {
            throw new NonSerializableTypeException($"Serialization of {this.GetType()} is not implemented");
        }

        [Export]
        protected void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        protected void ReportAbort(string message)
        {
            this.Log(LogLevel.Error, message);
            throw new CpuAbortException(message);
        }

        [Export]
        protected void ReportRuntimeAbort(string message, ulong pc)
        {
            this.Log(LogLevel.Error, "CPU abort [PC=0x{0:X}]: {1}", pc, message);
            throw new CpuAbortException(message);
        }

        [Export]
        protected ulong ReadByteFromBus(ulong offset)
        {
            return (ulong)machine.SystemBus.ReadByte(offset, this);
        }

        [Export]
        protected ulong ReadWordFromBus(ulong offset)
        {
            return (ulong)machine.SystemBus.ReadWord(offset, this);
        }

        [Export]
        protected ulong ReadDoubleWordFromBus(ulong offset)
        {
            return (ulong)machine.SystemBus.ReadDoubleWord(offset, this);
        }

        [Export]
        protected ulong ReadQuadWordFromBus(ulong offset)
        {
            return machine.SystemBus.ReadQuadWord(offset, this);
        }

        [Export]
        protected void WriteByteToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteByte(offset, unchecked((byte)value), this);
        }

        [Export]
        protected void WriteWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteWord(offset, unchecked((ushort)value), this);
        }

        [Export]
        protected void WriteDoubleWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteDoubleWord(offset, unchecked((uint)value), this);
        }

        [Export]
        protected void WriteQuadWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteQuadWord(offset, value, this);
        }

        [Export]
        protected uint ReadByteFromPort(ushort address)
        {
            return (uint)ReadByteFromBus(IoPortBaseAddress + address);
        }

        [Export]
        protected uint ReadWordFromPort(ushort address)
        {
            return (uint)ReadWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        protected uint ReadDoubleWordFromPort(ushort address)
        {
            return (uint)ReadDoubleWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        protected void WriteByteToPort(ushort address, uint value)
        {
            WriteByteToBus(IoPortBaseAddress + address, (byte)value);
        }

        [Export]
        protected void WriteWordToPort(ushort address, uint value)
        {
            WriteWordToBus(IoPortBaseAddress + address, (ushort)value);
        }

        [Export]
        protected void WriteDoubleWordToPort(ushort address, uint value)
        {
            WriteDoubleWordToBus(IoPortBaseAddress + address, (uint)value);
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        protected Action KvmInit;

        [Import]
        protected Func<ulong, ulong> KvmExecute;

        [Import]
        protected Func<ulong> KvmExecuteSingleStep;

        [Import]
        protected Action KvmInterruptExecution;

        [Import]
        protected Action KvmDispose;

        [Import]
        protected Action<int, ulong, ulong, ulong> KvmMapRange;

        [Import]
        protected Action<int> KvmUnmapRange;

        [Import]
        protected Action<int, int> KvmSetIrq;

#pragma warning restore 649

        protected string libraryFile;

        protected NativeBinder binder;

        protected int numberOfSegmentSlots;

        protected readonly List<SegmentMappingWithSlotNumber> currentMappings;

        protected readonly MinimalRangesCollection mappedMemory = new MinimalRangesCollection();

        protected const ulong IoPortBaseAddress = 0xE0000000;

        protected const int MaxRedirectionTableEntries = 24;

        /*
            Increments each time a new translation library resource is created.
            This counter marks each new instance of a kvm library with a new number, which is used in file names to avoid collisions.
            It has to survive emulation reset, so the file names remain unique.
        */
        private static int CpuCounter = 0;

        private void InitBinding()
        {
            var libraryResource = $"Antmicro.Renode.kvm-{Architecture}.so";
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
        }

        protected class SegmentMappingWithSlotNumber : SegmentMapping
        {
            public SegmentMappingWithSlotNumber(IMappedSegment segment, int slotNumber) : base(segment)
            {
                SlotNumber = slotNumber;
            }

            public int SlotNumber { get; set; }
        }
    }
}
