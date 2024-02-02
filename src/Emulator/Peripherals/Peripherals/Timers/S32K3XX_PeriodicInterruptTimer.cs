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
    public class S32K3XX_PeriodicInterruptTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_PeriodicInterruptTimer(IMachine machine, bool hasRealTimeInterrupt = false, bool hasLifetimeTimer = false, bool supportsTimersChaining = false) : base(machine)
        {
            DefineRegisters(hasRealTimeInterrupt, hasLifetimeTimer, supportsTimersChaining);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters(bool hasRealTimeInterrupt, bool hasLifetimeTimer, bool supportsTimersChaining)
        {
            var moduleControl = Registers.ModuleControl.Define(this, hasRealTimeInterrupt ? 0x6U : 0x2U)
                .WithReservedBits(3, 29)
                .WithTaggedFlag("ModuleDisableForPIT", 1)
                .WithTaggedFlag("Freeze", 0);
            if(hasRealTimeInterrupt)
            {
                moduleControl.WithTaggedFlag("ModuleDisableForRTI", 2);
            }
            else
            {
                moduleControl.WithReservedBits(2, 1);
            }

            if(hasLifetimeTimer)
            {
                Registers.UpperLifetimer.Define(this)
                    .WithTag("LifetimerValue", 0, 32);
                Registers.LowerLifetimer.Define(this)
                    .WithTag("LifetimerValue", 0, 32);
            }

            var channels = new Dictionary<Registers, bool>
            {
                {Registers.LoadValue0, false},
                {Registers.LoadValue1, supportsTimersChaining},
                {Registers.LoadValue2, supportsTimersChaining},
                {Registers.LoadValue3, supportsTimersChaining},
            };
            if(hasRealTimeInterrupt)
            {
                channels.Add(Registers.LoadValueRTI, false);
            }
            foreach(var channel in channels)
            {
                DefineChannelRegisters(channel.Key, channel.Value);
            }

            if(hasRealTimeInterrupt)
            {
                Registers.LoadValueSyncStatusRTI.Define(this)
                    .WithReservedBits(1, 31)
                    .WithTaggedFlag("SyncStatus", 0);
            }
        }

        private void DefineChannelRegisters(Registers loadValueOffset, bool supportsChaining)
        {
            // All channels have the same register map.
            var offsetToTimer0 = loadValueOffset - Registers.LoadValue0;
            var currentValueOffset = Registers.CurrentValue0 + offsetToTimer0;
            var controlOffset = Registers.Control0 + offsetToTimer0;
            var flagOffset = Registers.Flag0 + offsetToTimer0;

            loadValueOffset.Define(this)
                .WithTag("StartValue", 0, 32);
            currentValueOffset.Define(this)
                .WithTag("CurrentValue", 0, 32);

            var controlRegister = controlOffset.Define(this)
                .WithReservedBits(3, 29)
                .WithTaggedFlag("InterruptEnable", 1)
                .WithTaggedFlag("TimerEnable", 0);
            if(supportsChaining)
            {
                controlRegister.WithTaggedFlag("ChainMode", 2);
            }
            else
            {
                controlRegister.WithReservedBits(2, 1);
            }

            flagOffset.Define(this)
                .WithReservedBits(1, 31)
                .WithTaggedFlag("InterruptFlag", 0);
        }

        public enum Registers
        {
            ModuleControl = 0x0, // MCR
            UpperLifetimer = 0xE0, // LTMR64H
            LowerLifetimer = 0xE4, // LTMR64L
            LoadValueSyncStatusRTI = 0xEC, // RTI_LDVAL_STAT
            LoadValueRTI = 0xF0, // RTI_LDVAL
            CurrentValueRTI = 0xF4, // RTI_CVAL
            ControlRTI = 0xF8, // RTI_TCTRL
            FlagRTI = 0xFC, // RTI_TFLG
            LoadValue0 = 0x100, // LDVAL0
            CurrentValue0 = 0x104, // CVAL0
            Control0 = 0x108, // TCTRL0
            Flag0 = 0x10C, // TFLG0
            LoadValue1 = 0x110, // LDVAL1
            CurrentValue1 = 0x114, // CVAL1
            Control1 = 0x118, // TCTRL1
            Flag1 = 0x11C, // TFLG1
            LoadValue2 = 0x120, // LDVAL2
            CurrentValue2 = 0x124, // CVAL2
            Control2 = 0x128, // TCTRL2
            Flag2 = 0x12C, // TFLG2
            LoadValue3 = 0x130, // LDVAL3
            CurrentValue3 = 0x134, // CVAL3
            Control3 = 0x138, // TCTRL3
            Flag3 = 0x13C, // TFLG3
        }
    }
}
