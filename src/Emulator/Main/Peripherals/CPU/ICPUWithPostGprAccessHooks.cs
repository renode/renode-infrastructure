//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithPostGprAccessHooks: ICPU
    {
        void InstallPostGprAccessHookOn(UInt32 registerIndex, Action<bool> callback, UInt32 value);
        void EnablePostGprAccessHooks(UInt32 value);
    }
}

