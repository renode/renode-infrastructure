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
    public interface ICpuSupportingGdb : ICPUWithHooks, ICPUWithRegisters
    {
        void EnterSingleStepModeSafely(HaltArguments args, bool? blocking = null);

        string GDBArchitecture { get; }
        List<GDBFeatureDescriptor> GDBFeatures { get; }
        bool DebuggerConnected { get; set; }
    }
}

