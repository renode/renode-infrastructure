//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUSupportingLLVMDisas : ICPU
    {
        string GetLLVMTriple(uint flags);

        string[] AllLLVMTriples { get; }

        string LLVMModel { get; }

        Endianess DisassemblyHexFormatting { get; }
    }
}
