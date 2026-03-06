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
            if(DisassemblyString == null || DisassemblyString == "")
            {
                return $"0x{PC:x8}:   {OpcodeString}";
            }
            // This is a sane minimal length, based on some different binaries for quark.
            // X86 instructions do not have the upper limit of lenght, so we have to approximate.
            return $"0x{PC:x8}:   {OpcodeString,-14} {DisassemblyString}";
        }
    }
}
