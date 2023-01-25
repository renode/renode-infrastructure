//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Information about an unhandled field in a register.
    /// </summary>
    public struct Tag
    {
        public String Name;
        public int Position;
        public int Width;
        public ulong? AllowedValue;
    }
}
