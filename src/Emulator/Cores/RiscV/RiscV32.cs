//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 3)]
    public partial class RiscV32 : BaseRiscV
    {
        public RiscV32(string cpuType, long frequency, Machine machine, PrivilegeMode privilegeMode = PrivilegeMode.Priv1_10, Endianess endianness = Endianess.LittleEndian) : base(cpuType, frequency, machine, privilegeMode, endianness, CpuBitness.Bits32)
        {
        }

        public override string Architecture { get { return "riscv"; } }
    }
}

