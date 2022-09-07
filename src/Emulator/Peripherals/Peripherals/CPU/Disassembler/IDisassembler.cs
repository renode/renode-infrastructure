//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public interface IDisassembler
    {
        bool TryDisassembleInstruction(ulong pc, byte[] memory, uint flags, out DisassemblyResult result, int memoryOffset = 0);
        bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0);
    }

    public struct DisassemblyResult
    {
        public ulong PC { get; set; }
        public int OpcodeSize { get; set; }
        public string OpcodeString { get; set; } 
        public string DisassemblyString { get; set; }

        public override string ToString()
        {
            return $"0x{PC:x8}:   {OpcodeString} {DisassemblyString}";
        }
    }
}

