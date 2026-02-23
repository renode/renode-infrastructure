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

        public static string GetTriple(ICPU cpu, uint flags)
        {
            if(cpu.Architecture == "arm")
            {
                if(flags == 1)
                {
                    return "thumb";
                }
                else
                {
                    return "armv7a";
                }
            }
            if(cpu.Architecture == "arm64")
            {
                if(cpu.Model == "cortex-r52")
                {
                    flags |= 0b10;
                }
                switch(flags)
                {
                case 0b10:
                    return "armv7a";
                case 0b11:
                    return "thumb";
                default:
                    return "arm64";
                }
            }
            return SupportedArchitectures[cpu.Architecture];
        }

        public static string GetModel(ICPU cpu)
        {
            if(ModelTranslations.TryGetValue(cpu.Model, out var model))
            {
                return model;
            }
            if(cpu.Architecture == "riscv" || cpu.Architecture == "riscv64")
            {
                // Cache is not only to improve performance but also to log unsupported extensions once.
                lock(riscvModelsCache)
                {
                    return riscvModelsCache.Get<ICPU, string, string>(cpu, SupportedArchitectures[cpu.Architecture], GetRiscVCompatibleModel);
                }
            }
            return cpu.Model.ToLower();
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
            { "msp430x","msp430"    },
            { "xtensa", "xtensa"    }
        };

        private static readonly Dictionary<string, string> ModelTranslations = new Dictionary<string, string>
        {
            { "x86"       , "i386"       },
            { "x86_64"    , "x86-64"     },
            // see: https://reviews.llvm.org/D12692
            { "cortex-m4f", "cortex-m4"  },
            { "cortex-r5f", "cortex-r5"  },
            { "e200z6"    , "ppc32"      },
            { "gr716"     , "leon3"      },
            // TODO: In the current version of LLVM (20.1.7), there is only experimental support for Xtensa,
            // with only the "generic" CPU available. The only supported Xtensa feature is `FeatureDensity`.
            // After the LLVM update, update `mocked_sample_controller` accordingly in renode-llvm-disas repo, based on:
            // https://github.com/antmicro/tlib/blob/78bcb71570e72d24c641c444db00c2dab4cda85e/arch/xtensa/core-sample_controller/core-isa.h
            { "sample_controller" , "mocked-sample-controller"}
        };

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_get_cpu_features(string tripleName, IntPtr features);
    }
}
