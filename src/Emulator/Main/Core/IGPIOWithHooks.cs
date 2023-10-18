//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Core
{
    public interface IGPIOWithHooks : IGPIO
    {
        void AddStateChangedHook(Action<bool> hook);
        void RemoveStateChangedHook(Action<bool> hook);
    }
}

