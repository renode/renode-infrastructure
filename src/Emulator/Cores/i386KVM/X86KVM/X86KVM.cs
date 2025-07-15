//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86KVM : X86KVMBase
    {
        public X86KVM(string cpuType, IMachine machine, uint cpuId = 0)
            : base(cpuType, machine, CpuBitness.Bits32, cpuId)
        {
        }

        public override string Architecture => "i386";
    }
}
