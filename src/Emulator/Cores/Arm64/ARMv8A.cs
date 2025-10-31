//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

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
    public partial class ARMv8A : BaseARMv8, IARMTwoSecurityStatesCPU, IPeripheralRegister<ARM_GenericTimer, NullRegistrationPoint>
    {
        public ARMv8A(IMachine machine, string cpuType, ARM_GenericInterruptController genericInterruptController, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian)
                : base(cpuId, cpuType, machine, endianness)
        {
            Affinity = new Affinity(cpuId);
            gic = genericInterruptController;
            try
            {
                gic.AttachCPU(this);
            }
            catch(Exception e)
            {
                // Free unmanaged resources allocated by the base class constructor
                Dispose();
                throw new ConstructionException($"Failed to attach CPU to Generic Interrupt Controller: {e.Message}", e);
            }
            TlibSetGicCpuRegisterInterfaceVersion(gic.ArchitectureVersionAtLeast3 ? GICCPUInterfaceVersion.Version30Or40 : GICCPUInterfaceVersion.None);
            Reset();
            HasSingleSecurityState = TlibHasEl3() == 0;
        }

        public void GetAtomicExceptionLevelAndSecurityState(out ExceptionLevel exceptionLevel, out SecurityState securityState)
        {
            lock(elAndSecurityLock)
            {
                exceptionLevel = this.exceptionLevel;
                securityState = this.securityState;
            }
        }

        public void SetAvailableExceptionLevels(bool el2Enabled, bool el3Enabled)
        {
            if(started)
            {
                throw new RecoverableException("Available Exception Levels can only be set before starting the simulation.");
            }

            var returnValue = TlibSetAvailableEls(el2Enabled ? 1u : 0u, el3Enabled ? 1u : 0u);
            switch((SetAvailableElsReturnValue)returnValue)
            {
            case SetAvailableElsReturnValue.Success:
                HasSingleSecurityState = el3Enabled;
                return;
            case SetAvailableElsReturnValue.EL2OrEL3EnablingFailed:
                throw new RecoverableException($"The '{Model}' core doesn't support all the enabled Exception Levels.");
            // It should never be returned if 'started' is false.
            case SetAvailableElsReturnValue.SimulationAlreadyStarted:
            default:
                throw new ArgumentException("Invalid TlibSetAvailableEls return value!");
            }
        }

        public void Register(ARM_GenericTimer peripheral, NullRegistrationPoint registrationPoint)
        {
            if(timer != null)
            {
                throw new RegistrationException("A generic timer is already registered.");
            }
            timer = peripheral;
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(ARM_GenericTimer peripheral)
        {
            timer = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public override string Architecture { get { return "arm64"; } }

        public override string GDBArchitecture { get { return "aarch64"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.aarch64.core");
                for(var index = 0u; index <= 30; index++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor(index, 64, $"x{index}", "uint64", "general"));
                }
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8ARegisters.SP, 64, "sp", "data_ptr", "general"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8ARegisters.PC, 64, "pc", "code_ptr", "general"));
                // CPSR name is in line with GDB's 'G.5.1 AArch64 Features' manual page though it should be named PSTATE.
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8ARegisters.PSTATE, 32, "cpsr", "uint32", "general"));
                features.Add(coreFeature);

                AddSystemRegistersFeature(features, "org.renode.gdb.aarch64.sysregs");

                /*
                 * TODO
                 * The ‘org.gnu.gdb.aarch64.fpu’ feature is optional. If present, it should contain registers ‘v0’ through ‘v31’, ‘fpsr’, and ‘fpcr’.
                 * The ‘org.gnu.gdb.aarch64.sve’ feature is optional. If present, it should contain registers ‘z0’ through ‘z31’, ‘p0’ through ‘p15’, ‘ffr’ and ‘vg’.
                 * The ‘org.gnu.gdb.aarch64.pauth’ feature is optional. If present, it should contain registers ‘pauth_dmask’ and ‘pauth_cmask’.
                 */

                return features;
            }
        }

        public ExceptionLevel ExceptionLevel
        {
            get
            {
                lock(elAndSecurityLock)
                {
                    return exceptionLevel;
                }
            }
            set => TlibSetCurrentEl((uint)value);
        }

        public override ExecutionState ExecutionState => ExecutionState.AArch64;

        public override ExecutionState[] SupportedExecutionStates => new[] { ExecutionState.AArch32, ExecutionState.AArch64 };

        public SecurityState SecurityState
        {
            get
            {
                lock(elAndSecurityLock)
                {
                    return securityState;
                }
            }
        }

        public bool FIQMaskOverride => (GetSystemRegisterValue("hcr_el2") & 0b01000) != 0;

        public bool IRQMaskOverride => (GetSystemRegisterValue("hcr_el2") & 0b10000) != 0;

        public Affinity Affinity { get; }

        public bool IsEL3UsingAArch32State => false; // ARM8vA currently supports only AArch64 execution

        public bool HasSingleSecurityState { get; private set; }

        public event Action<ExceptionLevel, SecurityState> ExecutionModeChanged;

        [Export]
        protected void OnTcmMappingUpdate(int index, ulong newAddress, uint el01Enabled, uint el2Enabled)
        {
            throw new CpuAbortException($"TCM regions are not supported on {nameof(ARMv8A)}");
        }

        [Export]
        protected ulong ReadSystemRegisterInterruptCPUInterface(uint offset)
        {
            return gic.ReadSystemRegisterCPUInterface(offset);
        }

        [Export]
        protected void WriteSystemRegisterInterruptCPUInterface(uint offset, ulong value)
        {
            gic.WriteSystemRegisterCPUInterface(offset, value);
        }

        [Export]
        protected uint ReadSystemRegisterGenericTimer32(uint _)
        {
            this.Log(LogLevel.Error, "Reading 32-bit registers of the ARM Generic Timer is not allowed in 64bit version of the CPU");
            return 0;
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer32(uint _, uint __)
        {
            this.Log(LogLevel.Error, "Writing 32-bit registers of the ARM Generic Timer is not allowed in 64bit version of the CPU");
            return;
        }

        [Export]
        protected ulong ReadSystemRegisterGenericTimer64(uint offset)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to read a register of the ARM Generic Timer, but the timer was not found.");
                return 0;
            }
            return timer.ReadRegisterAArch64(offset);
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer64(uint offset, ulong value)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to write a register of the ARM Generic Timer, but the timer was not found.");
                return;
            }
            timer.WriteRegisterAArch64(offset, value);
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            switch((InterruptSignalType)number)
            {
            case InterruptSignalType.IRQ:
                return Interrupt.Hard;
            case InterruptSignalType.FIQ:
                return Interrupt.TargetExternal1;
            case InterruptSignalType.vIRQ:
                return Interrupt.TargetExternal2;
            case InterruptSignalType.vFIQ:
                return Interrupt.TargetExternal3;
            default:
                throw InvalidInterruptNumberException;
            }
        }

        protected override Type RegistersEnum => typeof(ARMv8ARegisters);

        [Export]
        private void OnExecutionModeChanged(uint el, uint isSecure)
        {
            lock(elAndSecurityLock)
            {
                exceptionLevel = (ExceptionLevel)el;
                securityState = isSecure != 0 ? SecurityState.Secure : SecurityState.NonSecure;
            }
            ExecutionModeChanged?.Invoke(ExceptionLevel, SecurityState);
        }

        private ExceptionLevel exceptionLevel;
        private SecurityState securityState;
        private ARM_GenericTimer timer;

#pragma warning disable 649
        [Import]
        private readonly Func<uint> TlibHasEl3;

        [Import]
        private readonly Func<uint, uint, uint> TlibSetAvailableEls;

        [Import]
        private readonly Action<uint> TlibSetCurrentEl;
#pragma warning restore 649

        private readonly object elAndSecurityLock = new object();
        private readonly ARM_GenericInterruptController gic;

        // These '*ReturnValue' enums have to be in sync with their counterparts in 'tlib/arch/arm64/arch_exports.c'.
        private enum SetAvailableElsReturnValue
        {
            SimulationAlreadyStarted = 1,
            EL2OrEL3EnablingFailed = 2,
            Success = 3,
        }
    }
}