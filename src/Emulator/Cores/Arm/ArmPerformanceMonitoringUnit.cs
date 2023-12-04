//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ArmPerformanceMonitoringUnit : IPeripheral
    {
        // PMU logic is implemented in the CPU itself
        // This block only exposes PMU interrupt and optional configuration interface
        public ArmPerformanceMonitoringUnit()
        {
            IRQ = new GPIO();
        }

        public void Reset()
        {
            IRQ.Unset();
            // Nothing more to see here, PMU will reset itself when CPU resets
        }

        public void OnOverflowAction(int counter)
        {
            this.DebugLog("PMU reporting counter overflow for counter {0}", counter);
            IRQ.Set(true);
        }

        public void RegisterCPU(Arm cpu)
        {
            parentCPU = cpu;
        }

        public GPIO IRQ { get; }

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
            using(parentCPU.GetMachine().ObtainPausedState())
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
            using(parentCPU.GetMachine().ObtainPausedState())
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
            using(parentCPU.GetMachine().ObtainPausedState())
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
            using(parentCPU.GetMachine().ObtainPausedState())
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

        private static void VerifyRegister(string register)
        {
            if(!ImplementedRegisters.Contains(register.ToUpperInvariant()))
            {
                throw new RecoverableException($"Invalid register: {register}. See \"ImplementedRegisters\" property for the list of registers");
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

        private const string SelectedCounterRegister = "PMSELR";
        private const string CounterValueRegister = "PMXEVCNTR";
        private const string CounterEventRegister = "PMXEVTYPER";
        private const string ControlRegister = "PMCR";

        private const uint CounterEventRegisterEventMask = 0xFF;
        private const int CounterEventRegisterPL0CountIgnoreOffset = 30;
        private const int CounterEventRegisterPL1CountIgnoreOffset = 31;
        private const uint ControlRegisterEnableMask = 0x1;

        private Arm parentCPU;
    }
}
