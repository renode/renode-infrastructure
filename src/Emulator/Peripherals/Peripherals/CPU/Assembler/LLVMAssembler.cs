//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.Disassembler;

namespace Antmicro.Renode.Peripherals.CPU.Assembler
{
    public class LLVMAssembler
    {
        public LLVMAssembler(ICPUSupportingLLVMDisas cpu)
        {
            this.cpu = cpu;
        }

        public byte[] AssembleBlock(ulong pc, string code, string triple, bool alternateDialect)
        {
            LLVMDisassembler.ValidateTriple(cpu, ref triple);
            var model = cpu.LLVMModel;
            // We need to initialize the architecture to be used before trying to assemble.
            // It's OK and cheap to initialize it multiple times, as this only sets a few pointers.
            LLVMDisasBindings.InitLlvmArchitecture(triple);
            if(!xtensaSupportWarningIssued && triple == "xtensa")
            {
                Logger.Log(LogLevel.Warning, "The assembler for Xtensa is currently an experimental feature in Renode");
                xtensaSupportWarningIssued = true;
            }
            bool ok;
            IntPtr output;
            IntPtr outLen;
            try
            {
                unsafe
                {
                    ok = LLVMDisasBindings.LlvmAsm(triple, model, alternateDialect ? 1 : 0u, code, pc, (IntPtr)(&output), (IntPtr)(&outLen));
                }
            }
            catch(EntryPointNotFoundException e)
            {
                throw new RecoverableException("Old version of libllvm-disas is in use, assembly is not available: ", e);
            }
            if(!ok)
            {
                var error = Marshal.PtrToStringAnsi(output);
                LLVMDisasBindings.LlvmFreeAsmResult(output);
                throw new RecoverableException(string.Format("Failed to assemble. Reason: {0}", error));
            }

            var result = new byte[(int)outLen];
            Marshal.Copy(output, result, 0, (int)outLen);
            LLVMDisasBindings.LlvmFreeAsmResult(output);
            return result;
        }

        private static bool xtensaSupportWarningIssued = false;

        private readonly ICPUSupportingLLVMDisas cpu;
    }
}