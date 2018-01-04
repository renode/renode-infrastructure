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
        void SetHookAtBlockBegin(Action<uint, uint> hook);

        void AddHook(uint addr, Action<uint> hook);
        void RemoveHook(uint addr, Action<uint> hook);
        void RemoveHooksAt(uint addr);
        void RemoveAllHooks();
    }
}

