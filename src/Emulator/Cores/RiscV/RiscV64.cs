//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if !PLATFORM_WINDOWS
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 3)]
    public partial class RiscV64 : BaseRiscV
    {
        public RiscV64(PlatformLevelInterruptController plic, CoreLevelInterruptor clint, string cpuType, Machine machine, uint hartId = 0, PrivilegeMode privilegeMode = PrivilegeMode.Priv1_10, Endianess endianness = Endianess.LittleEndian) : base(plic, clint, hartId, cpuType, machine, privilegeMode, endianness, CpuBitness.Bits64)
        {
        }

        public override string Architecture { get { return "riscv64"; } }
    }
}
#endif
