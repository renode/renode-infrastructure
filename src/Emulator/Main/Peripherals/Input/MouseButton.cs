//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Input
{
    [Flags]
    public enum MouseButton
    {
        Left   = 0x01,
        Right  = 0x02,
        Middle = 0x04,
        Side   = 0x08,
        Extra  = 0x10
    }
}

