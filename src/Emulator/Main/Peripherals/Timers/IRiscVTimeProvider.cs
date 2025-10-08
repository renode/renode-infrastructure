﻿//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Timers
{
    public interface IRiscVTimeProvider
    {
        ulong TimerValue { get; }
    }
}