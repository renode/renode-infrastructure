//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Endianess = ELFSharp.ELF.Endianess;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 1)]
    public partial class X86_64 : BaseX86
    {
        public X86_64(string cpuType, IMachine machine, LAPIC lapic): base(cpuType, machine, lapic, CpuBitness.Bits64)
        {
        }

        public override string Architecture { get { return "x86_64"; } }

        public override string GDBArchitecture { get { return "i386:x86-64"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.i386.core");
                coreFeature.Registers.Add(new GDBRegisterDescriptor(0, 64, "rax", "int64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(1, 64, "rcx", "int64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(2, 64, "rdx", "int64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(3, 64, "rbx", "int64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(4, 64, "rsp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(5, 64, "rbp", "data_ptr"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(6, 64, "rsi", "int64"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(7, 64, "rdi", "int64"));
                for(var index = 8u; index <= 15; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor(index, 64, $"r{index}", "int64"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor(16, 64, "rip", "code_ptr"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor(17, 32, "eflags"));

                coreFeature.Registers.Add(new GDBRegisterDescriptor(18, 32, "cs", "int32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(19, 32, "ss", "int32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(20, 32, "ds", "int32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(21, 32, "es", "int32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(22, 32, "fs", "int32"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(23, 32, "gs", "int32"));

                for(var index = 0u; index <= 7; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor(24 + index, 80, $"st{index}", "i387_ext"));
                }

                coreFeature.Registers.Add(new GDBRegisterDescriptor(32, 32, "fctrl", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(33, 32, "fstat", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(34, 32, "ftag", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(35, 32, "fiseg", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(36, 32, "fioff", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(37, 32, "foseg", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(38, 32, "fooff", "int", "float"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(39, 32, "fop", "int", "float"));

                features.Add(coreFeature);

                return features;
            }
        }
    }
}

