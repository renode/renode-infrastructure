//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class IbexRiscV32 : RiscV32
    {
        public IbexRiscV32(IMachine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, PrivilegeArchitecture privilegeArchitecture = PrivilegeArchitecture.Priv1_11, Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imcu_zicsr_zifencei", bool allowUnalignedAccesses = true, ulong? nmiVectorAddress = null, uint? nmiVectorLength = null) : base(timeProvider, cpuType, machine, hartId, privilegeArchitecture, endianness, allowUnalignedAccesses: allowUnalignedAccesses, interruptMode: InterruptMode.Vectored, nmiVectorAddress: nmiVectorAddress, nmiVectorLength: nmiVectorLength)
        {
            RegisterCustomCSRs();
        }

        private void RegisterCustomCSRs()
        {
            RegisterCSR((ulong)CSRs.CpuControl, () => 0ul, _ => {});
        }

        private enum CSRs
        {
            CpuControl = 0x7c0,
        }
    }
}
