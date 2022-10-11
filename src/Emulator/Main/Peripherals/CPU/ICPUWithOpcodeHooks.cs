//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithPostOpcodeExecutionHooks : ICPU
    {
        void AddPostOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong> hook);
        void EnablePostOpcodeExecutionHooks(UInt32 value);
    }
}

