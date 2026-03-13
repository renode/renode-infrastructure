//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86_64KVM : X86KVMBase
    {
        public X86_64KVM(string cpuType, IMachine machine, uint cpuId = 0)
            : base(cpuType, machine, CpuBitness.Bits64, cpuId)
        {
        }

        public override string Architecture => "x86_64";

        public override string GDBArchitecture => "i386:x86-64";

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();
                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.i386.core");

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RAX, 64, "rax", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RCX, 64, "rcx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RDX, 64, "rdx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RBX, 64, "rbx", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RSP, 64, "rsp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RBP, 64, "rbp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RSI, 64, "rsi", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RDI, 64, "rdi", "uint64"));

                for(var index = 8u; index <= 15; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.R8 + index - 8, 64, $"r{index}", "int64"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.RIP, 64, "rip", "code_ptr"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.EFLAGS, 64, "eflags", "x86_64_eflags"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CS, 64, "cs", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.SS, 64, "ss", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.DS, 64, "ds", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.ES, 64, "es", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.FS, 64, "fs", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.GS, 64, "gs", "uint64"));

                for(var index = 0u; index <= 7; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.ST0 + index, 80, $"st{index}", "i387_ext"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR0, 64, $"cr0", "x86_64_cr0"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR1, 64, $"cr1", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR2, 64, $"cr2", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR3, 64, $"cr3", "x86_64_cr3"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR4, 64, $"cr4", "x86_64_cr4"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.CR8, 64, $"cr8", "uint64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86_64KVMRegisters.EFER, 64, $"efer", "x86_64_efer"));

                var regnum = (uint)X86_64KVMRegisters.EFER + 1;
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