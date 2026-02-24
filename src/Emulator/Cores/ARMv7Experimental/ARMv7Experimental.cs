//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 2)]
    public abstract partial class ARMv7Experimental : TranslationCPU, ICPUWithHooks
    {
        public ARMv7Experimental(string cpuType, IMachine machine, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian)
            : base(cpuId, cpuType, machine, endianness)
        {
        }

        public void SetSystemRegisterValue(string name, ulong value)
        {
            ValidateSystemRegisterAccess(name, isWrite: true);

            TlibSetSystemRegister(name, value, 1u /* log_unhandled_access: true */);
        }

        public ulong GetSystemRegisterValue(string name)
        {
            ValidateSystemRegisterAccess(name, isWrite: false);

            return TlibGetSystemRegister(name, 1u /* log_unhandled_access: true */);
        }

        public override void Reset()
        {
            base.Reset();
        }

        // Used to load the correct tlib version
        public override string Architecture { get { return "arm-experimental"; } }

        public override string GDBArchitecture { get { return "arm"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();
                var coreFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.core");
                for(var idx = 0; idx <= 15; idx++)
                {
                    coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)(ArmRegisters.R0 + idx), 32, $"r{idx}", "uint32", "general"));
                }
                coreFeature.Registers.Add(new GDBRegisterDescriptor((uint)ARMv7ExperimentalRegisters.CPSR, 32, "cpsr", "uint32", "general"));
                features.Add(coreFeature);
                return features;
            }
        }

        public bool ImplementsPMSA => MemorySystemArchitecture == MemorySystemArchitectureType.Physical_PMSA;

        public bool ImplementsVMSA => MemorySystemArchitecture == MemorySystemArchitectureType.Virtual_VMSA;

        public abstract MemorySystemArchitectureType MemorySystemArchitecture { get; }

        protected override Interrupt DecodeInterrupt(int number)
        {
            switch(number)
            {
            case 0:
                return Interrupt.Hard;
            case 1:
                return Interrupt.TargetExternal1;
            default:
                throw InvalidInterruptNumberException;
            }
        }

        [Export]
        private ulong GetRandomUlong()
        {
            this.Log(LogLevel.Warning, "TODO: TGAU: not implemented: GetRandomUlong");
            return 0xAA55AA55AA55AA55; // Alternating 0s and 1s in binary
        }

        [Export]
        private void HandlePSCICall()
        {
            this.Log(LogLevel.Warning, "TODO: TGAU: not implemented: HandlePSCICall");
        }

        private bool IsSystemRegisterAccessible(string name, bool isWrite)
        {
            var result = TlibCheckSystemRegisterAccess(name, isWrite ? 1u : 0u);
            return (SystemRegisterCheckReturnValue)result == SystemRegisterCheckReturnValue.AccessValid;
        }

        private void ValidateSystemRegisterAccess(string name, bool isWrite)
        {
            switch((SystemRegisterCheckReturnValue)TlibCheckSystemRegisterAccess(name, isWrite ? 1u : 0u))
            {
            case SystemRegisterCheckReturnValue.AccessValid:
                return;
            case SystemRegisterCheckReturnValue.AccessorNotFound:
                var accessName = isWrite ? "Writing" : "Reading";
                throw new RecoverableException($"{accessName} the {name} register isn't supported");
            case SystemRegisterCheckReturnValue.RegisterNotFound:
                throw new RecoverableException("No such register.");
            default:
                throw new ArgumentException("Invalid TlibCheckSystemRegisterAccess return value");
            }
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        // The arguments are: char *name, uint64_t value, bool log_unhandled_access.
        private readonly Action<string, ulong, uint> TlibSetSystemRegister;

        [Import]
        // The arguments are: char *name, bool log_unhandled_access.
        private readonly Func<string, uint, ulong> TlibGetSystemRegister;

        [Import]
        private readonly Func<string, uint, uint> TlibCheckSystemRegisterAccess;
#pragma warning restore 649

        public enum MemorySystemArchitectureType
        {
            None,
            Physical_PMSA,
            Virtual_VMSA,
        }

        private enum SystemRegisterCheckReturnValue
        {
            RegisterNotFound = 1,
            AccessorNotFound = 2,
            AccessValid = 3,
        }
    }
}
