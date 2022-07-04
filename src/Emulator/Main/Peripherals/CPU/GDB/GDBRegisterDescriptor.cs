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
    public struct GDBRegisterDescriptor
    {
        public GDBRegisterDescriptor(uint number, uint size, string name, string type = null, string group = null) : this()
        {
            this.Number = number;
            this.Size = size;
            this.Name = name;
            this.Type = type;
            this.Group = group;
        }

        public uint Number { get; }
        public uint Size { get; }
        public string Name { get; }
        public string Type { get; }
        public string Group { get; }
    }
}

