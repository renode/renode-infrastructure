//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class ARMv7R : Arm, IARMSingleSecurityStateCPU
    {
        public ARMv7R(IMachine machine, string cpuType, uint cpuId = 0, ARM_GenericInterruptController genericInterruptController = null, Endianess endianness = Endianess.LittleEndian, uint? numberOfMPURegions = null)
            : base(cpuType, machine, cpuId, endianness, numberOfMPURegions)
        {
            try
            {
                genericInterruptController?.AttachCPU(this);
            }
            catch(Exception e)
            {
                throw new ConstructionException("Failed to attach CPU to Generic Interrupt Controller", e);
            }
        }

        public byte Affinity0 => (byte)Id;
        public SecurityState SecurityState => SecurityState.Secure;
        public uint AuxiliaryControlRegister { get; set; }

        protected override void Write32CP15Inner(Coprocessor32BitMoveInstruction instruction, uint value)
        {
            if(instruction == AuxiliaryControlRegisterInstruction)
            {
                AuxiliaryControlRegister = value;
                return;
            }
            base.Write32CP15Inner(instruction, value);
        }

        protected override uint Read32CP15Inner(Coprocessor32BitMoveInstruction instruction)
        {
            if(instruction == AuxiliaryControlRegisterInstruction)
            {
                return AuxiliaryControlRegister;
            }
            return base.Read32CP15Inner(instruction);
        }

        private readonly Coprocessor32BitMoveInstruction AuxiliaryControlRegisterInstruction = new Coprocessor32BitMoveInstruction(0, 1, 0, 1); // ACTLR
    }
}
