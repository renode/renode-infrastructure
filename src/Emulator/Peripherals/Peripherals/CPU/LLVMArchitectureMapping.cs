//
// Copyright (c) 2010-2024 Antmicro
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

        public static void GetTripleAndModelKey(ICPU cpu, uint flags, out string triple, out string model)
        {
            triple = SupportedArchitectures[cpu.Architecture];
            if(triple == "armv7a" && flags > 0)
            {
                triple = "thumb";
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
            { "x86_64", "x86_64"    }
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
