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
    public struct GDBTypeField
    {
        public GDBTypeField(string name, string type) : this()
        {
            this.Name = name;
            this.Type = type;
        }

        public string Name { get; }
        public string Type { get; }
    }
}

