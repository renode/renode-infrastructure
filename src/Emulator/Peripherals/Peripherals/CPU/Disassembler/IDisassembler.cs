//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using System.Reflection;
using System.Linq;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public delegate int DisassemblyProvider(ulong pc, IntPtr memory, ulong size, UInt32 flags, IntPtr output, ulong outputSize);

    public interface IDisassembler
    {
        DisassemblyProvider Disassemble { get; }
        string Name { get; }
    }
}

