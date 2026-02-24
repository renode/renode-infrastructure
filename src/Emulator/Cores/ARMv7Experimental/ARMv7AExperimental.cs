//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class ARMv7AExperimental : ARMv7Experimental, IARMSingleSecurityStateCPU, IRegisterablePeripheral<ARM_GenericTimer, NullRegistrationPoint>
    {
        public ARMv7AExperimental(IMachine machine, string cpuType, uint cpuId = 0, ARM_GenericInterruptController genericInterruptController = null, Endianess endianness = Endianess.LittleEndian)
            : base(cpuType, machine, cpuId, endianness)
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

        public override string GetLLVMTriple(uint flags)
        {
            return flags switch
            {
                0b10 => AllLLVMTriples[0], // AArch32 / ARM
                0b11 => AllLLVMTriples[1], // Thumb
                _ => throw new ArgumentOutOfRangeException(nameof(flags), $"Invalid flags value: {flags:b}. Expected values are 0b10 or 0b11")
            };
        }

        public void Register(ARM_GenericTimer peripheral, NullRegistrationPoint registrationPoint)
        {
            if(genericTimer != null)
            {
                throw new RegistrationException("A generic timer is already registered");
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

        // Currently unsupported, needed for IARMSingleSecurityStateCPU
        public bool FIQMaskOverride => false;

        public bool IRQMaskOverride => false;

        public Affinity Affinity { get; }

        public SecurityState SecurityState => SecurityState.Secure;

        public ExceptionLevel ExceptionLevel => ExceptionLevel.EL1_SystemMode;

        [Export]
        protected void OnTcmMappingUpdate(int index, ulong newAddress, uint el01Enabled, uint el2Enabled)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented. index={index} newAddress={newAddress} el01Enabled={el01Enabled} el2Enabled={el2Enabled}");
        }

        [Export]
        protected ulong ReadSystemRegisterInterruptCPUInterface(uint offset)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        [Export]
        protected void WriteSystemRegisterInterruptCPUInterface(uint offset, ulong value)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        [Export]
        protected uint ReadSystemRegisterGenericTimer32(uint _)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer32(uint _, uint __)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        [Export]
        protected ulong ReadSystemRegisterGenericTimer64(uint offset)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer64(uint offset, ulong value)
        {
            throw new CpuAbortException($"TODO: PC=${PC} TGAU: not implemented");
        }

        protected ARM_GenericTimer genericTimer;

        [Export]
        private void OnExecutionModeChanged(uint el, uint isSecure)
        {
            this.Log(LogLevel.Debug, $"TODO: PC=${PC} ARMv7A.OnExecutionModeChanged: TGAU: not implemented. NOP! args: el={el} isSecure={isSecure}");
        }
    }
}
