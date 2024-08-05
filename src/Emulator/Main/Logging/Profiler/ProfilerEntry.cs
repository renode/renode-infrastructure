//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging.Profiling
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public abstract class BaseEntry
    {
        public BaseEntry(ProfilerEntryType type)
        {
            RealTime = CustomDateTime.Now.Ticks;
            VirtualTime = TimeDomainsManager.Instance.VirtualTimeStamp.TimeElapsed.TotalMilliseconds;
            Type = type;
        }

        // RealTime and VirtualTime are expressed in other units (ticks, miliseconds).
        private long RealTime { get; }
        private double VirtualTime { get; }
        private ProfilerEntryType Type { get; }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class InstructionEntry : BaseEntry
    {
        public InstructionEntry(int cpuSlot, ulong executedInstructions) : base(ProfilerEntryType.ExecutedInstructions)
        {
            CpuSlot = cpuSlot;
            ExecutedInstructions = executedInstructions;
        }

        private int CpuSlot { get; }
        private ulong ExecutedInstructions { get; }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class MemoryEntry : BaseEntry
    {
        public MemoryEntry(byte operation) : base(ProfilerEntryType.MemoryAccess)
        {
            Operation = operation;
        }

        private byte Operation { get; }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PeripheralEntry : BaseEntry
    {
        public PeripheralEntry(byte operation, ulong address) : base(ProfilerEntryType.PeripheralAccess)
        {
            Operation = operation;
            Address = address;
        }

        private byte Operation { get; }
        private ulong Address { get; }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class ExceptionEntry : BaseEntry
    {
        public ExceptionEntry(ulong index) : base(ProfilerEntryType.Exception)
        {
            Index = index;
        }
        
        private ulong Index { get; }
    }

    public enum ProfilerEntryType : byte
    {
        ExecutedInstructions,
        MemoryAccess,
        PeripheralAccess,
        Exception
    }

    public enum MemoryOperation: byte
    {
        MemoryIORead,
        MemoryIOWrite,
        MemoryRead,
        MemoryWrite,
        InsnFetch,
    }
}
