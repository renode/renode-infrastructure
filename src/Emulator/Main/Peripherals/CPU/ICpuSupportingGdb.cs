//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICpuSupportingGdb : ICPUWithHooks, IControllableCPU
    {
        ulong Step(int count = 1, bool? blocking = null);
        ExecutionMode ExecutionMode { get; set; }
        uint PageSize { get; }
        event Action<HaltArguments> Halted;
        void EnterSingleStepModeSafely(HaltArguments args, bool? blocking = null);

        TimeHandle TimeHandle { get; }
        string GDBArchitecture { get; }
        List<GDBFeatureDescriptor> GDBFeatures { get; }
        bool DebuggerConnected { get; set; }
        uint Id { get; }
    }
}

