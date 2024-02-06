//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K3XX_SoftwareWatchdogTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_SoftwareWatchdogTimer(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0xFF00010A)
                .WithTaggedFlag("MAP0", 31)
                .WithTaggedFlag("MAP1", 30)
                .WithTaggedFlag("MAP2", 29)
                .WithTaggedFlag("MAP3", 28)
                .WithTaggedFlag("MAP4", 27)
                .WithTaggedFlag("MAP5", 26)
                .WithTaggedFlag("MAP6", 25)
                .WithTaggedFlag("MAP7", 24)
                .WithReservedBits(11, 13)
                .WithTag("ServiceMode", 9, 2)
                .WithTaggedFlag("ResetOnInvalidAccess", 8)
                .WithTaggedFlag("WindowMode", 7)
                .WithTaggedFlag("InterruptThenResetRequest", 6)
                .WithTaggedFlag("HardLock", 5)
                .WithTaggedFlag("SoftLock", 4)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("StopModeControl", 2)
                .WithTaggedFlag("DebugModeControl", 1)
                .WithTaggedFlag("WatchdogEnable", 0);
            Registers.Interrupt.Define(this)
                .WithReservedBits(1, 31)
                .WithTaggedFlag("TimeoutInterruptFlag", 0);
            Registers.Timeout.Define(this, 0x00000320)
                .WithTag("WatchdogTimeout", 0, 32);
            Registers.Window.Define(this)
                .WithTag("WindowStartValue", 0, 32);
            Registers.Service.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("WatchdogServiceCode", 0, 16);
            Registers.CounterOutput.Define(this)
                .WithTag("WatchdogCount", 0, 32);
            Registers.ServiceKey.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("ServiceKey", 0, 16);
            Registers.EventRequest.Define(this)
                .WithReservedBits(1, 31)
                .WithTaggedFlag("ResetRequestFlag", 0);
        }

        public enum Registers
        {
            Control = 0x0, // CR
            Interrupt = 0x4, // IR
            Timeout = 0x8, // TO
            Window = 0xC, // WN
            Service = 0x10, // SR
            CounterOutput = 0x14, // CO
            ServiceKey = 0x18, // SK
            EventRequest = 0x1C // RRR
        }
    }
}
