//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public interface IFlaglessDisassembler
    {
        bool TryDisassembleInstruction(ulong pc, byte[] memory, out DisassemblyResult result, int memoryOffset = 0);

        bool TryDecodeInstruction(ulong pc, byte[] memory, out byte[] opcode, int memoryOffset = 0);
    }
}
