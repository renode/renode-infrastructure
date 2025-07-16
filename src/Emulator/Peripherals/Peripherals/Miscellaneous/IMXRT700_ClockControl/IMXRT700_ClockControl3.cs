//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public partial class IMXRT700_ClockControl
    {
        private DoubleWordRegisterCollection DefineInstance3Registers()
        {
            var collection = new DoubleWordRegisterCollection(this);
            Instance3Registers.VDD1_SENSEMainClockControl0.Define(collection, 0xF02000)
                .WithValueField(0, 32, out control0);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance3Registers.VDD1_SENSERAMClockControl0Set.Define(collection)
                .WithTaggedFlag("CPU1", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MU0", 4)
                .WithTaggedFlag("MU1", 5)
                .WithTaggedFlag("MU2", 6)
                .WithTaggedFlag("OSTIMER", 7)
                .WithTaggedFlag("SEMA42_0", 8)
                .WithTaggedFlag("SDADC0", 9)
                .WithTaggedFlag("SARADC0", 10)
                .WithTaggedFlag("ACMP0", 11)
                .WithTaggedFlag("MICFIL", 12)
                .WithTaggedFlag("GLIKEY4", 13)
                .WithReservedBits(14, 6)
                .WithTaggedFlag("DBG_RT700", 20)
                .WithTaggedFlag("SYSCON3", 21)
                .WithTaggedFlag("IOPCTL1", 22)
                .WithTaggedFlag("GLIKEY1", 23)
                .WithTaggedFlag("LPI2C15", 24)
                .WithTaggedFlag("MEDIA_ACCESS_RAM_ARBITER1", 25)
                .WithReservedBits(26, 6)
                .WithWriteCallback((_, val) => { control0.Value |= val; });

            Instance3Registers.VDD1_SENSERAMClockControl0Clear.Define(collection)
                .WithTaggedFlag("CPU1", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MU0", 4)
                .WithTaggedFlag("MU1", 5)
                .WithTaggedFlag("MU2", 6)
                .WithTaggedFlag("OSTIMER", 7)
                .WithTaggedFlag("SEMA42_0", 8)
                .WithTaggedFlag("SDADC0", 9)
                .WithTaggedFlag("SARADC0", 10)
                .WithTaggedFlag("ACMP0", 11)
                .WithTaggedFlag("MICFIL", 12)
                .WithTaggedFlag("GLIKEY4", 13)
                .WithReservedBits(14, 6)
                .WithTaggedFlag("DBG_RT700", 20)
                .WithTaggedFlag("SYSCON3", 21)
                .WithTaggedFlag("IOPCTL1", 22)
                .WithTaggedFlag("GLIKEY1", 23)
                .WithTaggedFlag("LPI2C15", 24)
                .WithTaggedFlag("MEDIA_ACCESS_RAM_ARBITER1", 25)
                .WithReservedBits(26, 6)
                .WithWriteCallback((_, val) => { control0.Value &= ~val; });

            Instance3Registers.OneSourceClockSliceEnable.Define(collection)

                .WithFlag(0, name: "RTC_FCLK_EN")
                .WithTaggedFlag("dGDET2_FCLK_EN", 1)
                .WithTaggedFlag("dGDET3_FCLK_EN", 2)
                .WithReservedBits(3, 29);

            Instance3Registers.LowPowerOscillatorControl0.Define(collection, 0x807BC4D4)
                .WithReservedBits(0, 31)
                .WithTaggedFlag("CLKRDY", 31);

            Instance3Registers.VDD1_SENSEBaseClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance3Registers.FRO_TUNER2ClockStatus.Define(collection)
                .WithFlag(0, valueProviderCallback: _ => true, name: "CLK_OK")
                .WithReservedBits(1, 31);

            Instance3Registers.FRO2MAXClockDomainEnable.Define(collection, 0x7F)
                .WithTaggedFlag("FRO2MAX_OF_CMPT", 0)
                .WithTaggedFlag("FRO2MAX_OF_SENSE", 1)
                .WithTaggedFlag("FRO2MAX_OF_VDD2_DSP", 2)
                .WithTaggedFlag("FRO2MAX_OF_MD2", 3)
                .WithTaggedFlag("FRO2MAX_OF_MDN", 4)
                .WithTaggedFlag("FRO2MAX_OF_VDD2_COM", 5)
                .WithTaggedFlag("FRO2MAX_OF_COMN", 6)
                .WithReservedBits(7, 25);

            Instance3Registers.VDD1_SENSEMainClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.VDD1_SENSEMainClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance3Registers.VDD1_SENSERAMClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance3Registers.VDD1_SENSERAMClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.OSTIMERFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.OSTIMERFunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.SDADCFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.SDADCFunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.ADC0FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.ADC0FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.Wake32kHZClockSourceSelect.Define(collection, 0x1)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance3Registers.Wake32kHZClockDivider.Define(collection, 0x1F)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.MICFILFunctionalClockSourceSelect.Define(collection, 0x4)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.MICFILFunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.LPI2C15FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.LPI2C15FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.CLKOUT_VDD1ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance3Registers.CLKOUT_VDD1ClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance3Registers.VDD1_SENSEPeripheralClockControl0.Define(collection, 0xF02000)
                .WithValueField(0, 32, out control1);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance3Registers.VDD1_SENSEPeripheralClockControl0Set.Define(collection)
                .WithTaggedFlag("CPU1", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MU0", 4)
                .WithTaggedFlag("MU1", 5)
                .WithTaggedFlag("MU2", 6)
                .WithTaggedFlag("OSTIMER", 7)
                .WithTaggedFlag("SEMA42_0", 8)
                .WithTaggedFlag("SDADC0", 9)
                .WithTaggedFlag("SARADC0", 10)
                .WithTaggedFlag("ACMP0", 11)
                .WithTaggedFlag("MICFIL", 12)
                .WithTaggedFlag("GLIKEY4", 13)
                .WithReservedBits(14, 6)
                .WithTaggedFlag("DBG_RT700", 20)
                .WithTaggedFlag("SYSCON3", 21)
                .WithTaggedFlag("IOPCTL1", 22)
                .WithTaggedFlag("GLIKEY1", 23)
                .WithTaggedFlag("LPI2C15", 24)
                .WithTaggedFlag("MEDIA_ACESS_RAM_ARBITER1", 25)
                .WithReservedBits(26, 6)
                .WithWriteCallback((_, val) => { control1.Value |= val; });

            Instance3Registers.VDD1_SENSEPeripheralClockControl0Clear.Define(collection)
                .WithTaggedFlag("CPU1", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MU0", 4)
                .WithTaggedFlag("MU1", 5)
                .WithTaggedFlag("MU2", 6)
                .WithTaggedFlag("OSTIMER", 7)
                .WithTaggedFlag("SEMA42_0", 8)
                .WithTaggedFlag("SDADC0", 9)
                .WithTaggedFlag("SARADC0", 10)
                .WithTaggedFlag("ACMP0", 11)
                .WithTaggedFlag("MICFIL", 12)
                .WithTaggedFlag("GLIKEY4", 13)
                .WithReservedBits(14, 6)
                .WithTaggedFlag("DBG_RT700", 20)
                .WithTaggedFlag("SYSCON3", 21)
                .WithTaggedFlag("IOPCTL1", 22)
                .WithTaggedFlag("GLIKEY1", 23)
                .WithTaggedFlag("LPI2C15", 24)
                .WithTaggedFlag("MEDIA_ACCESS_RAM_ARBITER1", 25)
                .WithReservedBits(26, 6)
                .WithWriteCallback((_, val) => { control1.Value &= ~val; });

            Instance3Registers.OneSourceClockSliceEnableForVVD1_SENSE.Define(collection)
                .WithFlag(0, valueProviderCallback: (_) => true, name: "RTC_FCLK_EN")
                .WithTaggedFlag("dGDET2_FCLK_EN", 1)
                .WithTaggedFlag("dGDET3_FCLK_EN", 2)
                .WithReservedBits(3, 29);
            return collection;
        }

        private enum Instance3Registers
        {
            VDD1_SENSEMainClockControl0 = 0x10,
            VDD1_SENSERAMClockControl0Set = 0x40,
            VDD1_SENSERAMClockControl0Clear = 0x70,
            OneSourceClockSliceEnable = 0x90,
            LowPowerOscillatorControl0 = 0x210,
            VDD1_SENSEBaseClockSelectSource = 0x214,
            FRO_TUNER2ClockStatus = 0x290,
            FRO2MAXClockDomainEnable = 0x298,
            VDD1_SENSEMainClockDivider = 0x400,
            VDD1_SENSEMainClockSourceSelect = 0x434,
            VDD1_SENSERAMClockSourceSelect = 0x450,
            VDD1_SENSERAMClockDivider = 0x45C,
            OSTIMERFunctionalClockSourceSelect = 0x480,
            OSTIMERFunctionalClockDivider = 0x484,
            SDADCFunctionalClockSourceSelect = 0x600,
            SDADCFunctionalClockDivider = 0x604,
            ADC0FunctionalClockSourceSelect = 0x620,
            ADC0FunctionalClockDivider = 0x624,
            Wake32kHZClockSourceSelect = 0x750,
            Wake32kHZClockDivider = 0x754,
            MICFILFunctionalClockSourceSelect = 0x780,
            MICFILFunctionalClockDivider = 0x784,
            LPI2C15FunctionalClockSourceSelect = 0x788,
            LPI2C15FunctionalClockDivider = 0x78C,
            CLKOUT_VDD1ClockSourceSelect = 0x800,
            CLKOUT_VDD1ClockDivider = 0x804,
            VDD1_SENSEPeripheralClockControl0 = 0x810,
            VDD1_SENSEPeripheralClockControl0Set = 0x840,
            VDD1_SENSEPeripheralClockControl0Clear = 0x870,
            OneSourceClockSliceEnableForVVD1_SENSE = 0x890,
        }
    }
}
