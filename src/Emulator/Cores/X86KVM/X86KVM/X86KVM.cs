//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

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