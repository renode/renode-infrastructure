//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    [Flags]
    public enum SysbusAccessWidth
    {
        Byte = 1,
        Word = 2,
        DoubleWord = 4,
        QuadWord = 8
    }
}