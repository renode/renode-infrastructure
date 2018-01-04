//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Runtime.InteropServices;
using Antmicro.Renode.Peripherals.CPU.Disassembler;

namespace Antmicro.Renode.Disassembler.LLVM
{
    public static class CortexMAddressTranslator
    {
        public static DisassemblyProvider Wrap(DisassemblyProvider provider)
        {
            return (pc, memory, size, flags, output, outputSize) => CortexMDisassembler(pc, memory, size, flags, output, outputSize, provider);
        }

        private static void CopyTextToTheMemory(string text, IntPtr memory, ulong memorySize)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            Marshal.Copy(bytes, 0, memory, Math.Min(text.Length, (int)memorySize));
        }

        private static int CortexMDisassembler(ulong pc, IntPtr memory, ulong size, uint flags, IntPtr output, ulong outputSize, DisassemblyProvider provider)
        {
            switch(pc)
            {
            case 0xFFFFFFF0:
            case 0xFFFFFFF1:
                // Return to Handler mode, exception return uses non-floating-point state from the MSP and execution uses MSP after return.
                CopyTextToTheMemory("Handler mode: non-floating-point state, MSP/MSP\n", output, outputSize);
                return 4;
            case 0xFFFFFFF8:
            case 0xFFFFFFF9:
                // Return to Thread mode, exception return uses non-floating-point state from the MSP and execution uses MSP after return.
                CopyTextToTheMemory("Thread mode: non-floating-point state, MSP/MSP\n", output, outputSize);
                return 4;
            case 0xFFFFFFFC:
            case 0xFFFFFFFD:
                // Return to Thread mode, exception return uses non-floating-point state from the PSP and execution uses PSP after return.
                CopyTextToTheMemory("Thread mode: non-floating-point state, PSP/PSP\n", output, outputSize);
                return 4;
            case 0xFFFFFFE0:
            case 0xFFFFFFE1:
                // Return to Handler mode, exception return uses floating-point state from the MSP and execution uses MSP after return.
                CopyTextToTheMemory("Handler mode: floating-point state, MSP/MSP\n", output, outputSize);
                return 4;
            case 0xFFFFFFE8:
            case 0xFFFFFFE9:
                // Return to Thread mode, exception return uses floating-point state from the MSP and execution uses MSP after return.
                CopyTextToTheMemory("Thread mode: floating-point state, MSP/MSP\n", output, outputSize);
                return 4;
            case 0xFFFFFFEC:
            case 0xFFFFFFED:
                // Return to Thread mode, exception return uses floating-point state from the PSP and execution uses PSP after return.
                CopyTextToTheMemory("Thread mode: floating-point state, PSP/PSP\n", output, outputSize);
                return 4;
            default:
                return provider(pc, memory, size, flags, output, outputSize);
            }
        }
    }
}

