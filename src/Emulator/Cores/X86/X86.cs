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
    public partial class X86 : BaseX86
    {
        public X86(string cpuType, IMachine machine, LAPIC lapic): base(cpuType, machine, lapic, CpuBitness.Bits32)
        {
        }

        public override string Architecture { get { return "i386"; } }

        public override string GDBArchitecture { get { return Architecture; } }

        // When no register features are passed, GDB will assume a default register layout, selected based on the architecture.
        // Such layout is enough to make our stub implementation working.
        public override List<GDBFeatureDescriptor> GDBFeatures { get { return new List<GDBFeatureDescriptor>(); } }

    }
}
