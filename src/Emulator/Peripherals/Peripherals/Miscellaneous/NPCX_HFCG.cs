//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NPCX_HFCG : BasicBytePeripheral, IKnownSize
    {
        public NPCX_HFCG(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public long Size => 0x100;

        protected override void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithTag("LOAD (Load M and N Values)", 0, 1)
                .WithReservedBits(1, 1)
                .WithTag("LOCK (Disable Writing to all HFCG registers)", 2, 1)
                .WithReservedBits(3, 3)
                .WithTag("CLK_CHNG (Clock Changing)", 7, 1);

            Registers.MLowByteValue.Define(this)
                .WithTag("HFCGM7-0 (M Value Bits 7-0)", 0, 8);

            Registers.MHighByteValue.Define(this)
                .WithTag("HFCGM15-8 (M Value Bits 15-8)", 0, 8);

            Registers.NValue.Define(this)
                .WithTag("HFCGN5-0 (N Value Bits 5-0)", 0, 6)
                .WithReservedBits(6, 1)
                .WithTag("XF_RANGE (Extended Frequency Range)", 7, 1);

            Registers.Prescaler.Define(this)
                .WithTag("AHB6DIV (AHB6 Clock Divider)", 0, 2)
                .WithReservedBits(2, 2)
                .WithTag("FPRED (Core Clock Prescaler Divider Value)", 4, 4);

            Registers.BusClockDividers.Define(this)
                .WithReservedBits(0, 1)
                .WithTag("AHB6CLK_BLK (AHB6 Clock Block)", 1, 1)
                .WithReservedBits(2, 2)
                .WithTag("FIUDIV (FIU Clock Divider)", 4, 2)
                .WithReservedBits(6, 2);

            Registers.BusClockDividers1.Define(this)
                .WithTag("APB1DIV (APB1 Clock Divider)", 0, 4)
                .WithTag("APB2DIV (APB2 Clock Divider)", 4, 4);

            Registers.BusClockDividers2.Define(this)
                .WithTag("APB3DIV (APB3 Clock Divider)", 0, 4)
                .WithTag("APB4DIV (APB4 Clock Divider)", 4, 4);

            Registers.PrescalerInIdle.Define(this)
                .WithReservedBits(0, 3)
                .WithTag("FPRED_IDL_EN (Core Clock Prescaler Divider in Idle Value Enable)", 3, 1)
                .WithTag("FPRED_IDL (Core Clock Prescaler Divider in Idle Value)", 4, 4);
        }

        private enum Registers
        {
            Control = 0x0,
            MLowByteValue = 0x2,
            MHighByteValue = 0x4,
            NValue = 0x6,
            Prescaler = 0x8,
            BusClockDividers = 0x10,
            BusClockDividers1 = 0x12,
            BusClockDividers2 = 0x14,
            PrescalerInIdle = 0x1C
        }
    }
}
