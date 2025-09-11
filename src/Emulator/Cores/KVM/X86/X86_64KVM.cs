//
// Copyright (c) 2010-2025 Antmicro
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

                {
                    var eflags_flags = new List<GDBTypeBitField>();
                    eflags_flags.Add(new GDBTypeBitField("CF", 0, 0, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("", 1, 1, "priv_type")); // Reserved
                    eflags_flags.Add(new GDBTypeBitField("PF", 2, 2, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("", 3, 3, "bool")); // Reserved
                    eflags_flags.Add(new GDBTypeBitField("AF", 4, 4, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("", 5, 5, "priv_type")); // Reserved
                    eflags_flags.Add(new GDBTypeBitField("ZF", 6, 6, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("SF", 7, 7, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("TF", 8, 8, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("IF", 9, 9, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("DF", 10, 10, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("OF", 11, 11, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("IOPL", 12, 13, "uint8"));
                    eflags_flags.Add(new GDBTypeBitField("NT", 14, 14, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("", 15, 15, "priv_type")); // Reserved
                    eflags_flags.Add(new GDBTypeBitField("RF", 16, 16, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("VM", 17, 17, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("AC", 18, 18, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("VIF", 19, 19, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("VIP", 20, 20, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("ID", 21, 21, "bool"));
                    eflags_flags.Add(new GDBTypeBitField("", 22, 63, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_64_eflags", 8, eflags_flags));
                }
                {
                    var cr0_flags = new List<GDBTypeBitField>();
                    cr0_flags.Add(new GDBTypeBitField("PE", 0, 0, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("MP", 1, 1, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("EM", 2, 2, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("TS", 3, 3, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("ET", 4, 4, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("NE", 5, 5, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("", 6, 15, "priv_type")); // Reserved
                    cr0_flags.Add(new GDBTypeBitField("WP", 16, 16, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("", 17, 17, "priv_type")); // Reserved
                    cr0_flags.Add(new GDBTypeBitField("AM", 18, 18, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("", 19, 28, "priv_type")); // Reserved
                    cr0_flags.Add(new GDBTypeBitField("NW", 29, 29, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("CD", 30, 30, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("PG", 31, 31, "bool"));
                    cr0_flags.Add(new GDBTypeBitField("", 32, 63, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr0", 8, cr0_flags));
                }
                {
                    var cr3_flags = new List<GDBTypeBitField>();
                    cr3_flags.Add(new GDBTypeBitField("PCID", 0, 11, "uint16"));
                    cr3_flags.Add(new GDBTypeBitField("PDBR", 12, 63, "uin64"));
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr3", 8, cr3_flags));
                }
                {
                    var cr4_flags = new List<GDBTypeBitField>();
                    cr4_flags.Add(new GDBTypeBitField("VME", 0, 0, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PVI", 1, 1, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("TSD", 2, 2, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("DE", 3, 3, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PSE", 4, 4, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PAE", 5, 5, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("MCE", 6, 6, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PGE", 7, 7, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PCE", 8, 8, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("OSFXSR", 9, 9, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("OSXMMEXCEPT", 10, 10, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("UMIP", 11, 11, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("LA57", 12, 12, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("VMXE", 13, 13, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("SMXE", 14, 14, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("", 15, 15, "priv_type")); // Reserved
                    cr4_flags.Add(new GDBTypeBitField("FSGSBASE", 16, 16, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PCIDE", 17, 17, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("OSXSAVE", 18, 18, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("SMEP", 20, 20, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("SMAP", 21, 21, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PKE", 22, 22, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("CET", 23, 23, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("PKS", 24, 24, "bool"));
                    cr4_flags.Add(new GDBTypeBitField("", 25, 63, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_64_cr4", 8, cr4_flags));
                }
                {
                    var efer_flags = new List<GDBTypeBitField>();
                    efer_flags.Add(new GDBTypeBitField("SCE", 0, 0, "bool"));
                    efer_flags.Add(new GDBTypeBitField("", 1, 7, "priv_type")); // Reserved
                    efer_flags.Add(new GDBTypeBitField("LME", 8, 8, "bool"));
                    efer_flags.Add(new GDBTypeBitField("", 9, 9, "priv_type")); // Reserved
                    efer_flags.Add(new GDBTypeBitField("LMA", 10, 10, "bool"));
                    efer_flags.Add(new GDBTypeBitField("NXE", 11, 11, "bool"));
                    efer_flags.Add(new GDBTypeBitField("SVME", 12, 12, "bool"));
                    efer_flags.Add(new GDBTypeBitField("LMSLE", 13, 13, "bool"));
                    efer_flags.Add(new GDBTypeBitField("FFXSR", 14, 14, "bool"));
                    efer_flags.Add(new GDBTypeBitField("TCE", 15, 15, "bool"));
                    efer_flags.Add(new GDBTypeBitField("", 16, 63, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_64_efer", 8, efer_flags));
                }

                features.Add(coreFeature);

                return features;
            }
        }
    }
}