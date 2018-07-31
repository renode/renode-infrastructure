//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public interface IDisassemblable
    {
        SystemBus Bus { get; }
        bool LogTranslatedBlocks { get; set; }

        string Architecture { get; }
        string Model { get; }
    }
}

