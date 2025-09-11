//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86KVM : X86KVMBase
    {
        public X86KVM(string cpuType, IMachine machine, uint cpuId = 0, Detected64BitBehaviour on64BitDetected = Detected64BitBehaviour.Warn)
            : base(cpuType, machine, CpuBitness.Bits32, cpuId)
        {
            KvmSet64BitBehaviour((uint)on64BitDetected);
        }

        public override string Architecture => "x86";

        public override string GDBArchitecture => "i386";

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();
                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.i386.core");

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EAX, 32, "eax", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.ECX, 32, "ecx", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EDX, 32, "edx", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EBX, 32, "ebx", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.ESP, 32, "esp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EBP, 32, "ebp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.ESI, 32, "esi", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EDI, 32, "edi", "uint32"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EIP, 32, "eip", "code_ptr"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.EFLAGS, 32, "eflags", "x86_eflags"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CS, 32, "cs", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.SS, 32, "ss", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.DS, 32, "ds", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.ES, 32, "es", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.FS, 32, "fs", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.GS, 32, "gs", "uint32"));

                for(var index = 0u; index <= 7; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.ST0 + index, 80, $"st{index}", "i387_ext"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CR0, 32, $"cr0", "x86_cr0"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CR1, 32, $"cr1", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CR2, 32, $"cr2", "uint32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CR3, 32, $"cr3", "x86_cr3"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)X86KVMRegisters.CR4, 32, $"cr4", "x86_cr4"));

                var regnum = (uint)X86KVMRegisters.CR4 + 1;
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
                    eflags_flags.Add(new GDBTypeBitField("", 22, 31, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_eflags", 4, eflags_flags));
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
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_cr0", 4, cr0_flags));
                }
                {
                    var cr3_flags = new List<GDBTypeBitField>();
                    cr3_flags.Add(new GDBTypeBitField("PCID", 0, 11, "uint16"));
                    cr3_flags.Add(new GDBTypeBitField("PDBR", 12, 31, "uin64"));
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_cr3", 4, cr3_flags));
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
                    cr4_flags.Add(new GDBTypeBitField("", 25, 31, "priv_type")); // Reserved
                    coreFeature.Types.Add(GDBCustomType.Flags("x86_cr4", 4, cr4_flags));
                }

                features.Add(coreFeature);

                return features;
            }
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        protected Action<uint> KvmSet64BitBehaviour;

#pragma warning restore 649

        public enum Detected64BitBehaviour
        {
            Fault = 0,
            Warn = 1,
            Ignore = 2,
        }
    }
}