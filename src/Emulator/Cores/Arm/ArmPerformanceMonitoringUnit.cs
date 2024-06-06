//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ArmPerformanceMonitoringUnit : BasicDoubleWordPeripheral, IKnownSize
    {
        public static void VerifyCPUType(Arm cpu)
        {
            if(!SupportedCPUTypes.Any(t => t.IsAssignableFrom(cpu.GetType())))
            {
                throw new RecoverableException(
                    $"Tried to register {nameof(ArmPerformanceMonitoringUnit)} at {cpu.GetType()} while it can only be currently used with CPUs assignable from:"
                    + SupportedCPUTypes.Select(t => "\n* " + t.ToString())
                );
            }
        }

        // PMU is implemented in tlib, currently it isn't supported by ARMv8A/R tlib.
        public static readonly Type[] SupportedCPUTypes = new[] { typeof(ARMv7A), typeof(ARMv7R) };

        // PMU logic is implemented in the CPU itself
        // This block only exposes PMU interrupt and optional configuration interface
        public ArmPerformanceMonitoringUnit(IMachine machine, ulong peripheralId = PeripheralIdDefault, bool withProcessorIdMMIORegisters = true) : base(machine)
        {
            // CoreSight's PeripheralID contains core-specific fields, let's allow precising them in REPLs.
            PeripheralId = peripheralId;

            // It's unsure whether MMIO registers should include processor ID registers which is
            // why it's configurable. See the comment over `ProcessorIdRegisters`.
            this.withProcessorIdMMIORegisters = withProcessorIdMMIORegisters;

            IRQ = new GPIO();

            DefineMemoryMappedRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            // Most of the properties and fields keep the configuration and don't need to be reset.
            IRQ.Unset();
            SoftwareLockEnabled = true;

            // Nothing more to do here, PMU will reset itself when CPU resets
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            // Only the `LockAccess` register is accessible with Software Lock enabled.
            if(offset != (long)Registers.LockAccess && SoftwareLockEnabled)
            {
                this.Log(LogLevel.Warning, "Tried to write PMU register other than PMLAR using MMIO interface with Software Lock enabled, write ignored");
                this.Log(LogLevel.Info, "Software Lock can be cleared by writing 0x{0:X} to the PMLAR register at 0x{1:X3}", SoftwareLockDisableValue, (ulong)Registers.LockAccess);
                return;
            }

            base.WriteDoubleWord(offset, value);
        }

        public void OnOverflowAction(int counter)
        {
            this.DebugLog("PMU reporting counter overflow for counter {0}", counter);
            IRQ.Set(true);
        }

        public void RegisterCPU(Arm cpu)
        {
            VerifyCPUType(cpu);
            parentCPU = cpu;

            if(withProcessorIdMMIORegisters)
            {
                DefineProcessorIdRegisters(cpu);
            }

            // Peripheral ID's bits 20-23 (4-7 of `PMPID2`) contain CPU's major revision number,
            // AKA variant, which is also encoded in bits 20-23 of MIDR. Let's warn if those differ
            // and update Peripheral ID's variant field to MIDR variant's value.
            var midrVariant = BitHelper.GetValue(cpu.GetSystemRegisterValue("MIDR"), VariantOffset, VariantWidth);
            var peripheralIdVariant = BitHelper.GetValue(PeripheralId, VariantOffset, VariantWidth);
            if(midrVariant != peripheralIdVariant)
            {
                PeripheralId = BitHelper.ReplaceBits(PeripheralId, midrVariant, VariantWidth, destinationPosition: VariantOffset);
                this.Log(LogLevel.Info, "Updating Peripheral ID's CPU variant ({0}) in bits 20-23 to the actual CPU variant from MIDR ({1}).", peripheralIdVariant, midrVariant);
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        // This PMU doesn't support 64-bit registers
        public uint GetRegister(string register)
        {
            VerifyCPURegistered();
            VerifyRegister(register);
            return (uint)parentCPU.GetSystemRegisterValue(register);
        }

        public void SetRegister(string register, uint value)
        {
            VerifyCPURegistered();
            VerifyRegister(register);
            parentCPU.SetSystemRegisterValue(register, value);
        }

        // Convenience wrapper for getting an individual counter value
        public uint GetCounterValue(uint counter)
        {
            ValidateCounter(counter);

            uint res = 0;
            // We are manipulating internal PMU state, we need to pause emulation
            using(parentCPU.GetMachine().ObtainPausedState(true))
            {
                var selectedCounter = (uint)parentCPU.GetSystemRegisterValue(SelectedCounterRegister);
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, counter);
                res = (uint)parentCPU.GetSystemRegisterValue(CounterValueRegister);
                // Restore selected counter
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, selectedCounter);
            }
            return res;
        }

        // Convenience wrapper for setting an individual counter value
        public void SetCounterValue(uint counter, uint value)
        {
            ValidateCounter(counter);
            // We are manipulating internal PMU state, we need to pause emulation
            using(parentCPU.GetMachine().ObtainPausedState(true))
            {
                var selectedCounter = (uint)parentCPU.GetSystemRegisterValue(SelectedCounterRegister);
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, counter);
                parentCPU.SetSystemRegisterValue(CounterValueRegister, value);
                // Restore selected counter
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, selectedCounter);
            }
        }

        // Convenience wrapper for binding an event to a counter
        public void SetCounterEvent(uint counter, uint @event, bool ignoreCountAtPL0 = false, bool ignoreCountAtPL1 = false)
        {
            ValidateCounter(counter);
            // We are manipulating internal PMU state, we need to pause emulation
            using(parentCPU.GetMachine().ObtainPausedState(true))
            {
                var selectedCounter = (uint)parentCPU.GetSystemRegisterValue(SelectedCounterRegister);
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, counter);

                uint eventRegisterValue = @event & CounterEventRegisterEventMask;
                eventRegisterValue |= (ignoreCountAtPL0 ? 1u : 0u) << CounterEventRegisterPL0CountIgnoreOffset;
                eventRegisterValue |= (ignoreCountAtPL1 ? 1u : 0u) << CounterEventRegisterPL1CountIgnoreOffset;
                parentCPU.SetSystemRegisterValue(CounterEventRegister, eventRegisterValue);

                // Restore selected counter
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, selectedCounter);
            }
        }

        // Convenience wrapper for getting the event bound to a counter
        public uint GetCounterEvent(uint counter)
        {
            ValidateCounter(counter);

            uint res = 0;
            // We are manipulating internal PMU state, we need to pause emulation
            using(parentCPU.GetMachine().ObtainPausedState(true))
            {
                var selectedCounter = (uint)parentCPU.GetSystemRegisterValue(SelectedCounterRegister);
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, counter);
                res = (uint)parentCPU.GetSystemRegisterValue(CounterEventRegister);
                // Restore selected counter
                parentCPU.SetSystemRegisterValue(SelectedCounterRegister, selectedCounter);
            }
            return res;
        }

        public uint GetCycleCounterValue()
        {
            VerifyCPURegistered();

            return (uint)parentCPU.GetSystemRegisterValue("PMCCNTR");
        }

        // Artificially increase the value of all event counters subscribing to `eventId` by `value`
        // Can trigger overflow interrupt if it is enabled
        public void BroadcastEvent(int eventId, uint value)
        {
            VerifyCPURegistered();
            if(!Enabled)
            {
                throw new RecoverableException("PMU is disabled, this operation won't execute");
            }

            parentCPU.TlibUpdatePmuCounters(eventId, value);
        }

        // Enable additional debug logs in the PMU
        // To see the logs, it is also needed to set CPU logLevel to DEBUG (logLevel 0)
        public void Debug(bool status)
        {
            VerifyCPURegistered();

            this.Log(LogLevel.Info, "If you want to see PMU logs, remember to set DEBUG or lower logLevel on {0}", parentCPU.GetName());
            parentCPU.TlibPmuSetDebug((uint)(status ? 1 : 0));
        }

        public bool Enabled
        {
            get
            {
                VerifyCPURegistered();
                return (parentCPU.GetSystemRegisterValue(ControlRegister) & ControlRegisterEnableMask) > 0;
            }
            set
            {
                VerifyCPURegistered();
                var register = parentCPU.GetSystemRegisterValue(ControlRegister) & ~ControlRegisterEnableMask;
                parentCPU.SetSystemRegisterValue(ControlRegister, (value ? 1u : 0u) | register);
            }
        }

        // Combined value from `PMCID` registers.
        public uint ComponentId { get; set; } = ComponentIdDefault;

        // Zero by default; the value is IMPLEMENTATION DEFAULT according to the ARMv7AR manual.
        public uint DeviceConfiguration { get; set; }

        // The property only influences a value in the `PMCFGR` Configuration Register.
        // It doesn't make the X flag in the `PMCR` register, i.e. Export Enable, RAZ cause
        // the flag has no effects whatsoever (see `pmu.c:set_c9_pmcr` in tlib).
        public bool ExportSupported { get; set; }

        // Inverted "Software Lock", writes can be enabled by writing `SoftwareLockDisableValue`
        // to the `LockAccess` register.
        public bool SoftwareLockEnabled { get; set; } = true;

        // Each of the `PMPID` registers has just 1 byte of the "64-bit conceptual Peripheral ID".
        // Therefore the LSB of this value will provide the LSB of `PMPID0`, byte1 will provide the
        // LSB of `PMPID1` etc; the same as shown for `DBGPID` in the ARMv7AR manual's Figure C11-1
        // "Mapping between Debug Peripheral ID Registers and a 64-bit Peripheral ID value".
        public ulong PeripheralId { get; private set; }

        // Has no real influence on any logic.
        public bool SecureNonInvasiveDebugEnabled { get; set; }

        // Based on `PMCID` values from the ARMv7AR manual.
        public const uint ComponentIdDefault = 0xB105900D;

        // "64-bit conceptual Peripheral ID" with `PMPID` values from the ARMv7AR manual.
        public const ulong PeripheralIdDefault = 0x04000BB000;

        private static void DefineRegisterWithSingleIdByte(DoubleWordRegister register, int index, Func<ulong> valueProvider, string name)
        {
            if(valueProvider == null)
            {
                throw new ArgumentNullException(nameof(valueProvider));
            }

            register
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => valueProvider().AsRawBytes()[index], name: $"{name} byte {index}")
                .WithReservedBits(8, 24)
                ;
        }

        private static void VerifyRegister(string register)
        {
            if(!ImplementedRegisters.Contains(register.ToUpperInvariant()))
            {
                throw new RecoverableException($"Invalid register: {register}. See \"ImplementedRegisters\" property for the list of registers");
            }
        }

        private void DefineMemoryMappedRegisters()
        {
            Registers.EventCount0.DefineMany(this, EventCountersCount, DefineEventCountRegister);
            Registers.EventTypeSelect0.DefineMany(this, EventCountersCount, DefineEventTypeSelectRegister);

            DefineRegisterAccessingSystemRegister(Registers.CommonEventIdentification0, "PMCEID0");
            DefineRegisterAccessingSystemRegister(Registers.CommonEventIdentification1, "PMCEID1");
            DefineRegisterAccessingSystemRegister(Registers.Control, "PMCR");
            DefineRegisterAccessingSystemRegister(Registers.CountEnableClear, "PMCNTENCLR");
            DefineRegisterAccessingSystemRegister(Registers.CountEnableSet, "PMCNTENSET");
            DefineRegisterAccessingSystemRegister(Registers.CycleCount, "PMCCNTR", FieldMode.Read);
            DefineRegisterAccessingSystemRegister(Registers.InterruptEnableClear, "PMINTENCLR");
            DefineRegisterAccessingSystemRegister(Registers.InterruptEnableSet, "PMINTENSET");
            DefineRegisterAccessingSystemRegister(Registers.OverflowFlagStatus, "PMOVSR");
            // DefineRegisterAccessingSystemRegister(Registers.OverflowFlagStatusSet, "PMOVSSET");  // Not supported in tlib.
            DefineRegisterAccessingSystemRegister(Registers.SoftwareIncrement, "PMSWINC", FieldMode.Write);
            DefineRegisterAccessingSystemRegister(Registers.UserEnable, "PMUSERENR");

            Registers.Configuration.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => EventCountersCount, name: "Number of event counters (N)")
                .WithEnumField<DoubleWordRegister, CounterSizes>(8, 6, FieldMode.Read, valueProviderCallback: _ => CounterSizes.Counters32Bit, name: "Counter size (SIZE)")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => true, name: "Cycle counter implemented (CC)")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => true, name: "Cycle counter clock divider implemented (CCD)")
                .WithFlag(16, writeCallback: (_, newValue) => ExportSupported = newValue, valueProviderCallback: _ => ExportSupported, name: "Export supported (EX)")
                .WithReservedBits(17, 2)
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => true, name: "User-mode EnableRegister implemented (UEN)")
                .WithReservedBits(20, 12);

            // `PMCLAIM*` registers aren't described in the ARMv7AR manual; their implementation is based on the similar `DBGCLAIM*` registers.
            Registers.ClaimTagClear.Define(this)
                // `CLAIM` bits aren't used in the model besides the `ClaimTagSet` below.
                .WithFlags(0, 8, out var claimBits, FieldMode.Read | FieldMode.WriteOneToClear)
                .WithReservedBits(8, 24);

            Registers.ClaimTagSet.Define(this)
                // This register is only read to find out which bits are supported. `ClaimTagClear` can only be used to really read `claimBits`.
                .WithFlags(0, 8, writeCallback: (index, _, newValue) => claimBits[index].Value |= newValue, valueProviderCallback: (_, __) => true)
                .WithReservedBits(8, 24);

            Registers.LockAccess.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, newValue) =>
                    {
                        var newSoftwareLockEnabled = newValue != SoftwareLockDisableValue;
                        if(SoftwareLockEnabled == newSoftwareLockEnabled)
                        {
                            if(SoftwareLockEnabled)
                            {
                                this.Log(LogLevel.Warning, "Tried to disable Software Lock with invalid value 0x{0:X}, should be 0x{1:X}", newValue, SoftwareLockDisableValue);
                            }
                            return;
                        }

                        this.Log(LogLevel.Debug, "Software Lock {0}", newSoftwareLockEnabled ? "enabled" : "disabled");
                        SoftwareLockEnabled = newSoftwareLockEnabled;
                    }
                );

            Registers.LockStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "Software Lock implemented (SLI)")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => SoftwareLockEnabled, name: "Software Lock status (SLK)")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "Not 32-bit (nTT)")
                .WithReservedBits(3, 29);

            // Based on `PMAUTHSTATUS` for implementations without the Security Extensions; don't confuse it with `DBGAUTHSTATUS`.
            Registers.AuthenticationStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "Non-secure invasive debug enabled (NSE)")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "Non-secure invasive debug features implemented (NSI)")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "Non-secure non-invasive debug enabled (NSNE)")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "Non-secure non-invasive debug features implemented (NSNI)")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "Secure invasive debug enabled (SE)")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "Secure invasive debug features implemented (SI)")
                // The `SNE` flag is the only one which isn't constant according to the ARMv7AR manual hence the public settable property.
                // Its value should be "the logical result of `DBGEN` OR `NIDEN`" signals though.
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => SecureNonInvasiveDebugEnabled, name: "Secure non-invasive debug enabled (SNE)")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "Secure non-invasive debug features implemented (SNI)")
                .WithReservedBits(8, 24);

            Registers.DeviceConfiguration.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => DeviceConfiguration);

            Registers.DeviceType.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x6, name: "Main class (C)")  // Performance Monitors
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Sub type (T)")    // Processor
                .WithReservedBits(8, 24);

            // We start with PeripheralIdentification4 because registers 4-7 are before 0-3; hence also the unusual index manipulation.
            Registers.PeripheralIdentification4.DefineMany(this, 8,
                (register, index) => DefineRegisterWithSingleIdByte(register, index < 4 ? index + 4 : index - 4, () => PeripheralId, "Peripheral ID")
            );

            Registers.ComponentIdentification0.DefineMany(this, 4,
                (register, index) => DefineRegisterWithSingleIdByte(register, index, () => ComponentId, "Component ID")
            );
        }

        private void DefineEventCountRegister(DoubleWordRegister eventCountRegister, int index)
        {
            var counterIndex = (uint)index;
            eventCountRegister.DefineValueField(0, 32,
                writeCallback: (_, newValue) => SetCounterValue(counterIndex, (uint)newValue),
                valueProviderCallback: _ => GetCounterValue(counterIndex)
            );
        }

        private void DefineEventTypeSelectRegister(DoubleWordRegister eventTypeSelectRegister, int index)
        {
            var counter = (uint)index;
            eventTypeSelectRegister
                .WithValueField(0, 8,
                    writeCallback: (_, newValue) => SetCounterEvent(counter, (uint)newValue),
                    valueProviderCallback: _ => GetCounterEvent(counter))
                .WithReservedBits(8, 24);
        }

        private void DefineProcessorIdRegisters(Arm cpu)
        {
            var midrAliases = new List<ProcessorIdRegisters>
            {
                ProcessorIdRegisters.MainIDAlias,
                // It's a `MIDR` alias if `REVIDR` isn't really implemented which is true for tlib.
                // The manual says it's UNKNOWN otherwise as `REVIDR` can only be read via CP15.
                ProcessorIdRegisters.RevisionID_REVIDR,
            };
            midrAliases.AddIf(!cpu.ImplementsPMSA, ProcessorIdRegisters.MPUType_MPUIR);
            midrAliases.AddIf(!cpu.ImplementsVMSA, ProcessorIdRegisters.TLBType_TLBTR);

            foreach(var register in Enum.GetValues(typeof(ProcessorIdRegisters)).Cast<ProcessorIdRegisters>())
            {
                // Except for MIDR aliases, the registers name is included in the `register` name.
                var systemRegisterName = midrAliases.Contains(register) ? "MIDR" : register.ToString().Split(new[] {'_'}, count: 2).Last();
                register.Define(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => parentCPU.GetSystemRegisterValue(systemRegisterName));
            }
        }

        private void DefineRegisterAccessingSystemRegister(Registers register, string systemRegisterName, FieldMode fieldMode = FieldMode.Read | FieldMode.Write)
        {
            register.Define(this)
                .WithValueField(0, 32, fieldMode,
                    writeCallback: fieldMode.IsWritable() ? (_, newValue) => parentCPU.SetSystemRegisterValue(systemRegisterName, newValue) : (Action<ulong, ulong>)null,
                    valueProviderCallback: fieldMode.IsReadable() ? _ => parentCPU.GetSystemRegisterValue(systemRegisterName) : (Func<ulong, ulong>)null
                );
        }

        private void ValidateCounter(uint counter)
        {
            VerifyCPURegistered();

            var pmcr = (uint)parentCPU.GetSystemRegisterValue(ControlRegister);
            var supportedCounters = BitHelper.GetMaskedValue(pmcr, 11, 5) >> 11;

            if(counter >= supportedCounters)
            {
                throw new RecoverableException($"Invalid counter: {counter}, select from 0 to {supportedCounters - 1}");
            }
        }

        private void VerifyCPURegistered()
        {
            if(parentCPU == null)
            {
                throw new RecoverableException("No CPU registered itself in the PMU. You can register it manually by calling \"RegisterCPU\"");
            }
        }

        // This list needs to be maintained in sync with the tlib implementation of PMU
        // E.g. if we extend PMU implementation beyond this registers, they need to be added here too
        // It's made public so the list is available in the Monitor for the end user
        public static readonly HashSet<string> ImplementedRegisters
            = new HashSet<string>
        {
            "PMCR",
            "PMCNTENSET",
            "PMCNTENCLR",
            "PMOVSR",
            "PMSWINC",

            "PMCEID0",
            "PMCEID1",

            "PMSELR",
            "PMCCNTR",
            "PMXEVTYPER",
            "PMXEVCNTR",

            "PMUSERENR",
            "PMINTENSET",
            "PMINTENCLR",
        };

        private Arm parentCPU;

        private readonly bool withProcessorIdMMIORegisters;

        private const string SelectedCounterRegister = "PMSELR";
        private const string CounterValueRegister = "PMXEVCNTR";
        private const string CounterEventRegister = "PMXEVTYPER";
        private const string ControlRegister = "PMCR";

        private const uint CounterEventRegisterEventMask = 0xFF;
        private const int CounterEventRegisterPL0CountIgnoreOffset = 30;
        private const int CounterEventRegisterPL1CountIgnoreOffset = 31;
        private const uint ControlRegisterEnableMask = 0x1;
        private const uint EventCountersCount = 31;  // Doesn't include cycle counter.
        private const uint SoftwareLockDisableValue = 0xC5ACCE55;
        private const int VariantOffset = 20;  // In both Peripheral ID and MIDR.
        private const int VariantWidth = 4;  // In both Peripheral ID and MIDR.

        private enum CounterSizes
        {
            Counters32Bit = 0b011111,
            Counters64Bit = 0b111111,
        }

        // Based on Cortex-R8 manual 10.1.2 "PMU management registers" chapter's Table 10-3
        // "Processor Identifier Registers". The same offsets can be found in the ARMv7AR manual
        // in chapter C11.10.1 "About the Debug management registers" but describing memory-mapped
        // debug registers while Table D2-1 "Performance Monitors memory-mapped register views"
        // specifies 0xCC4-0xD7C offsets to be reserved. It's the reason for having the
        // `withProcessorIdMMIORegisters` construction parameter.
        private enum ProcessorIdRegisters
        {
            MainID_MIDR                        = 0xD00,
            CacheType_CTR                      = 0xD04,
            TCMType_TCMTR                      = 0xD08,
            TLBType_TLBTR                      = 0xD0C,
            MPUType_MPUIR                      = 0xD10,
            MultiprocessorAffinity_MPIDR       = 0xD14,
            RevisionID_REVIDR                  = 0xD18,
            MainIDAlias                        = 0xD1C,
            ProcessorFeature0_ID_PFR0          = 0xD20,
            ProcessorFeature1_ID_PFR1          = 0xD24,
            DebugFeature0_ID_DFR0              = 0xD28,
            AuxiliaryFeature0_ID_AFR0          = 0xD2C,
            MemoryModelFeature0_ID_MMFR0       = 0xD30,
            MemoryModelFeature1_ID_MMFR1       = 0xD34,
            MemoryModelFeature2_ID_MMFR2       = 0xD38,
            MemoryModelFeature3_ID_MMFR3       = 0xD3C,
            InstructionSetAttribute0_ID_ISAR0  = 0xD40,
            InstructionSetAttribute1_ID_ISAR1  = 0xD44,
            InstructionSetAttribute2_ID_ISAR2  = 0xD48,
            InstructionSetAttribute3_ID_ISAR3  = 0xD4C,
            InstructionSetAttribute4_ID_ISAR4  = 0xD50,
            InstructionSetAttribute5_ID_ISAR5  = 0xD54,
        }

        // Memory-mapped PMU registers; PMSELR isn't accessible because all counters can be
        // accessed directly. At 0xFxx there are extra registers not available through CP15.
        private enum Registers
        {
            // Based on ARM Architecture Reference Manual ARMv7-A and ARMv7-R edition's Table D2-1
            // "Performance Monitors memory-mapped register views" and Arm CoreSight Architecture
            // Performance Monitoring Unit Architecture's (further referenced as "CoreSight PMU
            // Specification") Table 3.1 "Memory-mapped register map".
            //
            // CoreSight PMU Specification's `PMCCNTR` offset, 0x3C, seems to be invalid. Its ID
            // is 31, which is even mentioned in the "2.6.3 Fixed-function cycle counter extension"
            // so it should be 0x7C as in the ARMv7AR manual. That's what will be used here.
            //
            // The offsets are for CoreSight PMU without dual-page extension for 32-bit registers.
            EventCount0                = 0x000,  // PMXEVCNTR<0> (PMEVCNTR in CoreSight PMU Specification)
            CycleCount                 = 0x07C,  // PMCCNTR (PMXEVCNTR<31>)
            EventTypeSelect0           = 0x400,  // PMXEVTYPER<0> (PMEVTYPER in CoreSight PMU Specification)
            CountEnableSet             = 0xC00,  // PMCNTENSET
            CountEnableClear           = 0xC20,  // PMCNTENCLR
            InterruptEnableSet         = 0xC40,  // PMINTENSET
            InterruptEnableClear       = 0xC60,  // PMINTENCLR
            OverflowFlagStatus         = 0xC80,  // PMOVSR
            SoftwareIncrement          = 0xCA0,  // PMSWINC
            OverflowFlagStatusSet      = 0xCC0,  // PMOVSSET; not implemented, it's only in VMSA CPUs with Virtualization Extensions
            Configuration              = 0xE00,  // PMCFGR, there's no system register equivalent
            Control                    = 0xE04,  // PMCR
            UserEnable                 = 0xE08,  // PMUSERENR
            CommonEventIdentification0 = 0xE20,  // PMCEID0
            CommonEventIdentification1 = 0xE24,  // PMCEID1

            // There are no system register equivalents for any of the registers that follow.

            // The CoreSight PMU Specification contains neither `PMCLAIM*` nor `PML*R` registers.
            // ARMv7AR manual says "PMLAR is UNPREDICTABLE in the external debug interface" with
            // something similar for `PMLSR` so it seems those should only be accessible with MMIO
            // accesses. `PMCLAIM*` registers aren't described at all so their implementation is
            // based on the `DBGCLAIM*` registers.
            ClaimTagSet                = 0xFA0,  // PMCLAIMSET
            ClaimTagClear              = 0xFA4,  // PMCLAIMCLR
            LockAccess                 = 0xFB0,  // PMLAR
            LockStatus                 = 0xFB4,  // PMLSR
            AuthenticationStatus       = 0xFB8,  // PMAUTHSTATUS

            // The registers below seem to be required for CoreSight mostly.
            DeviceConfiguration        = 0xFC8,  // PMDEVID
            DeviceType                 = 0xFCC,  // PMDEVTYPE
            PeripheralIdentification4  = 0xFD0,  // PMPID4
            PeripheralIdentification5  = 0xFD4,  // PMPID5
            PeripheralIdentification6  = 0xFD8,  // PMPID6
            PeripheralIdentification7  = 0xFDC,  // PMPID7
            PeripheralIdentification0  = 0xFE0,  // PMPID0
            PeripheralIdentification1  = 0xFE4,  // PMPID1
            PeripheralIdentification2  = 0xFE8,  // PMPID2
            PeripheralIdentification3  = 0xFEC,  // PMPID3
            ComponentIdentification0   = 0xFF0,  // PMCID0
            ComponentIdentification1   = 0xFF4,  // PMCID1
            ComponentIdentification2   = 0xFF8,  // PMCID2
            ComponentIdentification3   = 0xFFC,  // PMCID3
        }
    }
}
