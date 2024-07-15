//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Implementation based on: https://developer.arm.com/documentation/ddi0458/c/Multiprocessing/About-multiprocessing-and-the-SCU
    public class ArmSnoopControlUnit : BasicDoubleWordPeripheral, IHasOwnLife, IKnownSize
    {
        public ArmSnoopControlUnit(IMachine machine, byte smpMask = 0xFF, ArmSignalsUnit signalsUnit = null) : base(machine)
        {
            this.signalsUnit = signalsUnit;
            this.smpMask = smpMask;
            registeredCPUs = new List<ICPU>();

            DefineRegisters();
            Reset();
        }

        public void RegisterCPU(ICPU cpu)
        {
            if(registeredCPUs.Contains(cpu))
            {
                throw new RecoverableException($"CPU: {cpu.GetName()} was already registered");
            }
            else if(registeredCPUs.Count >= MaximumCPUs)
            {
                throw new RecoverableException($"Number of registered CPUs cannot be greater than {MaximumCPUs}");
            }
            else
            {
                Logger.Log(LogLevel.Debug, "Registered CPU {0} at index {1}", cpu.GetName(), registeredCPUs.Count);
                registeredCPUs.Add(cpu);
            }
        }

        public void Pause()
        {
            // Intentionally left blank.
        }

        public override void Reset()
        {
            lockedCPUs = new bool[MaximumCPUs];
            started = false;

            /* Do not reset FilteringStart/End properties - this is intentional
            /* they are SoC-specific and once set should not change on Reset
            */

            base.Reset();
        }

        public void Resume()
        {
            // Intentionally left blank.
        }

        public void Start()
        {
            if(started)
            {
                return;
            }
            started = true;
            ApplyConfigurationSignals();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                CheckRegisteredCPUs();
                var idx = registeredCPUs.IndexOf(cpu);
                if(idx == -1)
                {
                    Logger.Log(LogLevel.Error, "CPU {0} is not registered in Snoop Control Unit", cpu.GetName());
                }
                else if(lockedCPUs[idx])
                {
                    Logger.Log(LogLevel.Warning, "Tried to write value {0} at offset {1}, but access for CPU {2} has been locked", value, offset, cpu.GetName());
                    return;
                }
            }
            RegistersCollection.Write(offset, value);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out enabled, name: "SCU Enable")
                .WithFlag(1, out addressFilteringEnabled, FieldMode.Read, name: "Address Filtering Enable")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: (_) => false, name: "ECC/Parity Enable") // Parity for Cortex-A9, otherwise ECC
                .WithTaggedFlag("Speculative linefills enable", 3)
                .WithTaggedFlag("Force all Device to port0 enable", 4) // Cortex-A9 only
                .WithTaggedFlag("Standby enable", 5)
                .WithTaggedFlag("IC Standby enable", 6)
                .WithReservedBits(7, 5)
                .WithTaggedFlag("ECC enable M0", 12)
                .WithTaggedFlag("ECC enable M1", 13)
                .WithTaggedFlag("ECC enable MP", 14)
                .WithTaggedFlag("ECC enable ACP", 15)
                .WithTaggedFlag("ECC enable 0 FPP", 16) // Cortex-R8 only
                .WithTaggedFlag("ECC enable 1 FPP", 17)
                .WithTaggedFlag("ECC enable 2 FPP", 18)
                .WithTaggedFlag("ECC enable 3 FPP", 19)
                .WithTaggedFlag("ECC enable AXI TCM slave", 20)
                .WithReservedBits(21, 11);

            Registers.Configuration.Define(this)
                // 0 means one processor, 1 means two, etc.
                .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: (_) => { return (ulong)CountCPUs() - 1; }, name: "Number of Processors")
                .WithReservedBits(2, 2)
                /*  TODO: To check if a CPU is in SMP mode we should read SMP bit (6) from Auxiliary Control Register
                    For now let's mark all as participating in SMP, if the mask allows it
                */
                .WithValueField(4, 4, FieldMode.Read, 
                    valueProviderCallback: (_) =>
                    {
                        return BitHelper.CalculateMask(CountCPUs(), 0) & smpMask;
                    },
                    name: "CPUs in SMP")
                .WithTag("Cache/Tag RAM sizes", 8, 16) // This field's layout differs greatly between cores
                .WithReservedBits(24, 7)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: (_) =>
                    {
                        return MasterFilteringStartRange != 0 && MasterFilteringEndRange != 0;
                    },
                    name: "AXI master port 1"); // Cortex-R8 only

            Registers.AccessControl.Define(this)
                .WithFlags(0, MaximumCPUs,
                    writeCallback: (idx, _, val) =>
                    {
                        if(!val)
                        {
                            lockedCPUs[idx] = true;
                        }
                    },
                    valueProviderCallback: (idx, _) => !lockedCPUs[idx])
                .WithReservedBits(MaximumCPUs, 32 - MaximumCPUs);

            /*  We will return 1s here, meaning we don't support limiting access to other peripherals
                in Secure/Non-secure state
            */
            Registers.NonSecureAccessControl.Define(this)
                .WithFlags(0, 12, FieldMode.Read, valueProviderCallback: (_, __) => true)
                .WithReservedBits(12, 20);

            // These should have reserved lower 20 bits (Should-Be-Zero), so we clear them
            Registers.PeripheralsFilteringStart.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PeripheralsFilteringStartRange & ~0xFFFFFUL);

            Registers.PeripheralsFilteringEnd.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PeripheralsFilteringEndRange & ~0xFFFFFUL);

            Registers.MasterFilteringStart.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => MasterFilteringStartRange & ~0xFFFFFUL);

            Registers.MasterFilteringEnd.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => MasterFilteringEndRange & ~0xFFFFFUL);
        }

        public bool IsPaused => false;

        public ulong MasterFilteringStartRange { get; set; }
        public ulong MasterFilteringEndRange { get; set; }

        public ulong PeripheralsFilteringStartRange { get; set; }
        public ulong PeripheralsFilteringEndRange { get; set; }

        public long Size => 0x100;

        private void ApplyConfigurationSignals()
        {
            if(signalsUnit == null)
            {
                return;
            }

            addressFilteringEnabled.Value = signalsUnit.IsSignalEnabled(ArmSignals.MasterFilterEnable);
            MasterFilteringEndRange = signalsUnit.GetAddress(ArmSignals.MasterFilterEnd);
            MasterFilteringStartRange = signalsUnit.GetAddress(ArmSignals.MasterFilterStart);
            PeripheralsFilteringEndRange = signalsUnit.GetAddress(ArmSignals.PeripheralFilterEnd);
            PeripheralsFilteringStartRange = signalsUnit.GetAddress(ArmSignals.PeripheralFilterStart);
        }

        private int CountCPUs()
        {
            CheckRegisteredCPUs();
            return registeredCPUs.Count;
        }

        private void CheckRegisteredCPUs()
        {
            if(registeredCPUs.Count == 0)
            {
                var cpus = sysbus.GetCPUs();
                var numberOfCPUs = cpus.Count();

                // Cap Number of CPUs
                if(numberOfCPUs > MaximumCPUs)
                {
                    Logger.Log(LogLevel.Error, "Number of CPUs: {0} is more than the maximum supported. Capping CPUs number to: {1}. CPUs can be registered manually to overcome this warning.", numberOfCPUs, MaximumCPUs);
                    numberOfCPUs = MaximumCPUs;
                }

                registeredCPUs = cpus.OrderBy(x => x.MultiprocessingId).Take(numberOfCPUs).ToList();
            }
        }

        private IFlagRegisterField addressFilteringEnabled;
        private IFlagRegisterField enabled;
        private List<ICPU> registeredCPUs;
        private bool[] lockedCPUs;
        private bool started;

        private readonly ArmSignalsUnit signalsUnit;
        private readonly byte smpMask;

        // Should not be more than 4, but could be less
        private const int MaximumCPUs = 4;

        private enum Registers : long
        {
            Control = 0x0,
            Configuration = 0x4,

            // Not implemented
            PowerStatus = 0x8,
            InvalidateAllDataCacheLines = 0xC,
            // Linux might use this due to a bug in cache maintenance operations. Unimplemented
            SCUDiagnosticControl = 0x30,

            /*  See: https://developer.arm.com/documentation/ddi0458/c/Functional-Description/Processor-ports/AXI-master-port-1?lang=en
                MasterFilteringStart/End forces accesses at the range to be performed through AXI master port 1 which has
                "ECC protection on data and parity on control bits". These seem to be handled opaquely by the hardware.
                They can be declared via appropriate properties.
            */
            MasterFilteringStart = 0x40,
            MasterFilteringEnd = 0x44,

            /* See: https://developer.arm.com/documentation/ddi0458/c/Functional-Description/Processor-ports/AXI-peripheral-port?lang=en
               "It is used to access certain peripherals with a low latency, and having burst support."
               On Cortex-R8 these can also be referred to as LLPFilteringStart/End
            */
            PeripheralsFilteringStart = 0x48,
            PeripheralsFilteringEnd = 0x4C,

            AccessControl = 0x50,

            // See: https://developer.arm.com/documentation/ddi0407/e/snoop-control-unit/scu-registers/scu-non-secure-access-control-register
            NonSecureAccessControl = 0x54,

            // ECC Error banks are unimplemented
            ErrorBankFirstEntry = 0x60,
            ErrorBankSecondEntry = 0x64,

            // Debug operations are unimplemented
            DebugTagRAMOperation = 0x70,
            DebugTagRAMDataValue = 0x74,
            DebugTagRAMECCChunk = 0x78,

            // Cortex-R8 specific - unimplemented
            FatalECCError = 0x7C,
            /* Filtering for Fast Peripheral Port
               We ignore it, as is the case with AXI port 0
            */
            FPPFilteringStartCore0 = 0x80,
            FPPFilteringEndCore0 = 0x84,
            FPPFilteringStartCore1 = 0x88,
            FPPFilteringEndCore1 = 0x8C,
            FPPFilteringStartCore2 = 0x90,
            FPPFilteringEndCore2 = 0x94,
            FPPFilteringStartCore3 = 0x98,
            FPPFilteringEndCore3 = 0x9C,
        }
    }
}
