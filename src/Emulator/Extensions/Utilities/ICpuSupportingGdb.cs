//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICpuSupportingGdb : ICPUWithHooks, IControllableCPU
    {
        void Step(int count = 1, bool wait = true);
        ExecutionMode ExecutionMode { get; set; }
        event Action<HaltArguments> Halted;
        void EnterSingleStepModeSafely(HaltArguments args);

        void StartGdbServer(int port);
        void StopGdbServer();
        bool IsGdbServerCreated { get; }
    }
}

