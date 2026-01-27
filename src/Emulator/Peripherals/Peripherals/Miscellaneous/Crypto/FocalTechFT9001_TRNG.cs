//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class FocalTechFT9001_TRNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public FocalTechFT9001_TRNG(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.CONTROL.Define(this)
                .WithTag("Clock prescaler (CLK_DIV)", 0, 8)
                .WithTaggedFlag("TRNG enable (EN)", 8)
                .WithTaggedFlag("Interrupt enable (EN_IT)", 9)
                .WithFlag(10, name: "Clear interrupt (CLR_IT)", mode: FieldMode.WriteOneToClear) // Driver sets this after reading the Data register
                .WithFlag(11, name: "Interrupt (IT)", valueProviderCallback: _ => true) // Driver waits until this is set before reading the Data register
                .WithReservedBits(12, 4)
                .WithTag("Analog model enable (EN_ANA)", 16, 4)
                .WithTag("Analog model reset (RST_ANA)", 20, 4)
                .WithReservedBits(24, 8);

            Registers.DATA.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => (ulong)random.Next());
        }

        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        // Based on https://github.com/focaltech-3545/zephyr-ft9001-public/blob/f982a55e89fe5584982e37ae2b6bd0f22f8d7816/focaltech/hal/ft/ft90/ft9001/standard_peripheral/source/drv/inc/trng_reg.h
        // No public documentation is available, therefore register names are taken directly from the HAL headers.
        private enum Registers
        {
            CONTROL = 0x00,
            DATA = 0x04,

            TMCTRL = 0x08,
            STSCR = 0x0C,

            OSCR_Mx0 = 0x10,
            OSCR_Mx1 = 0x14,
            OSCR_Mx2 = 0x18,
            OSCR_Mx3 = 0x1C,

            RESERVED0 = 0x20,
            RESERVED1 = 0x24,
            RESERVED2 = 0x28,
            RESERVED3 = 0x2C,
            RESERVED4 = 0x30,
            RESERVED5 = 0x34,
            RESERVED6 = 0x38,
            RESERVED7 = 0x3C,
            RESERVED8 = 0x40,
            RESERVED9 = 0x44,

            SM3DRx0 = 0x48,
            SM3DRx1 = 0x4C,
            SM3DRx2 = 0x50,
            SM3DRx3 = 0x54,
            SM3DRx4 = 0x58,
            SM3DRx5 = 0x5C,
            SM3DRx6 = 0x60,
            SM3DRx7 = 0x64,

            OSCTRIMR1 = 0x68,
            OSCTRIMR2 = 0x6C,

            OSCCTR = 0x70,
            OSCDIVR = 0x74,
        }
    }
}
