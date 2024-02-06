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
    public class S32K3XX_SystemTimerModule : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_SystemTimerModule(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("CounterPrescaler", 8, 8)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("Freeze", 1)
                .WithTaggedFlag("TimerEnable", 0);
            Registers.Count.Define(this)
                .WithTag("TimerCount", 0, 32);

            var channelSize = (uint)(Registers.ChannelControl1 - Registers.ChannelControl0);
            Registers.ChannelControl0.DefineMany(this, ChannelCount, stepInBytes: channelSize, setup: (reg, index) => reg
                .WithReservedBits(1, 31)
                .WithTaggedFlag("ChannelEnable", 0)
            );
            Registers.ChannelInterrupt0.DefineMany(this, ChannelCount, stepInBytes: channelSize, setup: (reg, index) => reg
                .WithReservedBits(1, 31)
                .WithTaggedFlag("ChannelInterruptFlag", 0)
            );
            Registers.ChannelCompare0.DefineMany(this, ChannelCount, stepInBytes: channelSize, setup: (reg, index) => reg
                .WithTag("ChannelCompare", 0, 32)
            );
        }

        private const uint ChannelCount = 4;

        public enum Registers
        {
            Control = 0x0, // CR
            Count = 0x4, // CNT
            ChannelControl0 = 0x10, // CCR0
            ChannelInterrupt0 = 0x14, // CIR0
            ChannelCompare0 = 0x18, // CMP0
            ChannelControl1 = 0x20, // CCR1
            ChannelInterrupt1 = 0x24, // CIR1
            ChannelCompare1 = 0x28, // CMP1
            ChannelControl2 = 0x30, // CCR2
            ChannelInterrupt2 = 0x34, // CIR2
            ChannelCompare2 = 0x38, // CMP2
            ChannelControl3 = 0x40, // CCR3
            ChannelInterrupt3 = 0x44, // CIR3
            ChannelCompare3 = 0x48 // CMP3
        }
    }
}
