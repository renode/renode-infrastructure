//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DisassemblerAttribute : Attribute
    {
        public DisassemblerAttribute(string name, string[] architectures)
        {
            Name = name;
            Architectures = architectures;
        }

        public string Name { get; private set; }
        public string[] Architectures { get; private set; }
    }
}

