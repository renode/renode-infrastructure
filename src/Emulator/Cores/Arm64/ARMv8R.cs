//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities.Binding;

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

        public override void Reset()
        {
            base.Reset();
            SetSystemRegisterValue("hvbar", defaultHVBARValue);
            SetSystemRegisterValue("vbar", defaultVBARValue);
            foreach(var config in defaultTCMConfiguration)
            {
                RegisterTCMRegion(config);

                if(CfgTcmBoot && config.RegionIndex == 0)
                {
                    // If CFGTCMBOOTx is asserted, we keep TCM#0 enabled
                    continue;
                }

                TlibEnableTcmRegion(
                    config.RegionIndex,
                    TcmRegionEnable.Disable, // EL0 & EL1
                    TcmRegionEnable.Disable  // EL2
                );

                config.El01Enabled = false;
                config.El2Enabled = false;

                TlibEnableExternalPermissionHandlerForRange(config.Address, config.Size, 1U);
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

        [Export]
        public override uint CheckExternalPermissions(ulong address)
        {
            var target = machine.SystemBus.WhatIsAt(address, this);
            foreach(var tcm in defaultTCMConfiguration)
            {
                if(target?.Peripheral == tcm.Memory)
                {
                    var accessGranted = ExceptionLevel == ExceptionLevel.EL3_MonitorMode ||
                                        (ExceptionLevel == ExceptionLevel.EL2_HypervisorMode && tcm.El2Enabled) ||
                                        ((int)ExceptionLevel < 2 && tcm.El01Enabled);

                    return accessGranted ? 1U : 0U;
                }
            }

            return 1U;
        }

        public ExceptionLevel ExceptionLevel { get; private set; }

        public bool CfgTcmBoot { get; set; }

        // ARMv8R AArch32 cores always execute in NonSecure mode ("Arm Architecture Reference Manual Supplement Armv8, for the Armv8-R AArch32 architecture profile" - A1.3.1)
        // ARMv8R AArch64 cores always execute in Secure mode ("Arm Architecture Reference Manual Supplement Armv8, for R-profile AArch64 architecture" - C1.11 and A1.3)
        // since at this moment we only have AArch32 core supporting this ISA, let's lock it in NonSecure state
        public SecurityState SecurityState => SecurityState.NonSecure;

        public bool TrapGeneralExceptions => (GetSystemRegisterValue("hcr") & (1 << 27)) != 0;

        public bool FIQMaskOverride => (GetSystemRegisterValue("hcr") & 0b01000) != 0 || TrapGeneralExceptions;

        public bool IRQMaskOverride => (GetSystemRegisterValue("hcr") & 0b10000) != 0 || TrapGeneralExceptions;

        public Affinity Affinity { get; }

        public override ExecutionState ExecutionState => ExecutionState.AArch32;

        public override ExecutionState[] SupportedExecutionStates => new[] { ExecutionState.AArch32 };

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

                AddSystemRegistersFeature(features, "org.renode.gdb.aarch32.sysregs");

                return features;
            }
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
        protected ulong ReadSystemRegisterGenericTimer64(uint offset)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to read a 64-bit register of the ARM Generic Timer, but the timer was not found.");
                return 0;
            }

            return timer.ReadQuadWordRegisterAArch32(offset);
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
        protected void OnTcmMappingUpdate(int index, ulong newAddress, uint el01Enabled, uint el2Enabled)
        {
            using(ObtainGenericPauseGuard())
            {
                var tcmConfig = defaultTCMConfiguration
                    .Where(configuration => configuration.RegionIndex == index)
                    .SingleOrDefault();

                if(tcmConfig == null)
                {
                    this.Log(LogLevel.Error, "Tried to remap non existing TCM region #{0}", index);
                    return;
                }

                var registrationPoint = machine.SystemBus
                    .GetRegistrationPoints(tcmConfig.Memory)
                    .Where(x => x.Initiator == this)
                    .SingleOrDefault()
                ;

                registrationPoint = new BusRangeRegistration(
                    registrationPoint.Range
                        .MoveToZero()
                        .ShiftBy((long)newAddress))
                ;

                machine.SystemBus
                    .MoveRegistrationWithinContext(tcmConfig.Memory, registrationPoint, this)
                ;

                tcmConfig.El01Enabled = el01Enabled != 0;
                tcmConfig.El2Enabled = el2Enabled != 0;

                var areAllElEnabled = el01Enabled != 0 && el2Enabled != 0;
                TlibEnableExternalPermissionHandlerForRange(newAddress, tcmConfig.Size, areAllElEnabled ? 0U : 1U);
            }
        }

        [Export]
        protected void WriteSystemRegisterInterruptCPUInterface(uint offset, ulong value)
        {
            gic.WriteSystemRegisterCPUInterface(offset, value);
        }

        protected override Type RegistersEnum => typeof(ARMv8RRegisters);

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
            if(!TCMConfiguration.TryCreate(this, memory, regionIndex, out var config))
            {
                return false;
            }

            RegisterTCMRegion(config);
            defaultTCMConfiguration.Add(config);

            return true;
        }

        [Export]
        private void OnExecutionModeChanged(uint el, uint isSecure)
        {
            ExceptionLevel = (ExceptionLevel)el;
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

        private ARM_GenericTimer timer;

#pragma warning disable 649
        [Import]
        private readonly Action<uint, uint> TlibSetMpuRegionsCount;

        [Import]
        private readonly Func<uint, uint, uint> TlibSetAvailableEls;

        [Import]
        private readonly Action<uint, ulong, ulong> TlibRegisterTcmRegion;

        [Import]
        private readonly Action<ulong, TcmRegionEnable, TcmRegionEnable> TlibEnableTcmRegion;
#pragma warning restore 649

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

        private enum TcmRegionEnable : uint
        {
            Disable = 0,
            Enable = 1,
        }
    }
}