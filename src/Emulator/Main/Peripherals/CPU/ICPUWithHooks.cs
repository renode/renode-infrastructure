//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithHooks : ICPU
    {
        void ClearHookAtBlockBegin();
        void SetHookAtBlockBegin(Action<ulong, uint> hook);
        void SetHookAtBlockEnd(Action<ulong, uint> hook);
        void AddHookAtInterruptBegin(Action<ulong> hook);
        void AddHookAtInterruptEnd(Action<ulong> hook);

        void AddHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook);
        void RemoveHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook);
        void RemoveHooksAt(ulong addr);
        void RemoveAllHooks();
    }
}

