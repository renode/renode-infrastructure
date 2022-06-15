//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU.Assembler
{
    public interface IAssembler
    {
        byte[] AssembleBlock(ulong pc, string code, uint flags);
    }
}
