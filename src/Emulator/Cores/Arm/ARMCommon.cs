//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IARMSingleSecurityStateCPU
    {
        byte Affinity0 { get; }
        // This kind of CPU is always in a specific Security State and it can't be changed
        SecurityState SecurityState { get; }
    }

    public interface IARMTwoSecurityStatesCPU : IARMSingleSecurityStateCPU
    {
        void GetAtomicExceptionLevelAndSecurityState(out ExceptionLevel exceptionLevel, out SecurityState securityState);

        ExceptionLevel ExceptionLevel { get; }

        // This property should return false if CPU doesn't support EL3
        bool IsEL3UsingAArch32State { get; }

        // The CPU may support two security states, but be in configuration that allows only one
        bool HasSingleSecurityState { get; }

        event Action<ExceptionLevel, SecurityState> ExecutionModeChanged;
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
    // An ARM CPU should be connected to a GIC following the convention `[<N*2>-<N*2+1>] -> cpuN@[0-1]`";
    public enum InterruptSignalType
    {
        IRQ = 0,
        FIQ = 1,
    }

    public enum SecurityState
    {
        Secure,
        NonSecure
    }
}
