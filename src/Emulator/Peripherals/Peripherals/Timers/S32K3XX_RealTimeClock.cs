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
    public class S32K3XX_RealTimeClock : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_RealTimeClock(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.SupervisorControl.Define(this, 0x80000000)
                .WithTaggedFlag("RTCSupervisorBit", 31)
                .WithReservedBits(0, 31);
            Registers.Control.Define(this)
                .WithTaggedFlag("CounterEnable", 31)
                .WithTaggedFlag("RTCInterruptEnable", 30)
                .WithTaggedFlag("FreezeEnableBit", 29)
                .WithTaggedFlag("CounterRollOverInterruptEnable", 28)
                .WithReservedBits(16, 12)
                .WithTaggedFlag("AutonomousPeriodicInterruptEnable", 15)
                .WithTaggedFlag("APIInterruptEnable", 14)
                .WithTag("ClockSelect", 12, 2)
                .WithTaggedFlag("DivideBy512enable", 11)
                .WithTaggedFlag("DivideBy32enable", 10)
                .WithReservedBits(1, 9)
                .WithTaggedFlag("TriggerEnableForAnalogComparator", 0);
            Registers.Status.Define(this)
                .WithReservedBits(30, 2)
                .WithTaggedFlag("RTCInterruptFlag", 29)
                .WithReservedBits(19, 10)
                .WithTaggedFlag("InvalidRTCWrite", 18)
                .WithTaggedFlag("InvalidAPIVALWrite", 17)
                .WithReservedBits(16, 1)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("APIInterruptFlag", 13)
                .WithReservedBits(11, 2)
                .WithTaggedFlag("CounterRollOverInterruptFlag", 10)
                .WithReservedBits(0, 9);
            Registers.Counter.Define(this)
                .WithTag("RTCCounterValue", 0, 32);
            Registers.APICompareValue.Define(this)
                .WithTag("APICompareValue", 0, 32);
            Registers.RTCCompareValue.Define(this)
                .WithTag("RTCCompareValue", 0, 32);
        }

        public enum Registers
        {
            SupervisorControl = 0x0, // RTCSUPV
            Control = 0x4, // RTCC
            Status = 0x8, // RTCS
            Counter = 0xC, // RTCCNT
            APICompareValue = 0x10, // APIVAL
            RTCCompareValue = 0x14 // RTCVAL
        }
    }
}
