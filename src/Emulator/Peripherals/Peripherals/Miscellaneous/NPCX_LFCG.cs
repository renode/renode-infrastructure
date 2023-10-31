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

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToWord | AllowedTranslation.WordToByte)]
    public class NPCX_LFCG : BasicWordPeripheral, IKnownSize
    {
        public NPCX_LFCG(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithReservedBits(0, 2)
                .WithTag("LREFEN (LPC Clock Reference Enable)", 2, 1)
                .WithTag("LFLER (LFCG Locked on External Reference)", 3, 1)
                .WithTag("UDCP (Update Calibration Parameters)", 4, 1)
                .WithTag("LFLOC (LFCG Register Write Lock)", 5, 1)
                .WithReservedBits(6, 1)
                .WithTag("XTCLK_VAL (XTOSC Clock Valid)", 7, 1)
                .WithReservedBits(8, 8);

            Registers.HighFrequencyReferenceDivisorI.Define(this)
                .WithTag("HFRDI (High-Frequency Reference Divisor I)", 0, 12).
                WithReservedBits(12, 4);

            Registers.HighFrequencyReferenceDivisorF.Define(this)
                .WithTag("HFRDF (High-Frequency Reference Divisor F)", 0, 16);

            Registers.ClockDivisor.Define(this)
                .WithTag("FRCDIV (FRCLK Clock Divisor)", 0, 8)
                .WithReservedBits(8, 8);

            Registers.DivisorCorrectionValue1.Define(this)
                .WithTag("DIVCOR1 (Divisor Correction Value 1)", 0, 8)
                .WithReservedBits(8, 8);

            Registers.DivisorCorrectionValue2.Define(this)
                .WithTag("DIVCOR2 (Divisor Correction Value 2)", 0, 8)
                .WithReservedBits(8, 8);

            Registers.Control2.Define(this)
                .WithReservedBits(0, 3)
                .WithTag("AUDP_EN (Automatic Update Enable)", 3, 1)
                .WithTag("STOPCAL (Stop Calibration to External Reference)", 4, 1)
                .WithTag("FCLKRUN (Force PCI_CLK Running)", 5, 1)
                .WithTag("XT_OSC_SL_EN (Crystal Oscillator Selection Enable)", 6, 1)
                .WithReservedBits(7, 1)
                .WithReservedBits(8, 8);
        }

        private enum Registers
        {
            Control = 0x0,
            HighFrequencyReferenceDivisorI = 0x2,
            HighFrequencyReferenceDivisorF = 0x4,
            ClockDivisor = 0x6,
            DivisorCorrectionValue1 = 0x8,
            DivisorCorrectionValue2 = 0xA,
            Control2 = 0x14
        }
    }
}
