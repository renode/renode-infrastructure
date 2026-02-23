//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
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
