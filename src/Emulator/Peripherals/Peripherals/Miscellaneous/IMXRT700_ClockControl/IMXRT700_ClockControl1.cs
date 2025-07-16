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
        private DoubleWordRegisterCollection DefineInstance1Registers()
        {
            var collection = new DoubleWordRegisterCollection(this);
            Instance1Registers.VDD1_SENSEPeripheralClockControl0.Define(collection, 0xC0)
                .WithValueField(0, 32, out control0);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance1Registers.VDD1_SENSEPeripheralClockControl1.Define(collection)
                .WithValueField(0, 32, out control1);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance1Registers.VDD1_SENSEPeripheralClockControl0Set.Define(collection)
                .WithReservedBits(0, 6)
                .WithTaggedFlag("SLEEPCON1", 6)
                .WithTaggedFlag("SYSCON1", 7)
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, val) => { control0.Value |= val; });

            Instance1Registers.VDD1_SENSEPeripheralClockControl1Set.Define(collection)
                .WithTaggedFlag("SENSE_ACCESS_RAM_ARBITER0", 0)
                .WithTaggedFlag("HiFi1", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("eDMA2", 4)
                .WithTaggedFlag("eDMA3", 5)
                .WithTaggedFlag("LP_FLEXCOMM17", 6)
                .WithTaggedFlag("LP_FLEXCOMM18", 7)
                .WithTaggedFlag("LP_FLEXCOMM19", 8)
                .WithTaggedFlag("LP_FLEXCOMM20", 9)
                .WithTaggedFlag("SAI3", 10)
                .WithTaggedFlag("I3C2", 11)
                .WithTaggedFlag("I3C3", 12)
                .WithTaggedFlag("GPIO8", 13)
                .WithTaggedFlag("GPIO9", 14)
                .WithTaggedFlag("GPIO10", 15)
                .WithTaggedFlag("PINT1", 16)
                .WithTaggedFlag("CTIMER5", 17)
                .WithTaggedFlag("CTIMER6", 18)
                .WithTaggedFlag("CTIMER7", 19)
                .WithTaggedFlag("MRT1", 20)
                .WithTaggedFlag("UTICK1", 21)
                .WithTaggedFlag("CDOG3", 22)
                .WithTaggedFlag("CDOG4", 23)
                .WithTaggedFlag("MU3", 24)
                .WithTaggedFlag("SEMA42_3", 25)
                .WithTaggedFlag("WWDT2", 26)
                .WithTaggedFlag("WWDT3", 27)
                .WithReservedBits(28, 2)
                .WithTaggedFlag("INPUTMUX1", 30)
                .WithReservedBits(31, 1)
                .WithWriteCallback((_, val) => { control1.Value |= val; });

            Instance1Registers.VDD1_SENSEPeripheralClockControl0Clear.Define(collection)
                .WithReservedBits(0, 6)
                .WithTaggedFlag("SLEEPCON1", 6)
                .WithTaggedFlag("SYSCON1", 7)
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, val) => { control0.Value &= ~val; });

            Instance1Registers.VDD1_SENSEPeripheralClockControl1Clear.Define(collection)
                .WithTaggedFlag("SENSE_ACCESS_RAM_ARBITER0", 0)
                .WithTaggedFlag("HiFi1", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("eDMA2", 4)
                .WithTaggedFlag("eDMA3", 5)
                .WithTaggedFlag("LP_FLEXCOMM17", 6)
                .WithTaggedFlag("LP_FLEXCOMM18", 7)
                .WithTaggedFlag("LP_FLEXCOMM19", 8)
                .WithTaggedFlag("LP_FLEXCOMM20", 9)
                .WithTaggedFlag("SAI3", 10)
                .WithTaggedFlag("I3C2", 11)
                .WithTaggedFlag("I3C3", 12)
                .WithTaggedFlag("GPIO8", 13)
                .WithTaggedFlag("GPIO9", 14)
                .WithTaggedFlag("GPIO10", 15)
                .WithTaggedFlag("PINT1", 16)
                .WithTaggedFlag("CTIMER5", 17)
                .WithTaggedFlag("CTIMER6", 18)
                .WithTaggedFlag("CTIMER7", 19)
                .WithTaggedFlag("MRT1", 20)
                .WithTaggedFlag("UTICK1", 21)
                .WithTaggedFlag("CDOG3", 22)
                .WithTaggedFlag("CDOG4", 23)
                .WithTaggedFlag("MU3", 24)
                .WithTaggedFlag("SEMA42_3", 25)
                .WithTaggedFlag("WWDT2", 26)
                .WithTaggedFlag("WWDT3", 27)
                .WithReservedBits(28, 2)
                .WithTaggedFlag("INPUTMUX1", 30)
                .WithReservedBits(31, 1)
                .WithWriteCallback((_, val) => { control1.Value &= ~val; });

            Instance1Registers.VDD1_SENSEBaseClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance1Registers.CPUClockofDSPinVDD1_SENSEClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.CPUClockofDSPinVDD1_SENSEClockSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.SAI3ClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.SAI3FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.UTICK1FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.UTICK1FunctionalClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.WWDT2FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.WWDT3FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.SYSTICKFunctionalClockSelectSource.Define(collection, 0x4)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.SYSTICKFunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.CTIMERindexFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.CTIMER5FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.CTIMER6FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.CTIMER7FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.I3C2AndI3C3FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.I3C2AndI3C3FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.LP_FLEXCOMM17ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.LP_FLEXCOMM17ClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.LP_FLEXCOMM18ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.LP_FLEXCOMM18ClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.LP_FLEXCOMM19ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.LP_FLEXCOMM19ClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.LP_FLEXCOMM20ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance1Registers.LP_FLEXCOMM20ClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance1Registers.VDD1_SENSEAudioClockSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);
            return collection;
        }

        private enum Instance1Registers
        {
            VDD1_SENSEPeripheralClockControl0 = 0x10,
            VDD1_SENSEPeripheralClockControl1 = 0x14,
            VDD1_SENSEPeripheralClockControl0Set = 0x40,
            VDD1_SENSEPeripheralClockControl1Set = 0x44,
            VDD1_SENSEPeripheralClockControl0Clear = 0x70,
            VDD1_SENSEPeripheralClockControl1Clear = 0x74,
            VDD1_SENSEBaseClockSelectSource = 0x438,
            CPUClockofDSPinVDD1_SENSEClockDivider = 0x440,
            CPUClockofDSPinVDD1_SENSEClockSelect = 0x444,
            SAI3ClockSelectSource = 0x500,
            SAI3FunctionalClockDivider = 0x504,
            UTICK1FunctionalClockSourceSelect = 0x700,
            UTICK1FunctionalClockDivider = 0x704,
            WWDT2FunctionalClockSourceSelect = 0x720,
            WWDT3FunctionalClockSourceSelect = 0x740,
            SYSTICKFunctionalClockSelectSource = 0x760,
            SYSTICKFunctionalClockDivider = 0x764,
            CTIMERindexFunctionalClockSourceSelect = 0x7A0,
            CTIMER5FunctionalClockDivider = 0x7B0,
            CTIMER6FunctionalClockDivider = 0x7B4,
            CTIMER7FunctionalClockDivider = 0x7B8,
            I3C2AndI3C3FunctionalClockSourceSelect = 0x800,
            I3C2AndI3C3FunctionalClockDivider = 0x810,
            LP_FLEXCOMM17ClockSourceSelect = 0xA00,
            LP_FLEXCOMM17ClockDivider = 0xA04,
            LP_FLEXCOMM18ClockSourceSelect = 0xA20,
            LP_FLEXCOMM18ClockDivider = 0xA24,
            LP_FLEXCOMM19ClockSourceSelect = 0xA40,
            LP_FLEXCOMM19ClockDivider = 0xA44,
            LP_FLEXCOMM20ClockSourceSelect = 0xA60,
            LP_FLEXCOMM20ClockDivider = 0xA64,
            VDD1_SENSEAudioClockSource = 0xAA0,
        }
    }
}
