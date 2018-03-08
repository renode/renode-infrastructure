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
    public partial class RiscV64 : BaseRiscV
    {
        public RiscV64(string cpuType, long frequency, Machine machine, PrivilegeMode privilegeMode = PrivilegeMode.Priv1_10, Endianess endianness = Endianess.LittleEndian) : base(cpuType, frequency, machine, privilegeMode, endianness, CpuBitness.Bits64)
        {
            intTypeToVal.Add(0, IrqType.SupervisorTimerIrq);
            intTypeToVal.Add(1, IrqType.SupervisorExternalIrq);
            intTypeToVal.Add(2, IrqType.SupervisorSoftwareInterrupt);
        }

        public override string Architecture { get { return "riscv64"; } }
    }
}
