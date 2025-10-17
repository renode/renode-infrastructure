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
    public partial class RiscV32 : BaseRiscV
    {
        public RiscV32(
            IMachine machine,
            string cpuType,
            IRiscVTimeProvider timeProvider = null,
            uint hartId = 0,
            [NameAlias("privilegeArchitecture")] PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_11,
            Endianess endianness = Endianess.LittleEndian,
            ulong? nmiVectorAddress = null,
            uint? nmiVectorLength = null,
            bool allowUnalignedAccesses = false,
            uint pmpNumberOfAddrBits = 32,
            InterruptMode interruptMode = InterruptMode.Auto,
            PrivilegeLevels privilegeLevels = PrivilegeLevels.MachineSupervisorUser
        )
            : base(timeProvider, hartId, cpuType, machine, privilegedArchitecture, endianness, CpuBitness.Bits32, nmiVectorAddress, nmiVectorLength,
                    allowUnalignedAccesses, interruptMode, privilegeLevels: privilegeLevels, pmpNumberOfAddrBits: pmpNumberOfAddrBits)
        {
        }

        public override string Architecture { get { return "riscv"; } }

        public override string GDBArchitecture { get { return "riscv:rv32"; } }

        public override RegisterValue VLEN => VLENB * 8u;

        protected override byte MostSignificantBit => 31;

        private uint BeforePCWrite(uint value)
        {
            PCWritten();
            return value;
        }

        public enum MstatusFieldOffsets : byte
        {
            SD = 31,
            SDT = 24,
            SPELP = 23,
            TSR = 22,
            TW = 21,
            TVM = 20,
            MXR = 19,
            SUM = 18,
            MPRV = 17,
            XS = 15,
            FS = 13,
            MPP = 11,
            VS = 9,
            SPP = 8,
            MPIE = 7,
            UBE = 6,
            SPIE = 5,
            MIE = 3,
            SIE = 1,
        }
    }
}