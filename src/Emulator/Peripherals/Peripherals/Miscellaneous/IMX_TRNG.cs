//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class IMX_TRNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMX_TRNG(IMachine machine) : base(machine)
        {
            rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
        }

        public long Size => 0x4000;

        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Registers.MiscControl.Define(this)
                .WithTag("SAMP_MODE", 0, 2)
                .WithTag("OSC_DIV", 2, 2)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("RST_DEF", 6)
                .WithTaggedFlag("FOR_SCLK", 7)
                .WithTaggedFlag("FCT_FAIL", 8)
                .WithTaggedFlag("FCT_VAL", 9)
                .WithFlag(10, name: "ENT_VAL", valueProviderCallback: _ => true)
                .WithTaggedFlag("TST_OUT", 11)
                .WithFlag(12, name: "ERR", valueProviderCallback: _ => false)
                .WithTaggedFlag("TSTOP_OK", 13)
                .WithTaggedFlag("LRUN_CONT", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("PRGM", 16)
                .WithReservedBits(17, 15)
            ;

            Registers.StatCheckMisc.Define(this)
                .WithTag("LRUN_MAX", 0, 8)
                .WithReservedBits(8, 8)
                .WithTag("RTY_CT", 16, 4)
            ;

            Registers.PokerRange.Define(this)
                .WithTag("PKR_RNG", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.SeedControl.Define(this)
                .WithTag("SAMP_SIZE", 0, 16)
                .WithTag("ENT_DLY", 16, 16)
            ;

            Registers.Entropy.DefineMany(this, 16, (register, idx) =>
            {
                var j = idx;
                register
                    .WithValueField(0, 32, name: $"ENT{j}",
                        valueProviderCallback: _ => (uint)rng.Next())
                ;
            });
        }

        private readonly PseudorandomNumberGenerator rng;

        private enum Registers
        {
            MiscControl = 0x0,
            StatCheckMisc = 0x4,
            PokerRange = 0x8,
            // Poker Maximum Limit / Poker Square Calculation Result
            PKRMAX_PKRSQ = 0xC,
            SeedControl = 0x10,
            // Sparse Bit Limit / Total Samples
            SBLIM_TOTSAM = 0x14,
            FreqCountMin = 0x18,
            // Frequency Count / Frequency Count Maximum Limit
            FRQCNT_FRQMAX = 0x1C,
            // Statistical Check Monobit Count / Limit
            SCMC_SCML = 0x20,
            // Statistical Check Run Length 1 Count / Limit
            SCR1C_SCR1L = 0x24,
            // Statistical Check Run Length 2 Count / Limit
            SCR2C_SCR2L = 0x28,
            // Statistical Check Run Length 3 Count / Limit
            SCR3C_SCR3L = 0x2C,
            // Statistical Check Run Length 4 Count / Limit
            SCR4C_SCR4L = 0x30,
            // Statistical Check Run Length 5 Count / Limit
            SCR5C_SCR5L = 0x34,
            // Statistical Check Run Length 6 Count / Limit
            SCR6C_SCR6L = 0x38,
            Status = 0x3C,
            Entropy  = 0x40,
            // Statistical Check Poker Count 1 and 0
            PKRCNT10 = 0x80,
            // Statistical Check Poker Count 3 and 2
            PKRCNT32 = 0x84,
            // Statistical Check Poker Count 5 and 4
            PKRCNT54 = 0x88,
            // Statistical Check Poker Count 7 and 6
            PKRCNT76 = 0x8C,
            // Statistical Check Poker Count 9 and 8
            PKRCNT98 = 0x90,
            // Statistical Check Poker Count B and A
            PKRCNTBA = 0x94,
            // Statistical Check Poker Count D and C
            PKRCNTDC = 0x98,
            // Statistical Check Poker Count F and E
            PKRCNTFE = 0x9C,
            SecurityConfig = 0xA0,
            InterruptControl = 0xA4,
            InterruptMask = 0xA8,
            InterruptStatus = 0xAC,
            VersionID1 = 0xF0,
            VersionID2 = 0xF4,
        }
    }
}
