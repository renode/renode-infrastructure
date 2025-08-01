//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class LLVMArchitectureMapping
    {
        public static bool IsSupported(ICPU cpu)
        {
            return SupportedArchitectures.ContainsKey(cpu.Architecture);
        }

        public static void GetTripleAndModelKey(ICPU cpu, ref uint flags, out string triple, out string model)
        {
            triple = SupportedArchitectures[cpu.Architecture];
            if(triple == "armv7a" && flags > 0)
            {
                // For armv7a the flags are only 1 bit: 0 = ARM, 1 = thumb
                triple = "thumb";
                // The flags as passed to the disassembler are a different sort of flags than the parameter
                // of this function - the parameter contains disassembly flags reflecting the current state
                // of the CPU (see CurrentBlockDisassemblyFlags), while the disassembler flags select which
                // assembly dialect to use. Clear them out here, because the ARM disassembler only supports
                // dialect 0 (see LLVM's createARMMCInstPrinter). Actually, this would not cause any issues
                // because LLVM's C API detects this and ignores the alternate dialect flag, but let's pass
                // a proper value in the first place.
                flags = 0;
            }

            if(triple == "arm64")
            {
                // For arm64 there are two flags: bit[0] means Thumb and bit[1] means AArch32.
                // The valid values are 00, 10, and 11 (no 64-bit Thumb).

                // ARMv8 defines AArch32 and AArch64 execution modes, but not every ARMv8 processor
                // supports both. `cpu.Architecture` is used to load the correct tlib version, so we need to
                // special-case Cortex-R52 which only supports AArch32 so that the correct arguments are
                // passed to LLVM.
                // This occurs for example in AssembleBlock, which has a default flags = 0
                if(cpu.Model == "cortex-r52")
                {
                    flags |= 0b10; // Set AArch32 bit
                }

                if(flags == 0b10)
                {
                    triple = "armv7a";
                }
                else if(flags == 0b11)
                {
                    triple = "thumb";
                }
                // The same logic about not passing these through to LLVM applies.
                flags = 0;
            }

            if(!ModelTranslations.TryGetValue(cpu.Model, out model))
            {
                if(triple == "riscv32" || triple == "riscv64")
                {
                    // Cache is not only to improve performance but also to log unsupported extensions once.
                    model = riscvModelsCache.Get<ICPU, string, string>(cpu, triple, GetRiscVCompatibleModel);
                }
                else
                {
                    model = cpu.Model.ToLower();
                }
            }
        }

        private static string GetRiscVCompatibleModel(ICPU cpu, string triple)
        {
            var model = cpu.Model.ToLower();

            if(!model.StartsWith("rv32") && !model.StartsWith("rv64"))
            {
                throw new RecoverableException(
                    "Failed to set up LLVM engine for assembling or disassembling; " +
                    "only RISC-V models starting with either rv32 or rv64 are supported, " +
                    $"model of '{cpu}' CPU is unsupported: {model}"
                );
            }

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

        private static readonly SimpleCache riscvModelsCache = new SimpleCache();

        private static readonly Dictionary<string, string> SupportedArchitectures = new Dictionary<string, string>
        {
            { "arm",    "armv7a"    },
            { "arm-m",  "thumb"     },
            { "arm64",  "arm64"     },
            { "mips",   "mipsel"    },
            { "riscv",  "riscv32"   },
            { "riscv64","riscv64"   },
            { "ppc",    "ppc"       },
            { "ppc64",  "ppc64le"   },
            { "sparc",  "sparc"     },
            { "i386",   "i386"      },
            { "x86_64", "x86_64"    },
            { "msp430", "msp430"    },
            { "msp430x","msp430"    }
        };

        private static readonly Dictionary<string, string> ModelTranslations = new Dictionary<string, string>
        {
            { "x86"       , "i386"       },
            { "x86_64"    , "x86-64"     },
            // this case is included because of #3250
            { "arm926"    , "arm926ej-s" },
            // see: https://reviews.llvm.org/D12692
            { "cortex-m4f", "cortex-m4"  },
            { "cortex-r5f", "cortex-r5"  },
            { "e200z6"    , "ppc32"      },
            { "gr716"     , "leon3"      }
        };

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_get_cpu_features(string tripleName, IntPtr features);
    }
}
