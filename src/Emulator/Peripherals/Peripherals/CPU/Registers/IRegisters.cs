//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU.Registers
{
    public interface IRegisters
    {
        IEnumerable<int> Keys { get; }
        RegisterValue this[int index] { get; set; }
    }
}

