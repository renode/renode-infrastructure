//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 1)]
    public partial class X86_64 : BaseX86
    {
        public X86_64(string cpuType, IMachine machine, LAPIC lapic) : base(cpuType, machine, lapic, CpuBitness.Bits64)
        {
        }

        public override string GetLLVMTriple(uint flags) => AllLLVMTriples[0];

        public override string Architecture { get { return "x86_64"; } }

        public override string GDBArchitecture { get { return "i386:x86-64"; } }

        public override string[] AllLLVMTriples => new[] { "x86_64" };

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.i386.core");

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RAX, 64, "rax", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RCX, 64, "rcx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RDX, 64, "rdx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RBX, 64, "rbx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RSP, 64, "rsp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RBP, 64, "rbp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RSI, 64, "rsi", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RDI, 64, "rdi", "uint64"));

                for(var index = 8u; index <= 15; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.R8 + index - 8, 64, $"r{index}", "int64"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.RIP, 64, "rip", "code_ptr"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.EFLAGS, 64, "eflags", "x86_64_eflags"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CS, 64, "cs", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.SS, 64, "ss", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.DS, 64, "ds", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.ES, 64, "es", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.FS, 64, "fs", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.GS, 64, "gs", "uint64"));

                for(var index = 0u; index <= 7; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.ST0 + index, 80, $"st{index}", "i387_ext"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CR0, 64, $"cr0", "x86_64_cr0"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CR1, 64, $"cr1", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CR2, 64, $"cr2", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CR3, 64, $"cr3", "x86_64_cr3"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64Registers.CR4, 64, $"cr4", "x86_64_cr4"));

                var regnum = (uint)X86_64Registers.CR4 + 1;
                foreach(var float_reg in new[] { "fctrl", "fstat", "ftag", "fiseg", "fioff", "foseg", "fooff", "fop" })
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor(regnum++, 32, float_reg, "int", "float"));
                }

                coreFeature.Types.Add(GDBCustomType.Flags("x86_64_eflags", 8, X86_64GDBFields.EflagsFlags));
                coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr0", 8, X86_64GDBFields.Cr0Flags));
                coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr3", 8, X86_64GDBFields.Cr3Flags));
                coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr4", 8, X86_64GDBFields.Cr4Flags));
                coreFeature.Types.Add(GDBCustomType.Flags("x86_64_efer", 8, X86_64GDBFields.EferFlags));

                features.Add(coreFeature);

                return features;
            }
        }
    }
}