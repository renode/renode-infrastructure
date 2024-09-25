//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.CPU.Assembler
{
    public class LLVMAssembler : IAssembler
    {
        public LLVMAssembler(ICPU cpu)
        {
            if(!LLVMArchitectureMapping.IsSupported(cpu))
            {
                throw new ArgumentOutOfRangeException(nameof(cpu));
            }
            this.cpu = cpu;
        }

        public byte[] AssembleBlock(ulong pc, string code, uint flags)
        {
            LLVMArchitectureMapping.GetTripleAndModelKey(cpu, flags, out var triple, out var model);
            // We need to initialize the architecture to be used before trying to assemble.
            // It's OK and cheap to initialize it multiple times, as this only sets a few pointers.
            init_llvm_architecture(triple);
            bool ok;
            IntPtr output;
            IntPtr outLen;
            try
            {
                ok = llvm_asm(triple, model, flags, code, pc, out output, out outLen);
            }
            catch(EntryPointNotFoundException e)
            {
                throw new RecoverableException("Old version of libllvm-disas is in use, assembly is not available: ", e);
            }
            if(!ok)
            {
                var error = Marshal.PtrToStringAnsi(output);
                llvm_free_asm_result(output);
                throw new RecoverableException(string.Format("Failed to assemble. Reason: {0}", error));
            }

            var result = new byte[(int)outLen];
            Marshal.Copy(output, result, 0, (int)outLen);
            llvm_free_asm_result(output);
            return result;
        }

        private readonly ICPU cpu;

        [DllImport("libllvm-disas")]
        private static extern IntPtr init_llvm_architecture(string triple);

        [DllImport("libllvm-disas")]
        private static extern bool llvm_asm(string arch, string cpu, uint flags, string instructions, ulong addr, out IntPtr output, out IntPtr outLen);

        [DllImport("libllvm-disas")]
        private static extern void llvm_free_asm_result(IntPtr result);
    }
}
