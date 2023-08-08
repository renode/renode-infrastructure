//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MAX32650_PWRSEQ : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_PWRSEQ(IMachine machine, MAX32650_RTC rtc) : base(machine)
        {
            RTC = rtc;
            DefineRegisters();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x1800)
                .WithTag("LP_CTRL.ramret", 0, 2)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("LP_CTRL.rregen", 8)
                .WithTaggedFlag("LP_CTRL.bkgrnd", 9)
                .WithTaggedFlag("LP_CTRL.fwkm", 10)
                .WithTaggedFlag("LP_CTRL.bgoff", 11)
                .WithTaggedFlag("LP_CTRL.porvcoremd", 12)
                .WithReservedBits(13, 7)
                .WithFlag(20, name: "LP_CTRL.vcoremd",
                    valueProviderCallback: _ => (RTC.SubSecondsSignificantBits & 0x01) != 0x00)
                .WithFlag(21, name: "LP_CTRL.vrt(cmd",
                    valueProviderCallback: _ => (RTC.SubSecondsSignificantBits & 0x02) != 0x00)
                .WithFlag(22, name: "LP_CTRL.vdd(amd",
                    valueProviderCallback: _ => (RTC.SubSecondsSignificantBits & 0x04) != 0x00)
                .WithFlag(23, name: "LP_CTRL.vdd(iomd",
                    valueProviderCallback: _ => (RTC.SubSecondsSignificantBits & 0x08) != 0x00)
                .WithTaggedFlag("LP_CTRL.vddiohmd", 24)
                .WithReservedBits(25, 2)
                .WithTaggedFlag("LP_CTRL.vddbmd", 27)
                .WithReservedBits(28, 4);
        }

        private readonly MAX32650_RTC RTC;

        private enum Registers
        {
            Control = 0x00,
            GPIO0WakeupEnable = 0x04,
            GPIO0WakeupFlags = 0x08,
            GPIO1WakeupEnable = 0x0C,
            GPIO1WakeupFlags = 0x10,
            GPIO2WakeupEnable = 0x14,
            GPIO2WakeupFlags = 0x18,
            GPIO3WakeupEnable = 0x1C,
            GPIO3WakeupFlags = 0x20,
            USBWakeupStatus = 0x30,
            USBWakeupEnable = 0x34,
            RAMShutDownControl = 0x40,
        }
    }
}
