//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class ARMv7R : Arm, IARMSingleSecurityStateCPU
    {
        public ARMv7R(IMachine machine, string cpuType, uint cpuId = 0, ARM_GenericInterruptController genericInterruptController = null, Endianess endianness = Endianess.LittleEndian,
                      uint? numberOfMPURegions = null, ArmSignalsUnit signalsUnit = null)
            : base(cpuType, machine, cpuId, endianness, numberOfMPURegions, signalsUnit)
        {
            Affinity = new Affinity(cpuId);
            try
            {
                genericInterruptController?.AttachCPU(this);
            }
            catch(Exception e)
            {
                // Free unmanaged resources allocated by the base class constructor
                Dispose();
                throw new ConstructionException($"Failed to attach CPU to Generic Interrupt Controller: {e.Message}", e);
            }
        }

        public override MemorySystemArchitectureType MemorySystemArchitecture => MemorySystemArchitectureType.Physical_PMSA;

        // Currently unsupported
        public bool FIQMaskOverride => false;
        public bool IRQMaskOverride => false;

        public Affinity Affinity { get; }
        public SecurityState SecurityState => SecurityState.Secure;
        public ExceptionLevel ExceptionLevel => ExceptionLevel.EL1_SystemMode;
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
