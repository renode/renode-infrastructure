//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 12)]
    public partial class RiscV32 : BaseRiscV
    {
        public RiscV32(CoreLevelInterruptor clint, string cpuType, Machine machine, uint hartId = 0, PrivilegeArchitecture privilegeArchitecture = PrivilegeArchitecture.Priv1_10, Endianess endianness = Endianess.LittleEndian) : base(clint, hartId, cpuType, machine, privilegeArchitecture, endianness, CpuBitness.Bits32)
        {
        }

        public override string Architecture { get { return "riscv"; } }

        public override string GDBArchitecture { get { return "riscv:rv32"; } }
    }
}

