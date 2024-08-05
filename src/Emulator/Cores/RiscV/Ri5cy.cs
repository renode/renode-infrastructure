//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Ri5cy : RiscV32
    {
        public Ri5cy(IMachine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, [NameAlias("privilegeArchitecture")] PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_10, Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imc_zicsr_zifencei") : base(machine, cpuType, timeProvider, hartId, privilegedArchitecture, endianness)
        {
            // enable all interrupt sources
            MIE = 0xffffffff;

            CSRValidation = CSRValidationLevel.None;

            // register custom CSRs
            // TODO: add support for HW loops
            RegisterCSR((ulong)0x7b0, () => 0u, _ => {}); //lpstart0
            RegisterCSR((ulong)0x7b1, () => 0u, _ => {}); //lpend1
            RegisterCSR((ulong)0x7b2, () => 0u, _ => {}); //lpcount0

            RegisterCSR((ulong)0x7b4, () => 0u, _ => {}); //lpstart1
            RegisterCSR((ulong)0x7b5, () => 0u, _ => {}); //lpend1
            RegisterCSR((ulong)0x7b6, () => 0u, _ => {}); //lpcount1
        }
    }
}

