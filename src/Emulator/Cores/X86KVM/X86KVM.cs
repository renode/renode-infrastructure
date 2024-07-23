//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Migrant;
using System.IO;
using System.Linq;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Binding;
using ELFSharp.ELF;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using System.Threading;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86KVM : BaseCPU, IGPIOReceiver, ICPUWithRegisters, IControllableCPU, ICPUWithMappedMemory
    {
        public X86KVM(string cpuType, IMachine machine, uint cpuId = 0)
            : base(cpuId, cpuType, machine, Endianess.LittleEndian, CpuBitness.Bits32)
        {
            currentMappings = new List<SegmentMappingWithSlotNumber>();
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
            throw new ConstructionException($"Use of ArrayMemory with {nameof(X86KVM)} core is not supported: Execution via IO accesses is not implemented");
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

        // field in the 'flags' variable are defined in
        // Intel(R) 64 and IA-32 Architectures Software Developer’s Manual - Volume 3 (3.4.5)
        public void SetDescriptor(SegmentDescriptor descriptor, byte selector, ulong baseAddress, uint limit, uint flags)
        {
            switch(descriptor)
            {
                case SegmentDescriptor.CS:
                    KvmSetCsDescriptor(baseAddress, limit, selector, flags);
                    break;
                case SegmentDescriptor.DS:
                    KvmSetDsDescriptor(baseAddress, limit, selector, flags);
                    break;
                case SegmentDescriptor.ES:
                    KvmSetEsDescriptor(baseAddress, limit, selector, flags);
                    break;
                case SegmentDescriptor.SS:
                    KvmSetSsDescriptor(baseAddress, limit, selector, flags);
                    break;
                case SegmentDescriptor.FS:
                    KvmSetFsDescriptor(baseAddress, limit, selector, flags);
                    break;
                case SegmentDescriptor.GS:
                    KvmSetGsDescriptor(baseAddress, limit, selector, flags);
                    break;
                default:
                    throw new RecoverableException($"Setting the {descriptor} descriptor is not implemented, ignoring");
            }
        }

        public override string ToString()
        {
            return $"[CPU: {this.GetCPUThreadName(machine)}]";
        }

        public override string Architecture => "i386";

        public override ulong ExecutedInstructions => throw new RecoverableException("ExecutedInstructions property is not implemented");

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

        /*
            Increments each time a new translation library resource is created.
            This counter marks each new instance of a kvm library with a new number, which is used in file names to avoid collisions.
            It has to survive emulation reset, so the file names remain unique.
        */
        private static int CpuCounter = 0;

        private void Init()
        {
            var libraryResource = string.Format("Antmicro.Renode.kvm-{0}.so", Architecture);
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

            KvmInit();
        }

        private void OnMachinePeripheralsChanged(IMachine machine, PeripheralsChangedEventArgs args)
        {
            // We have another CPU in platform, currently we don't support it in KVM based emulation.
            if(args.Peripheral is ICPU && args.Peripheral != this)
            {
                throw new RegistrationException($"{this.GetType()} cpu doesn't support multicore setups");
            }
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            throw new NonSerializableTypeException($"Serialization of {this.GetType()} is not implemented");
        }

        [Export]
        private void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        private void ReportAbort(string message)
        {
            this.Log(LogLevel.Error, "CPU abort [PC=0x{0:X}]: {1}.", PC.RawValue, message);
            throw new CpuAbortException(message);
        }

        [Export]
        private ulong ReadByteFromBus(ulong offset)
        {
            return (ulong)machine.SystemBus.ReadByte(offset, this);
        }

        [Export]
        private ulong ReadWordFromBus(ulong offset)
        {
            return (ulong)machine.SystemBus.ReadWord(offset, this);
        }

        [Export]
        private ulong ReadDoubleWordFromBus(ulong offset)
        {
            return machine.SystemBus.ReadDoubleWord(offset, this);
        }

        [Export]
        private ulong ReadQuadWordFromBus(ulong offset)
        {
            return machine.SystemBus.ReadQuadWord(offset, this);
        }

        [Export]
        private void WriteByteToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteByte(offset, unchecked((byte)value), this);
        }

        [Export]
        private void WriteWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteWord(offset, unchecked((ushort)value), this);
        }

        [Export]
        private void WriteDoubleWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteDoubleWord(offset, (uint)value, this);
        }

        [Export]
        private void WriteQuadWordToBus(ulong offset, ulong value)
        {
            machine.SystemBus.WriteQuadWord(offset, value, this);
        }

        [Export]
        private uint ReadByteFromPort(uint address)
        {
            return (uint)ReadByteFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private uint ReadWordFromPort(uint address)
        {
            return (uint)ReadWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private uint ReadDoubleWordFromPort(uint address)
        {
            return (uint)ReadDoubleWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private void WriteByteToPort(uint address, uint value)
        {
            WriteByteToBus(IoPortBaseAddress + address, value);

        }

        [Export]
        private void WriteWordToPort(uint address, uint value)
        {
            WriteWordToBus(IoPortBaseAddress + address, value);
        }

        [Export]
        private void WriteDoubleWordToPort(uint address, uint value)
        {
            WriteDoubleWordToBus(IoPortBaseAddress + address, value);
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private Action KvmInit;

        [Import]
        private Func<ulong, ulong> KvmExecute;

        [Import]
        private Func<ulong> KvmExecuteSingleStep;

        [Import]
        private Action KvmInterruptExecution;

        [Import]
        private Action KvmDispose;

        [Import]
        private Action<int, ulong, ulong, ulong> KvmMapRange;

        [Import]
        private Action<int> KvmUnmapRange;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetCsDescriptor;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetDsDescriptor;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetEsDescriptor;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetSsDescriptor;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetFsDescriptor;

        [Import]
        private Action<ulong, uint, uint, uint> KvmSetGsDescriptor;

        [Import]
        private Action<int, int> KvmSetIrq;

        #pragma warning restore 649

        private string libraryFile;

        private NativeBinder binder;

        private int numberOfSegmentSlots;

        private readonly List<SegmentMappingWithSlotNumber> currentMappings;

        private readonly MinimalRangesCollection mappedMemory = new MinimalRangesCollection();

        private const uint IoPortBaseAddress = 0xE0000000;

        private const int MaxRedirectionTableEntries = 24;

        public enum SegmentDescriptor
        {
            CS,
            SS,
            DS,
            ES,
            FS,
            GS
        }

        private class SegmentMappingWithSlotNumber : SegmentMapping
        {
            public int SlotNumber { get; set; }

            public SegmentMappingWithSlotNumber(IMappedSegment segment, int slotNumber) : base(segment)
            {
                SlotNumber = slotNumber;
            }
        }
    }
}
