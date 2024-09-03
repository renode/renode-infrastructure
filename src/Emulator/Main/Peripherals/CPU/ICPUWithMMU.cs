//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithMMU
    {
        ulong TranslateAddress(ulong logicalAddress, MpuAccess accessType);
        bool TryTranslateAddress(ulong logicalAddress, MpuAccess accessType, out ulong physicalAddress);
        uint PageSize { get; }
    }
    
    public enum MpuAccess
    {
        Read = 0,
        Write = 1,
        InstructionFetch = 2
    }
}

