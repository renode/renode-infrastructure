//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU.Disassembler;

namespace Antmicro.Renode.Disassembler.LLVM
{
    [DisassemblerAttribute("LLVM", new[] { "arm", "arm-m", "mips", "i386", "riscv", "riscv64", "ppc", "ppc64", "sparc" })]
    public class LLVMDisassembler : IAutoLoadType, IDisassembler
    {
        public LLVMDisassembler(IDisassemblable cpu)
        {
            if(!SupportedArchitectures.ContainsKey(cpu.Architecture))
            {
                throw new ArgumentOutOfRangeException("cpu");
            }

            this.cpu = cpu;
            cache = new Dictionary<string, LLVMDisasWrapper>();

            Disassemble = cpu.Architecture == "arm-m" ? CortexMAddressTranslator.Wrap(LLVMDisassemble) : LLVMDisassemble;
        }

        public string Name { get { return "LLVM"; } }
        public DisassemblyProvider Disassemble { get; private set; }

        private int LLVMDisassemble(ulong pc, IntPtr memory, ulong size, uint flags, IntPtr output, ulong outputSize)
        {
            var triple = SupportedArchitectures[cpu.Architecture];
            if(triple == "armv7a" && flags > 0)
            {
                triple = "thumb";
            }

            var key = string.Format("{0} {1}", triple, cpu.Model);
            if(!cache.ContainsKey(key))
            {
                string model;
                switch(cpu.Model)
                {
                case "x86":
                    model = "i386";
                    break;
                // this case is included because of #3250
                case "arm926":
                    model = "arm926ej-s";
                    break;
                case "e200z6":
                    model = "ppc32";
                    break;
                default:
                    model = cpu.Model;
                    break;
                }

                cache.Add(key, new LLVMDisasWrapper(model, triple));
            }

            return cache[key].Disassemble(memory, (ulong)size, pc, output, (uint)outputSize);
        }

        private static readonly Dictionary<string, string> SupportedArchitectures = new Dictionary<string, string>
        {
            { "arm",    "armv7a"    },
            { "arm-m",  "thumb"     },
            { "mips",   "mipsel"    },
            { "riscv",  "riscv32"   },
            { "riscv64","riscv64"   },
            { "ppc",    "ppc"       },
            { "ppc64",  "ppc64le"   },
            { "sparc",  "sparc"     },
            { "i386",   "i386"      }
        };

        private readonly Dictionary<string, LLVMDisasWrapper> cache;
        private readonly IDisassemblable cpu;
    }
}
