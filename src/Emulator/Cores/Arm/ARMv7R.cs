//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class ARMv7R : Arm, IARMSingleSecurityStateCPU
    {
        public ARMv7R(Machine machine, string cpuType, uint cpuId = 0, ARM_GenericInterruptController genericInterruptController = null, Endianess endianness = Endianess.LittleEndian)
            : base(cpuType, machine, cpuId, endianness)
        {
            genericInterruptController?.AttachCPU(cpuId, this);
        }

        public byte Affinity0 => (byte)Id;
        public SecurityState SecurityState => SecurityState.Secure;
    }
}
