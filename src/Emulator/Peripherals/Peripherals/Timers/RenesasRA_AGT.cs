//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasRA_AGT : BasicBytePeripheral
    {
        public RenesasRA_AGT(IMachine machine) : base(machine)
        {}

        protected override void DefineRegisters()
        {
            Registers.Counter.DefineMany(this, 2, (register, i) =>
                {
                    register.WithTag($"Counter [{(i+1)*8}:{i*8}]", 0, 8);
                },
                name: "Counter"
            );

            Registers.CompareMatchA.DefineMany(this, 2, (register, i) =>
                {
                    register.WithTag($"CompareMatchA [{(i+1)*8}:{i*8}]", 0, 8);
                },
                name: "CompareMatchA"
            );

            Registers.CompareMatchB.DefineMany(this, 2, (register, i) =>
                {
                    register.WithTag($"CompareMatchB [{(i+1)*8}:{i*8}]", 0, 8);
                },
                name: "CompareMatchB"
            );

            Registers.Control.Define(this)
                .WithTaggedFlag("Count Start (TSTART)", 0)
                .WithTaggedFlag("Count Status Flag (TCSTF)", 1)
                .WithTaggedFlag("Count Forces Stop (TSTOP)", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("Active Edge Judgment Flag (TEDGF)", 4)
                .WithTaggedFlag("Underflow Flag (TUNDF)", 5)
                .WithTaggedFlag("Compare Match A Flag (TCMAF)", 6)
                .WithTaggedFlag("Compare Match B Flag (TCMBF)", 7)
            ;

            Registers.Mode1.Define(this)
                .WithTag("Operating Mode (TMOD)", 0, 3)
                .WithTaggedFlag("Edge Polarity (TEDGPL)", 3)
                .WithTag("Count Source (TCK)", 4, 3)
                .WithReservedBits(7, 1)
            ;

            Registers.Mode2.Define(this)
                .WithTag("Source Clock Frequency Division Ratio (CKS)", 0, 3)
                .WithReservedBits(3, 4)
                .WithTaggedFlag("Low Power Mode (LPM)", 7)
            ;

            Registers.IOControl.Define(this)
                .WithTaggedFlag("I/O Polarity Switch (TEDGSEL)", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("AGTOn pin Output Enable (TOE)", 2)
                .WithReservedBits(3, 1)
                .WithTag("Input Filter (TIPF)", 4, 2)
                .WithTag("Count Control (TIOGT)", 6, 2)
            ;

            Registers.EventPinSelect.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("AGTEEn Polarity Selection (EEPS)", 2)
                .WithReservedBits(3, 5)
            ;

            Registers.CompareMatchFunctionSelect.Define(this)
                .WithTaggedFlag("Compare Match A Register Enable (TCMEA)", 0)
                .WithTaggedFlag("AGTOAn Pin Output Enable (TOEA)", 1)
                .WithTaggedFlag("AGTOAn Pin Polarity Select (TOPOLA)", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("Compare Match B Register Enable (TCMEB)", 4)
                .WithTaggedFlag("AGTOBn Pin Output Enable (TOEB)", 5)
                .WithTaggedFlag("AGTOBn Pin Polarity Select (TOPOLB)", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.PinSelect.Define(this)
                .WithTag("AGTIOn Pin Select (SEL)", 0, 2)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("AGTIOn Pin Input Enable (TIES)", 4)
                .WithReservedBits(5, 3)
            ;
        }

        public enum Registers
        {
            Counter                     = 0x00,
            CompareMatchA               = 0x02,
            CompareMatchB               = 0x04,
            Control                     = 0x08,
            Mode1                       = 0x09,
            Mode2                       = 0x0A,
            IOControl                   = 0x0C,
            EventPinSelect              = 0x0D,
            CompareMatchFunctionSelect  = 0x0E,
            PinSelect                   = 0x0F,
        }
    }
}
