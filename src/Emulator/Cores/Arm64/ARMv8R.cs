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
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Debugging;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class ARMv8R : BaseARMv8, IARMSingleSecurityStateCPU, IPeripheralRegister<ARM_GenericTimer, NullRegistrationPoint>
    {
        public ARMv8R(string cpuType, IMachine machine, ARM_GenericInterruptController genericInterruptController, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian, uint mpuRegionsCount = 16, ulong defaultHVBARValue = 0, ulong defaultVBARValue = 0, uint mpuHyperRegionsCount = 16)
                : base(cpuId, cpuType, machine, endianness)
        {
            Affinity = new Affinity(cpuId);
            this.defaultHVBARValue = defaultHVBARValue;
            this.defaultVBARValue = defaultVBARValue;

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
            TlibSetMpuRegionsCount(mpuRegionsCount, mpuHyperRegionsCount);
            TlibSetGicCpuRegisterInterfaceVersion(gic.ArchitectureVersionAtLeast3 ? GICCPUInterfaceVersion.Version30Or40 : GICCPUInterfaceVersion.None);
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            SetSystemRegisterValue("hvbar", defaultHVBARValue);
            SetSystemRegisterValue("vbar", defaultVBARValue);
            foreach(var config in defaultTCMConfiguration)
            {
                RegisterTCMRegion(config);
            }
        }

        public ulong GetSystemRegisterValue(string name)
        {
            ValidateSystemRegisterAccess(name, isWrite: false);

            return TlibGetSystemRegister(name, 1u /* log_unhandled_access: true */);
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
                return;
            case SetAvailableElsReturnValue.EL2OrEL3EnablingFailed:
                throw new RecoverableException($"The '{Model}' core doesn't support all the enabled Exception Levels.");
            // It should never be returned if 'started' is false.
            case SetAvailableElsReturnValue.SimulationAlreadyStarted:
            default:
                throw new ArgumentException("Invalid TlibSetAvailableEls return value!");
            }
        }

        public void SetSystemRegisterValue(string name, ulong value)
        {
            ValidateSystemRegisterAccess(name, isWrite: true);

            TlibSetSystemRegister(name, value, 1u /* log_unhandled_access: true */);
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

        public override string GDBArchitecture { get { return "arm"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.core");
                for(var index = 0u; index <= 12; index++)
                {
                    var cpuRegisterIdx = (uint)ARMv8RRegisters.R0 + index;
                    coreFeature.Registers.Add(new GDBRegisterDescriptor(cpuRegisterIdx, 32, $"r{index}", "uint32", "general"));
                }
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8RRegisters.R13, 32, "sp", "data_ptr", "general"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8RRegisters.R14, 32, "lr", "code_ptr", "general"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8RRegisters.R15, 32, "pc", "code_ptr", "general"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv8RRegisters.CPSR, 32, "cpsr", "uint32", "general"));
                features.Add(coreFeature);

                return features;
            }
        }

        public void RegisterTCMRegion(IMemory memory, uint regionIndex)
        {
            if(!machine.IsPaused)
            {
                throw new RecoverableException("Registering TCM regions might only take place on paused machine");
            }
            if(!TryRegisterTCMRegion(memory, regionIndex))
            {
                this.Log(LogLevel.Error, "Attempted to register a TCM region #{0}, but {1} is not registered for this CPU.", regionIndex, machine.GetLocalName(memory));
            }
        }

        private void RegisterTCMRegion(TCMConfiguration config)
        {
            try
            {
                TlibRegisterTcmRegion(config.Address, config.Size, config.RegionIndex);
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
        }

        private bool TryRegisterTCMRegion(IMemory memory, uint regionIndex)
        {
            ulong address;
            if(!TCMConfiguration.TryFindRegistrationAddress(machine.SystemBus, this, memory, out address))
            {
                return false;
            }

            var config = new TCMConfiguration(checked((uint)address), checked((ulong)memory.Size), regionIndex);
            RegisterTCMRegion(config);
            defaultTCMConfiguration.Add(config);

            return true;
        }

        public ExceptionLevel ExceptionLevel => exceptionLevel;
        // ARMv8R AArch32 cores always execute in NonSecure mode ("Arm Architecture Reference Manual Supplement Armv8, for the Armv8-R AArch32 architecture profile" - A1.3.1)
        // ARMv8R AArch64 cores always execute in Secure mode ("Arm Architecture Reference Manual Supplement Armv8, for R-profile AArch64 architecture" - C1.11 and A1.3)
        // since at this moment we only have AArch32 core supporting this ISA, let's lock it in NonSecure state
        public SecurityState SecurityState => SecurityState.NonSecure;

        public bool TrapGeneralExceptions => (GetSystemRegisterValue("hcr") & (1 << 27)) != 0;
        public bool FIQMaskOverride => (GetSystemRegisterValue("hcr") & 0b01000) != 0 || TrapGeneralExceptions;
        public bool IRQMaskOverride => (GetSystemRegisterValue("hcr") & 0b10000) != 0 || TrapGeneralExceptions;

        public Affinity Affinity { get; }

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
                    this.Log(LogLevel.Error, "Unexpected interrupt type for IRQ#{0}", number);
                    throw InvalidInterruptNumberException;
            }
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
        protected ulong ReadSystemRegisterGenericTimer64(uint offset)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to read a 64-bit register of the ARM Generic Timer, but the timer was not found.");
                return 0;
            }

            return timer.ReadQuadWordRegisterAArch32(offset);
        }

        [Export]
        protected uint ReadSystemRegisterGenericTimer32(uint offset)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to read a 32-bit register of the ARM Generic Timer, but the timer was not found.");
                return 0;
            }

            return timer.ReadDoubleWordRegisterAArch32(offset);
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer64(uint offset, ulong value)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to write a 64-bit register of the ARM Generic Timer, but the timer was not found.");
                return;
            }

            timer.WriteQuadWordRegisterAArch32(offset, value);
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer32(uint offset, uint value)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to write a 32-bit register of the ARM Generic Timer, but the timer was not found.");
                return;
            }

            timer.WriteDoubleWordRegisterAArch32(offset, value);
        }

        [Export]
        private void OnExecutionModeChanged(uint el, uint isSecure)
        {
            exceptionLevel = (ExceptionLevel)el;
            // ARMv8R cores cannot change security state (Architecture Manual mandates it)
            DebugHelper.Assert((isSecure != 0 ? SecurityState.Secure : SecurityState.NonSecure) == SecurityState, $"{nameof(ARMv8R)} should not change its Security State.");
        }

        private void ValidateSystemRegisterAccess(string name, bool isWrite)
        {
            if(name.ToLower().Equals("nzcv"))
            {
                throw new RecoverableException("Use '<cpu_name> PSTATE' to access NZCV.");
            }

            switch((SystemRegisterCheckReturnValue)TlibCheckSystemRegisterAccess(name, isWrite ? 1u : 0u))
            {
            case SystemRegisterCheckReturnValue.AccessValid:
                return;
            case SystemRegisterCheckReturnValue.AccessorNotFound:
                var accessName = isWrite ? "Writing" : "Reading";
                throw new RecoverableException($"{accessName} the {name} register isn't supported.");
            case SystemRegisterCheckReturnValue.RegisterNotFound:
                throw new RecoverableException("No such register.");
            default:
                throw new ArgumentException("Invalid TlibCheckSystemRegisterAccess return value!");
            }
        }

        private ExceptionLevel exceptionLevel;
        private ARM_GenericTimer timer;

        private readonly ARM_GenericInterruptController gic;
        private readonly ulong defaultHVBARValue;
        private readonly ulong defaultVBARValue;

        private readonly List<TCMConfiguration> defaultTCMConfiguration = new List<TCMConfiguration>();

        // These '*ReturnValue' enums have to be in sync with their counterparts in 'tlib/arch/arm64/arch_exports.c'.
        private enum SetAvailableElsReturnValue
        {
            SimulationAlreadyStarted = 1,
            EL2OrEL3EnablingFailed   = 2,
            Success                  = 3,
        }

        private enum SystemRegisterCheckReturnValue
        {
            RegisterNotFound = 1,
            AccessorNotFound = 2,
            AccessValid      = 3,
        }

#pragma warning disable 649
        [Import]
        private Action<GICCPUInterfaceVersion> TlibSetGicCpuRegisterInterfaceVersion;

        [Import]
        private Func<string, uint, uint> TlibCheckSystemRegisterAccess;

        [Import]
        // The arguments are: char *name, bool log_unhandled_access.
        private Func<string, uint, ulong> TlibGetSystemRegister;

        [Import]
        private Func<uint, uint, uint> TlibSetAvailableEls;

        [Import]
        // The arguments are: char *name, uint64_t value, bool log_unhandled_access.
        private Action<string, ulong, uint> TlibSetSystemRegister;

        [Import]
        private Action<uint, uint> TlibSetMpuRegionsCount;

        [Import]
        private Action<uint, ulong, ulong> TlibRegisterTcmRegion;
#pragma warning restore 649
    }
}
