//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.Binding;

using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class X86KVMBase : KVMCPU
    {
        // field in the 'flags' variable are defined in
        // Intel(R) 64 and IA-32 Architectures Software Developer’s Manual - Volume 3 (3.4.5)
        public void SetDescriptor(SegmentDescriptor descriptor, ushort selector, ulong baseAddress, uint limit, uint flags)
        {
            switch(descriptor)
            {
            case SegmentDescriptor.CS:
                KvmSetCsDescriptor(baseAddress, limit, selector, flags);
                break;
            case SegmentDescriptor.DS:
                KvmSetDsDescriptor(baseAddress, limit, selector, flags);
                break;
            case SegmentDescriptor.ES:
                KvmSetEsDescriptor(baseAddress, limit, selector, flags);
                break;
            case SegmentDescriptor.SS:
                KvmSetSsDescriptor(baseAddress, limit, selector, flags);
                break;
            case SegmentDescriptor.FS:
                KvmSetFsDescriptor(baseAddress, limit, selector, flags);
                break;
            case SegmentDescriptor.GS:
                KvmSetGsDescriptor(baseAddress, limit, selector, flags);
                break;
            default:
                throw new RecoverableException($"Setting the {descriptor} descriptor is not implemented, ignoring");
            }
        }

        protected X86KVMBase(string cpuType, IMachine machine, CpuBitness cpuBitness, uint cpuId = 0)
            : base(cpuType, machine, Endianess.LittleEndian, cpuBitness, cpuId)
        {
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetCsDescriptor;

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetDsDescriptor;

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetEsDescriptor;

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetSsDescriptor;

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetFsDescriptor;

        [Import]
        protected Action<ulong, uint, ushort, uint> KvmSetGsDescriptor;

#pragma warning restore 649

        public enum SegmentDescriptor
        {
            CS,
            SS,
            DS,
            ES,
            FS,
            GS
        }
    }
}