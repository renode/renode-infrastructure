//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICpuSupportingGdb : ICPUWithHooks, IControllableCPU
    {
        ulong Step(int count = 1);
        ExecutionMode ExecutionMode { get; set; }
        event Action<HaltArguments> Halted;
        void EnterSingleStepModeSafely(HaltArguments args);

        string GDBArchitecture { get; }
        bool DebuggerConnected { get; set; }
        uint Id { get; }
        string Name { get; }
    }
}

