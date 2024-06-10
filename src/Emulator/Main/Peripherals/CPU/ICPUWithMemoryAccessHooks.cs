//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging.Profiling;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithMemoryAccessHooks : ICPU
    {
        void SetHookAtMemoryAccess(Action<ulong, MemoryOperation, ulong, ulong> hook);
    }
}

