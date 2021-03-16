//
// Copyright (c) 2010-2021 Antmicro
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
        public IbexRiscV32(Machine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, PrivilegeArchitecture privilegeArchitecture = PrivilegeArchitecture.Priv1_11, Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imcu") : base(timeProvider, cpuType, machine, hartId, privilegeArchitecture, endianness, interruptMode: InterruptMode.Vectored)
        {
        }
    }
}
