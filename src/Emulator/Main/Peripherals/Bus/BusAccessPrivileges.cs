//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    [Flags]
    public enum BusAccessPrivileges
    {
        None = 0b000,
        Read = 0b001,
        Write = 0b010,
        Other = 0b100
    }
}
