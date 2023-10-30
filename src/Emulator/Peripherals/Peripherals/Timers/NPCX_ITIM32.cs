//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class NPCX_ITIM32 : BasicDoubleWordPeripheral, IKnownSize
    {
        public NPCX_ITIM32(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Prescaler.Define(this)
                .WithTag("PRE_8 (Prescaler Value)", 0, 8)
                .WithReservedBits(8, 24);

            Registers.ControlAndStatus.Define(this)
                .WithTag("TO_STS (Timeout Status)", 0, 1)
                .WithReservedBits(1, 1)
                .WithTag("TO_IE (Timeout Interrupt Enable)", 2, 1)
                .WithTag("TO_WUE (Timeout Wake-Up Enable)", 3, 1)
                .WithTag("CKSEL (Input Clock Select)", 4, 1)
                .WithReservedBits(5, 2)
                .WithTag("ITEN (ITIM32 Module Enable)", 7, 1)
                .WithReservedBits(8, 24);

            Registers.Counter.Define(this)
                .WithTag("CNT_32 (32-Bit Counter Value)", 0, 32);
        }

        private enum Registers
        {
            Prescaler = 0x1,
            ControlAndStatus = 0x4,
            Counter = 0x8
        }
    }
}
