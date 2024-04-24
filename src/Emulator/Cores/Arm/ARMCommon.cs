//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IARMSingleSecurityStateCPU : ICPU
    {
        ExceptionLevel ExceptionLevel { get; }

        Affinity Affinity { get; }
        // This kind of CPU is always in a specific Security State and it can't be changed
        SecurityState SecurityState { get; }

        bool FIQMaskOverride { get; }
        bool IRQMaskOverride { get; }
    }

    public interface IARMTwoSecurityStatesCPU : IARMSingleSecurityStateCPU
    {
        void GetAtomicExceptionLevelAndSecurityState(out ExceptionLevel exceptionLevel, out SecurityState securityState);

        // This property should return false if CPU doesn't support EL3
        bool IsEL3UsingAArch32State { get; }

        // The CPU may support two security states, but be in configuration that allows only one
        bool HasSingleSecurityState { get; }

        event Action<ExceptionLevel, SecurityState> ExecutionModeChanged;
    }

    public interface IARMCPUsConnectionsProvider
    {
        void AttachCPU(IARMSingleSecurityStateCPU cpu);
        // AttachedCPUs and CPUAttached provide information for GPIO handling purposes.
        // Depending on the declaration order in repl file, some CPUs can be attached before or after peripheral's creation.
        IEnumerable<IARMSingleSecurityStateCPU> AttachedCPUs { get; }
        event Action<IARMSingleSecurityStateCPU> CPUAttached;
    }

    public enum ExceptionLevel : uint
    {
        EL0_UserMode = 0,
        EL1_SystemMode = 1,
        EL2_HypervisorMode = 2,
        EL3_MonitorMode = 3
    }

    public enum ExecutionState
    {
        AArch32,
        AArch64
    }

    // GIC should use GPIO#0 of an ARM CPU to signal IRQ and GPIO#1 to signal FIQ
    // An ARM CPU should be connected to a GIC following the convention `[<N*4>-<N*4+3>] -> cpuN@[0-3]`";
    public enum InterruptSignalType
    {
        IRQ  = 0,
        FIQ  = 1,
        vIRQ = 2,
        vFIQ = 3,
    }

    public enum SecurityState
    {
        Secure,
        NonSecure
    }

    public class Affinity
    {
        public Affinity(byte level0, byte level1 = 0, byte level2 = 0, byte level3 = 0)
        {
            levels[0] = level0;
            levels[1] = level1;
            levels[2] = level2;
            levels[3] = level3;
        }

        public Affinity(uint allLevels)
        {
            AllLevels = allLevels;
        }

        public byte GetLevel(int levelIndex)
        {
            return levels[levelIndex];
        }

        public uint AllLevels
        {
            get => BitHelper.ToUInt32(levels, 0, levels.Length, reverse: true);
            protected set => BitHelper.GetBytesFromValue(levels, 0, value, levels.Length, reverse: true);
        }

        public override string ToString()
        {
            return String.Join(".", levels);
        }

        protected readonly byte[] levels = new byte[LevelsCount];
        protected const int LevelsCount = 4;
    }

    public class MutableAffinity : Affinity
    {
        public MutableAffinity() : base(0) { }

        public void SetLevel(int levelIndex, byte levelValue)
        {
            levels[levelIndex] = levelValue;
        }
    }
}
