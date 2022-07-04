//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct GDBTypeEnumValue
    {
        public GDBTypeEnumValue(string name, uint value) : this()
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; }
        public uint Value { get; }
    }
}

