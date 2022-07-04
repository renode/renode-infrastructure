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
    public struct GDBTypeBitField
    {
        public GDBTypeBitField(string name, uint start, uint end, string type) : this()
        {
            this.Name = name;
            this.Start = start;
            this.End = end;
            this.Type = type;
        }

        public static GDBTypeBitField Filler(uint start, uint end, string type)
        {
            return new GDBTypeBitField("", start, end, type);
        }

        public string Name { get; }
        public uint Start { get; }
        public uint End { get; }
        public string Type { get; }
    }
}

