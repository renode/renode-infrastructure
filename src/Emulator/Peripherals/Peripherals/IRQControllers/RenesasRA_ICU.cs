//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class RenesasRA_ICU : BasicDoubleWordPeripheral, INumberedGPIOOutput, IIRQController
    {
        public RenesasRA_ICU(IMachine machine, uint interruptsCount) : base(machine)
        {
            this.interruptCount = interruptsCount;

            DefineRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            Registers.IRQControl0.DefineMany(this, interruptCount, (register, registerIndex) =>
            {
                register
                    .WithTag($"IRQMD{registerIndex}", 0, 2)
                    .WithReservedBits(2, 2)
                    .WithTag($"FCLKSEL{registerIndex}", 4, 2)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag($"FLTEN{registerIndex}", 7)
                    .WithReservedBits(8, 24)
                ;
            });

            Registers.NMIPinInterruptControl.Define(this)
                .WithTaggedFlag("NMIMD", 0)
                .WithReservedBits(1, 3)
                .WithTag("NFCLKSEL", 4, 2)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("NFLTEN", 7)
                .WithReservedBits(8, 24)
                ;

            Registers.NonMaskableInterruptEnable.Define(this)
                .WithTaggedFlag("IWDTEN", 0)
                .WithTaggedFlag("WDTEN", 1)
                .WithTaggedFlag("LVD1EN", 2)
                .WithTaggedFlag("LVD2EN", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTEN", 6)
                .WithTaggedFlag("NMIEN", 7)
                .WithTaggedFlag("RPEEN", 8)
                .WithTaggedFlag("RECCEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMEN", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFEN", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPEEN", 15)
                .WithReservedBits(16, 16)
                ;

            Registers.NonMaskableInterruptStatusClear.Define(this)
                .WithTaggedFlag("IWDTCLR", 0)
                .WithTaggedFlag("WDTCLR", 1)
                .WithTaggedFlag("LVD1CLR", 2)
                .WithTaggedFlag("LVD2CLR", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTCLR", 6)
                .WithTaggedFlag("NMICLR", 7)
                .WithTaggedFlag("RPECLR", 8)
                .WithTaggedFlag("RECCCLR", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMCLR", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFCLR", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPECLR", 15)
                .WithReservedBits(16, 16)
                ;

            Registers.NonMaskableInterruptStatus.Define(this)
                .WithTaggedFlag("IWDTST", 0)
                .WithTaggedFlag("WDTST", 1)
                .WithTaggedFlag("LVD1ST", 2)
                .WithTaggedFlag("LVD2ST", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTST", 6)
                .WithTaggedFlag("NMIST", 7)
                .WithTaggedFlag("RPEST", 8)
                .WithTaggedFlag("RECCST", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMST", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFST", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPEST", 15)
                .WithReservedBits(16, 16)
                ;

            Registers.WakeUpInterruptEnable0.Define(this)
                .WithTag("IRQWUPEN", 0, 16)
                .WithTaggedFlag("IWDTWUPEN", 16)
                .WithReservedBits(17, 1)
                .WithTaggedFlag("LVD1WUPEN", 18)
                .WithTaggedFlag("LVD2WUPEN", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("RTCALMWUPEN", 24)
                .WithTaggedFlag("RTCPRDWUPEN", 25)
                .WithTaggedFlag("USBHSWUPEN", 26)
                .WithTaggedFlag("USBFS0WUPEN", 27)
                .WithTaggedFlag("AGT1UDWUPEN", 28)
                .WithTaggedFlag("AGT1CAWUPEN", 29)
                .WithTaggedFlag("AGT1CBWUPEN", 30)
                .WithTaggedFlag("IIC0WUPEN", 31)
                ;

            Registers.WakeUpinterruptenableregister1.Define(this)
                .WithTaggedFlag("AGT3UDWUPEN", 0)
                .WithTaggedFlag("AGT3CAWUPEN", 1)
                .WithTaggedFlag("AGT3CBWUPEN", 2)
                .WithReservedBits(3, 29)
                ;

            Registers.SYSEventLinkSetting.Define(this)
                .WithTag("SELSR0", 0, 16)
                .WithReservedBits(16, 16)
                ;

            Registers.DMACEventLinkSetting0.DefineMany(this, interruptCount, (register, registerIndex) =>
            {
                register
                .WithTag($"DELS{registerIndex}", 0, 9)
                .WithReservedBits(9, 7)
                .WithTaggedFlag($"IR{registerIndex}", 16)
                .WithReservedBits(17, 15)
                ;
            });

            Registers.ICUEventLinkSetting0.DefineMany(this, interruptCount, (register, registerIndex) =>
            {
                register
                    .WithTag($"IELSR{registerIndex}", 0, 32)
                    ;
            });
        }

        private readonly uint interruptCount;

        private enum Registers
        {
            IRQControl0 = 0x0,
            NMIPinInterruptControl = 0x100,
            NonMaskableInterruptEnable = 0x120,
            NonMaskableInterruptStatusClear = 0x130,
            NonMaskableInterruptStatus = 0x140,
            WakeUpInterruptEnable0 = 0x1a0,
            WakeUpinterruptenableregister1 = 0x1a4,
            SYSEventLinkSetting = 0x200,
            DMACEventLinkSetting0 = 0x280,
            ICUEventLinkSetting0 = 0x300,
        }
    }
}
