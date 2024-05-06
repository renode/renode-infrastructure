//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICpuSupportingGdb : ICPUWithHooks, ICPUWithRegisters
    {
        void EnterSingleStepModeSafely(HaltArguments args);

        string GDBArchitecture { get; }
        List<GDBFeatureDescriptor> GDBFeatures { get; }
        bool DebuggerConnected { get; set; }
    }
}

