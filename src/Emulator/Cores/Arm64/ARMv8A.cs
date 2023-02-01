//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class ARMv8A : TranslationCPU, IPeripheralRegister<ARM_GenericTimer, NullRegistrationPoint>
    {
        public ARMv8A(Machine machine, string cpuType, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian)
                : base(cpuId, cpuType, machine, endianness, CpuBitness.Bits64)
        {
            Reset();
        }

        public ulong GetSystemRegisterValue(string name)
        {
            ValidateSystemRegisterAccess(name, isWrite: false);

            return TlibGetSystemRegister(name);
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

            TlibSetSystemRegister(name, value);
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
                coreFeature.Registers.Add(new GDBRegisterDescriptor(31, 64, "sp", "data_ptr", "general"));
                coreFeature.Registers.Add(new GDBRegisterDescriptor(32, 64, "pc", "code_ptr", "general"));
                // CPSR name is in line with GDB's 'G.5.1 AArch64 Features' manual page though it should be named PSTATE.
                coreFeature.Registers.Add(new GDBRegisterDescriptor(33, 32, "cpsr", "uint32", "general"));
                features.Add(coreFeature);

                /*
                 * TODO
                 * The ‘org.gnu.gdb.aarch64.fpu’ feature is optional. If present, it should contain registers ‘v0’ through ‘v31’, ‘fpsr’, and ‘fpcr’.
                 * The ‘org.gnu.gdb.aarch64.sve’ feature is optional. If present, it should contain registers ‘z0’ through ‘z31’, ‘p0’ through ‘p15’, ‘ffr’ and ‘vg’.
                 * The ‘org.gnu.gdb.aarch64.pauth’ feature is optional. If present, it should contain registers ‘pauth_dmask’ and ‘pauth_cmask’. 
                 */

                return features;
            }
        }

        public uint ExceptionLevel
        {
            get => TlibGetCurrentEl();
            set => TlibSetCurrentEl(value);
        }

        public bool IsSecureState => ExceptionLevel == 3 || TlibIsSecureBelowEl3() == 1;

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        [Export]
        protected ulong ReadSystemRegisterGenericTimer(uint offset)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to read a register of the ARM Generic Timer, but the timer was not found.");
                return 0;
            }
            return timer.ReadRegisterAArch64(offset);
        }

        [Export]
        protected void WriteSystemRegisterGenericTimer(uint offset, ulong value)
        {
            if(timer == null)
            {
                this.Log(LogLevel.Error, "Trying to write a register of the ARM Generic Timer, but the timer was not found.");
                return;
            }
            timer.WriteRegisterAArch64(offset, value);
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
        private FuncUInt32StringUInt32 TlibCheckSystemRegisterAccess;

        [Import]
        private FuncUInt32 TlibGetCurrentEl;

        [Import]
        private FuncUInt64String TlibGetSystemRegister;

        [Import]
        private FuncUInt32 TlibIsSecureBelowEl3;

        [Import]
        private FuncUInt32UInt32UInt32 TlibSetAvailableEls;

        [Import]
        private ActionUInt32 TlibSetCurrentEl;

        [Import]
        private ActionStringUInt64 TlibSetSystemRegister;
#pragma warning restore 649
    }
}
