//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class ARMv7A : Arm, IARMSingleSecurityStateCPU, IPeripheralRegister<ARM_GenericTimer, NullRegistrationPoint>
    {
        public ARMv7A(IMachine machine, string cpuType, uint cpuId = 0, ARM_GenericInterruptController genericInterruptController = null, Endianess endianness = Endianess.LittleEndian)
            : base(cpuType, machine, cpuId, endianness)
        {
            Affinity = new Affinity(cpuId);
            try
            {
                genericInterruptController?.AttachCPU(this);
            }
            catch(Exception e)
            {
                throw new ConstructionException($"Failed to attach CPU to Generic Interrupt Controller: {e.Message}", e);
            }
        }

        public void Register(ARM_GenericTimer peripheral, NullRegistrationPoint registrationPoint)
        {
            if(genericTimer != null)
            {
                throw new RegistrationException("A generic timer is already registered.");
            }
            genericTimer = peripheral;
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(ARM_GenericTimer peripheral)
        {
            genericTimer = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public override MemorySystemArchitectureType MemorySystemArchitecture => MemorySystemArchitectureType.Virtual_VMSA;

        // Currently unsupported
        public bool FIQMaskOverride => false;
        public bool IRQMaskOverride => false;

        public Affinity Affinity { get; }
        public SecurityState SecurityState => SecurityState.Secure;
        public ExceptionLevel ExceptionLevel => ExceptionLevel.EL1_SystemMode;

        protected override void Write32CP15Inner(Coprocessor32BitMoveInstruction instruction, uint value)
        {
            if(instruction.CRn == GenericTimerCoprocessorRegister)
            {
                if(genericTimer != null)
                {
                    genericTimer.WriteDoubleWordRegisterAArch32(instruction.FieldsOnly, value);
                    return;
                }
                this.Log(LogLevel.Error, "Trying to write the register of a generic timer, by the CP15 32-bit write instruction ({0}), but a timer was not found.", instruction);
                return;
            }
            base.Write32CP15Inner(instruction, value);
        }

        protected override uint Read32CP15Inner(Coprocessor32BitMoveInstruction instruction)
        {
            if(instruction.CRn == GenericTimerCoprocessorRegister)
            {
                if(genericTimer != null)
                {
                    return genericTimer.ReadDoubleWordRegisterAArch32(instruction.FieldsOnly);
                }
                this.Log(LogLevel.Error, "Trying to read the register of a generic timer, by the CP15 32-bit read instruction ({0}), but a timer was not found - returning 0x0.", instruction);
                return 0;
            }
            return base.Read32CP15Inner(instruction);
        }

        protected override void Write64CP15Inner(Coprocessor64BitMoveInstruction instruction, ulong value)
        {
            if(instruction.CRm == GenericTimerCoprocessorRegister)
            {
                if(genericTimer != null)
                {
                    genericTimer.WriteQuadWordRegisterAArch32(instruction.FieldsOnly, value);
                    return;
                }
                this.Log(LogLevel.Error, "Trying to write the register of a generic timer, by the CP15 64-bit write instruction ({0}), but a timer was not found.", instruction);
                return;
            }
            base.Write64CP15Inner(instruction, value);
        }

        protected override ulong Read64CP15Inner(Coprocessor64BitMoveInstruction instruction)
        {
            if(instruction.CRm == GenericTimerCoprocessorRegister)
            {
                if(genericTimer != null)
                {
                    return genericTimer.ReadQuadWordRegisterAArch32(instruction.FieldsOnly);
                }
                this.Log(LogLevel.Error, "Trying to read the register of a generic timer, by the CP15 64-bit read instruction ({0}), but a timer was not found - returning 0x0.", instruction);
                return 0;
            }
            return base.Read64CP15Inner(instruction);
        }

        protected ARM_GenericTimer genericTimer;

        private const uint GenericTimerCoprocessorRegister = 14;
    }
}
