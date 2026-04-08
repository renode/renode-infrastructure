//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public static class LLVMDisasBindings
    {
        static LLVMDisasBindings()
        {
            var libPath = PlatformFileLoader.FindPlatformFile("libllvm-disas.so");
            binder = new NativeBinder(typeof(LLVMDisasBindings), libPath);
        }

        [Import(UseExceptionWrapper = false)]
        public static Func<string, IntPtr> InitLlvmArchitecture;

        [Import(UseExceptionWrapper = false)]
        public static Func<string, string, uint, string, ulong, IntPtr, IntPtr, bool> LlvmAsm;

        [Import(UseExceptionWrapper = false)]
        public static Action<IntPtr> LlvmFreeAsmResult;

        [Import(UseExceptionWrapper = false)]
        public static Func<IntPtr, IntPtr, UInt64, IntPtr, UInt32, int> LlvmDisasmInstruction;

        [Import(UseExceptionWrapper = false)]
        public static Func<string, string, IntPtr> LlvmCreateDisasmCpu;

        [Import(UseExceptionWrapper = false, Optional = true)]
        public static Func<string, string, uint, IntPtr> LlvmCreateDisasmCpuWithFlags;

        [Import(UseExceptionWrapper = false)]
        public static Action<IntPtr> LlvmDisasmDispose;

        [Import(UseExceptionWrapper = false)]
        public static Action<string, IntPtr> LlvmDisasmGetCpuFeatures;

        private static readonly NativeBinder binder;
    }
}
