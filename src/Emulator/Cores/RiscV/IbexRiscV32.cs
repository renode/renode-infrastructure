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
        public IbexRiscV32(Machine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, PrivilegeArchitecture privilegeArchitecture = PrivilegeArchitecture.Priv1_11, Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imcu") : base(timeProvider, cpuType, machine, hartId, privilegeArchitecture, endianness)
        {
            MTVEC = 0x00000001; // Ibex is always in interrupt vector mode
            CSRValidation = CSRValidationLevel.None;

            RegisterCSR((ulong)0x305, () => {  // Overload the MTVEC CSR
                                return MTVEC | (ulong)0x1;
                        }, value => {
                                MTVEC = value | 0x1; // Force interrupt vector mode
                        });
        }
    }
}