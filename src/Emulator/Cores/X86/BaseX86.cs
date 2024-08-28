//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Endianess = ELFSharp.ELF.Endianess;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.IRQControllers;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 1)]
    public abstract class BaseX86 : TranslationCPU
    {
        public BaseX86(string cpuType, IMachine machine, LAPIC lapic, CpuBitness bitness): base(cpuType, machine, Endianess.LittleEndian, bitness)
        {
            this.lapic = lapic;
        }

        public void SetDescriptor(SegmentDescriptor descriptor, uint selector, uint baseAddress, uint limit, uint flags)
        {
            switch(descriptor)
            {
                case SegmentDescriptor.CS:
                    TlibSetCsDescriptor(selector, baseAddress, limit, flags);
                    break;
                default:
                    throw new RecoverableException($"Setting the {descriptor} descriptor is not implemented");
            }
        }

        public bool HltAsNop
        { 
            get => neverWaitForInterrupt; 
            set
            {
                neverWaitForInterrupt = value;
            }
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0)
            {
                return Interrupt.Hard;
            }
            throw InvalidInterruptNumberException;
        }

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            return ExceptionDescriptionsMap.TryGetValue(exceptionIndex, out var result)
                ? result
                : base.GetExceptionDescription(exceptionIndex);
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

        [Export]
        private int GetPendingInterrupt()
        {
            return lapic.GetPendingInterrupt();
        }

        [Export]
        private ulong GetInstructionCount()
        {
            return this.ExecutedInstructions;
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        private ActionUInt32UInt32UInt32UInt32 TlibSetCsDescriptor;

#pragma warning restore 649

        private readonly LAPIC lapic;

        private readonly Dictionary<ulong, string> ExceptionDescriptionsMap = new Dictionary<ulong, string>
        {
            {0, "Division by zero"},
            {1, "Single-step interrupt"},
            {2, "NMI"},
            {3, "Breakpoint"},
            {4, "Overflow"},
            {5, "Bounds"},
            {6, "Invalid Opcode"},
            {7, "Coprocessor not available"},
            {8, "Double Fault"},
            {9, "Coprocessor Segment Overrun"},
            {10, "Invalid Task State Segment"},
            {11, "Segment not present"},
            {12, "Stack Fault"},
            {13, "General protection fault"},
            {14, "Page Fault"},
            {16, "Math Fault"},
            {17, "Alignment Check"},
            {18, "Machine Check"},
            {256, "System call"}
        };

        private const uint IoPortBaseAddress = 0xE0000000;

        public enum SegmentDescriptor
        {
            CS,
            SS,
            DS,
            ES,
            FS,
            GS
        }
    }
}
