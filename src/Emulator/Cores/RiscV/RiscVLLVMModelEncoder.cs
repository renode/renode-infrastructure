//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Runtime.InteropServices;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class RiscVLLVMModelEncoder
    {
        public static string GetModel(ICPUSupportingLLVMDisas cpu)
        {
            // Cache is not only to improve performance but also to log unsupported extensions once.
            lock(modelsCache)
            {
                return modelsCache.Get<ICPU, string, string>(cpu, cpu.GetLLVMTriple(0), GetModelInner);
            }
        }

        private static string GetModelInner(ICPU cpu, string triple)
        {
            var model = cpu.Model.ToLower();

            var pointerToFeaturesStringPointer = IntPtr.Zero;
            var featuresStringPointer = IntPtr.Zero;
            string[] supportedFeatures;
            try
            {
                pointerToFeaturesStringPointer = Marshal.AllocHGlobal(IntPtr.Size);
                llvm_disasm_get_cpu_features(triple, pointerToFeaturesStringPointer);
                featuresStringPointer = Marshal.ReadIntPtr(pointerToFeaturesStringPointer);

                if(featuresStringPointer == IntPtr.Zero)
                {
                    throw new RecoverableException(
                        "Failed to set up LLVM engine for assembling or disassembling; " +
                        $"could not extract supported features for {triple} CPUs"
                    );
                }

                supportedFeatures = Marshal.PtrToStringAnsi(featuresStringPointer).Split(';');
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToFeaturesStringPointer);
                Marshal.FreeHGlobal(featuresStringPointer);
            }

            // Start with 4 letters of the base architecture (either rv32 or rv64)
            var supportedModel = model.Remove(4);
            // Take the model, skip 4 letter of the base architecture,
            // split it for cases like `rv64imac_zicsr_zifencei`.
            var shortFeatures = model.Remove(0, 4).Split('_').First().Select(c => c.ToString());
            var longFeatures = model.Split('_').Skip(1);
            var features = shortFeatures.Concat(longFeatures);

            foreach(var feature in features)
            {
                if(supportedFeatures.Contains(feature))
                {
                    if(longFeatures.Contains(feature))
                    {
                        supportedModel += "_" + feature;
                    }
                    else
                    {
                        supportedModel += feature;
                    }
                }
                else
                {
                    cpu.InfoLog(
                        "Skipping RISC-V extension unsupported by LLVM during assembler or disassembler setup: {0}",
                        feature
                    );
                }
            }
            return supportedModel;
        }

        private static readonly SimpleCache modelsCache = new SimpleCache();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_get_cpu_features(string tripleName, IntPtr features);
    }
}
