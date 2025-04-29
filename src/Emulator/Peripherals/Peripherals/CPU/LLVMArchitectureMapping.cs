//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;

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
                model = cpu.Model.ToLower();
            }

            if(model == "cortex-r52")
            {
                triple = "arm";
            }

            // RISC-V extensions Zicsr and Zifencei are not supported in LLVM yet:
            // https://discourse.llvm.org/t/support-for-zicsr-and-zifencei-extensions/68369
            // https://reviews.llvm.org/D143924
            // The LLVM version used by Renode (at the time of adding this logic) is 14.0.0-rc1
            if(model.Contains("_zicsr"))
            {
                model = model.Replace("_zicsr", "");
            }

            if(model.Contains("_zifencei"))
            {
                model = model.Replace("_zifencei", "");
            }
        }

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
    }
}
