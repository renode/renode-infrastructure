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

        // When no register features are passed, GDB will assume a default register layout, selected based on the architecture.
        // Such layout is enough to make our stub implementation working.
        public override List<GDBFeatureDescriptor> GDBFeatures => new List<GDBFeatureDescriptor>();
    }
}