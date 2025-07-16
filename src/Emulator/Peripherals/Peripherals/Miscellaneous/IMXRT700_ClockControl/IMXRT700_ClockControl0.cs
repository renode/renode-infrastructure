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
        private DoubleWordRegisterCollection DefineInstance0Registers()
        {
            var collection = new DoubleWordRegisterCollection(this);

            Instance0Registers.VDD2_COMPPeripheralClockControl0.Define(collection, 0xF006)
                .WithValueField(0, 32, out control0);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl1.Define(collection, 0x200)
                .WithValueField(0, 32, out control1);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl2.Define(collection)
                .WithValueField(0, 32, out control2);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl3.Define(collection, 0xF8000)
                .WithValueField(0, 32, out control3);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl4.Define(collection, 0x1)
                .WithValueField(0, 32, out control4);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl5.Define(collection, 0x8)
                .WithValueField(0, 32, out control5);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance0Registers.VDD2_COMPPeripheralClockControl0Set.Define(collection)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("CODE_CACHE", 1)
                .WithTaggedFlag("SYSTEM_CACHE", 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("VDD2_OTP0", 5)
                .WithReservedBits(6, 6)
                .WithTaggedFlag("SLEEPCON0", 12)
                .WithTaggedFlag("SYSCON0", 13)
                .WithTaggedFlag("GLIKEY0", 14)
                .WithTaggedFlag("GLIKEY3", 15)
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, val) => { control0.Value = val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl1Set.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("TPIU_TRACE_CLKIN", 2)
                .WithTaggedFlag("SWO_TRACECLKIN", 3)
                .WithTaggedFlag("TSCLK", 4)
                .WithTaggedFlag("eDMA0", 5)
                .WithTaggedFlag("eDMA1", 6)
                .WithTaggedFlag("PKC_RAM_CTRl", 7)
                .WithTaggedFlag("PKC", 8)
                .WithTaggedFlag("ROMCP", 9)
                .WithTaggedFlag("XSPI0", 10)
                .WithTaggedFlag("XSPI1", 11)
                .WithTaggedFlag("CACHE64_0", 12)
                .WithTaggedFlag("CACHE64_1", 13)
                .WithTaggedFlag("QK_SUBSYS", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("MMU0", 16)
                .WithTaggedFlag("MMU1", 17)
                .WithTaggedFlag("GPIO0", 18)
                .WithTaggedFlag("GPIO1", 19)
                .WithTaggedFlag("GPIO2", 20)
                .WithTaggedFlag("GPIO3", 21)
                .WithTaggedFlag("GPIO4", 22)
                .WithTaggedFlag("GPIO5", 23)
                .WithTaggedFlag("GPIO6", 24)
                .WithTaggedFlag("GPIO7", 25)
                .WithTaggedFlag("SCT0", 26)
                .WithTaggedFlag("CDOG0", 27)
                .WithTaggedFlag("CDOG1", 28)
                .WithTaggedFlag("CDOG2", 29)
                .WithTaggedFlag("LP_FLEXCOMM0", 30)
                .WithTaggedFlag("LP_FLEXCOMM1", 31)
                .WithWriteCallback((_, val) => { control1.Value = val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl2Set.Define(collection)
                .WithTaggedFlag("LP_FLEXCOMM2", 0)
                .WithTaggedFlag("LP_FLEXCOMM3", 1)
                .WithTaggedFlag("LP_FLEXCOMM4", 2)
                .WithTaggedFlag("LP_FLEXCOMM5", 3)
                .WithTaggedFlag("LP_FLEXCOMM6", 4)
                .WithTaggedFlag("LP_FLEXCOMM7", 5)
                .WithTaggedFlag("LP_FLEXCOMM8", 6)
                .WithTaggedFlag("LP_FLEXCOMM9", 7)
                .WithTaggedFlag("LP_FLEXCOMM10", 8)
                .WithTaggedFlag("LP_FLEXCOMM11", 9)
                .WithTaggedFlag("LP_FLEXCOMM12", 10)
                .WithTaggedFlag("LP_FLEXCOMM13", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("SAI0", 13)
                .WithTaggedFlag("SAI1", 14)
                .WithTaggedFlag("SAI2", 15)
                .WithTaggedFlag("I3C0", 16)
                .WithTaggedFlag("I3C1", 17)
                .WithTaggedFlag("CRC0", 18)
                .WithTaggedFlag("WWDT0", 19)
                .WithTaggedFlag("WWDT1", 20)
                .WithTaggedFlag("CTIMER0", 21)
                .WithTaggedFlag("CTIMER1", 22)
                .WithTaggedFlag("CTIMER2", 23)
                .WithTaggedFlag("CTIMER3", 24)
                .WithTaggedFlag("CTIMER4", 25)
                .WithTaggedFlag("MRT0", 26)
                .WithTaggedFlag("UTICK0", 27)
                .WithReservedBits(28, 2)
                .WithTaggedFlag("SEMA42_4", 30)
                .WithTaggedFlag("MU4", 31)
                .WithWriteCallback((_, val) => { control2.Value = val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl3Set.Define(collection)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("PINT0", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("FREQME0", 8)
                .WithReservedBits(9, 1)
                .WithTaggedFlag("INPUTMUX0", 10)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("SAFO_SGI", 12)
                .WithTaggedFlag("TRACE", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("PRINCE0", 15)
                .WithTaggedFlag("PRINCE1", 16)
                .WithTaggedFlag("XSPI1_PRINCE_XEX", 17)
                .WithTaggedFlag("CMX_PERFMON0", 18)
                .WithTaggedFlag("CMX_PERFMON1", 19)
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, val) => { control3.Value = val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl4Set.Define(collection)
                .WithTaggedFlag("HiFi4", 0)
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, val) => { control4.Value = val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl5Set.Define(collection)
                .WithTaggedFlag("NPU0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("COMP_ACCESS_RAM_ARBITER1", 2)
                .WithTaggedFlag("IOPCTL0", 3)
                .WithTaggedFlag("HiFi4_ACCESS_RAM_ARBITER1", 4)
                .WithTaggedFlag("MEDIA_ACCESS_RAM_ARBITER0", 5)
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, val) => { control5.Value |= val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl0Clear.Define(collection)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("CODE_CACHE", 1)
                .WithTaggedFlag("SYSTEM_CACHE", 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("VDD2_OTP0", 5)
                .WithReservedBits(6, 6)
                .WithTaggedFlag("SLEEPCON0", 12)
                .WithTaggedFlag("SYSCON0", 13)
                .WithTaggedFlag("GLIKEY0", 14)
                .WithTaggedFlag("GLIKEY3", 15)
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, val) => { control0.Value &= ~val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl1Clear.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("TPIU_TRACE_CLKIN", 2)
                .WithTaggedFlag("SWO_TRACECLKIN", 3)
                .WithTaggedFlag("TSCLK", 4)
                .WithTaggedFlag("eDMA0", 5)
                .WithTaggedFlag("eDMA1", 6)
                .WithTaggedFlag("PKC_RAM_CTRl", 7)
                .WithTaggedFlag("PKC", 8)
                .WithTaggedFlag("ROMCP", 9)
                .WithTaggedFlag("XSPI0", 10)
                .WithTaggedFlag("XSPI1", 11)
                .WithTaggedFlag("CACHE64_0", 12)
                .WithTaggedFlag("CACHE64_1", 13)
                .WithTaggedFlag("QK_SUBSYS", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("MMU0", 16)
                .WithTaggedFlag("MMU1", 17)
                .WithTaggedFlag("GPIO0", 18)
                .WithTaggedFlag("GPIO1", 19)
                .WithTaggedFlag("GPIO2", 20)
                .WithTaggedFlag("GPIO3", 21)
                .WithTaggedFlag("GPIO4", 22)
                .WithTaggedFlag("GPIO5", 23)
                .WithTaggedFlag("GPIO6", 24)
                .WithTaggedFlag("GPIO7", 25)
                .WithTaggedFlag("SCT0", 26)
                .WithTaggedFlag("CDOG0", 27)
                .WithTaggedFlag("CDOG1", 28)
                .WithTaggedFlag("CDOG2", 29)
                .WithTaggedFlag("LP_FLEXCOMM0", 30)
                .WithTaggedFlag("LP_FLEXCOMM1", 31)
                .WithWriteCallback((_, val) => { control1.Value &= ~val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl2Clear.Define(collection)
                .WithTaggedFlag("LP_FLEXCOMM2", 0)
                .WithTaggedFlag("LP_FLEXCOMM3", 1)
                .WithTaggedFlag("LP_FLEXCOMM4", 2)
                .WithTaggedFlag("LP_FLEXCOMM5", 3)
                .WithTaggedFlag("LP_FLEXCOMM6", 4)
                .WithTaggedFlag("LP_FLEXCOMM7", 5)
                .WithTaggedFlag("LP_FLEXCOMM8", 6)
                .WithTaggedFlag("LP_FLEXCOMM9", 7)
                .WithTaggedFlag("LP_FLEXCOMM10", 8)
                .WithTaggedFlag("LP_FLEXCOMM11", 9)
                .WithTaggedFlag("LP_FLEXCOMM12", 10)
                .WithTaggedFlag("LP_FLEXCOMM13", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("SAI0", 13)
                .WithTaggedFlag("SAI1", 14)
                .WithTaggedFlag("SAI2", 15)
                .WithTaggedFlag("I3C0", 16)
                .WithTaggedFlag("I3C1", 17)
                .WithTaggedFlag("CRC0", 18)
                .WithTaggedFlag("WWDT0", 19)
                .WithTaggedFlag("WWDT1", 20)
                .WithTaggedFlag("CTIMER0", 21)
                .WithTaggedFlag("CTIMER1", 22)
                .WithTaggedFlag("CTIMER2", 23)
                .WithTaggedFlag("CTIMER3", 24)
                .WithTaggedFlag("CTIMER4", 25)
                .WithTaggedFlag("MRT0", 26)
                .WithTaggedFlag("UTICK0", 27)
                .WithReservedBits(28, 2)
                .WithTaggedFlag("SEMA42_4", 30)
                .WithTaggedFlag("MU4", 31)
                .WithWriteCallback((_, val) => { control2.Value &= ~val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl3Clear.Define(collection)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("PINT0", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("FREQME0", 8)
                .WithReservedBits(9, 1)
                .WithTaggedFlag("INPUTMUX0", 10)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("SAFO_SGI", 12)
                .WithTaggedFlag("TRACE", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("PRINCE0", 15)
                .WithTaggedFlag("PRINCE1", 16)
                .WithTaggedFlag("XSPI1_PRINCE_XEX", 17)
                .WithTaggedFlag("CMX_PERFMON0", 18)
                .WithTaggedFlag("CMX_PERFMON1", 19)
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, val) => { control3.Value &= ~val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl4Clear.Define(collection)
                .WithTaggedFlag("HiFi4", 0)
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, val) => { control4.Value &= ~val; });

            Instance0Registers.VDD2_COMPPeripheralClockControl5Clear.Define(collection)
                .WithTaggedFlag("NPU0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("COMP_ACCESS_RAM_ARBITER1", 2)
                .WithTaggedFlag("IOPCTL0", 3)
                .WithTaggedFlag("HiFi4_ACCESS_RAM_ARBITER1", 4)
                .WithTaggedFlag("MEDIA_ACCESS_RAM_ARBITER0", 5)
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, val) => { control5.Value &= ~val; });

            Instance0Registers.OneSourceClockSliceEnable.Define(collection)
                .WithFlag(0, name: "dGDET0_FCLK_EN")
                .WithFlag(1, name: "dGDET1_FCLK_EN")
                .WithReservedBits(2, 30);

            Instance0Registers.FRO_TUNER0AndFRO_TUNER1ClockStatus.Define(collection)
                .WithFlag(0, valueProviderCallback: (_) => true, name: "FRO1_CLK_OK")
                .WithFlag(1, valueProviderCallback: (_) => true, name: "FRO0_CLK_OK")
                .WithReservedBits(2, 30);

            Instance0Registers.FRO0MAXClockDomainEnable.Define(collection, 0x7F)
                .WithTaggedFlag("FRO0MAX_OF_CMPT", 0)
                .WithTaggedFlag("FRO0MAX_OF_SENSE", 1)
                .WithTaggedFlag("FRO0MAX_OF_VDD2_DSP", 2)
                .WithTaggedFlag("FRO0MAX_OF_MD2", 3)
                .WithTaggedFlag("FRO0MAX_OF_MDN", 4)
                .WithTaggedFlag("FRO0MAX_OF_VDD2_COM", 5)
                .WithTaggedFlag("FRO0MAX_OF_COMN", 6)
                .WithReservedBits(7, 25);

            Instance0Registers.VDD2_COMPMainClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.VDD2_COMPBaseClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance0Registers.VDD2_DSPBaseClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance0Registers.VDD2_COMBaseClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance0Registers.VDD2_COMPMainClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance0Registers.VDD2_DSPClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.VDD2_DSPClockSelectSource.Define(collection, 0x4)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.RAMClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance0Registers.RAMClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.TPIUFunctionalClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.TPIUFunctionalClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.XSPI0FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.XSPI0FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.XSPI1FunctionalClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.XSPI1FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.SCTFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.SCTFunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.UTICK0FunctionalClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.UTICK0FunctionalClockDivider.Define(collection, 0x1)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.WWDT0FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.WWDT1FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.SYSTICKFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.SYSTICKFunctionalClockDivider.Define(collection, 0x00000007)
                .WithValueField(0, 8, name: "DIV")
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource0Select.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource0Divider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.LP_FLEXCOMM0ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource1Select.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource1Divider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.LP_FLEXCOMM1ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource2Select.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource2Divider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.LP_FLEXCOMM2ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource3Select.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM0to13ClockSource3Divider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.LP_FLEXCOMM3ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM4ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM5ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM6ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM7ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM8ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM9ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM10ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM11ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM12ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.LP_FLEXCOMM13ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.SAI0SAI1AndSAI2ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.SAI0SAI1AndSAI2FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMER0FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMER1FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMER2FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMER3FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMER4FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CTIMERindexFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.TRNGFunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.TRNGFCLKClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.I3C0AndI3C1FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.I3C0AndI3C1PCLKSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.I3C0AndI3C1PCLKDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.I3C0AndI3C1FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.CLKOUT_VDD2ClockSelectSource.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance0Registers.CLKOUT_VDD2ClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance0Registers.VDD2_COMPAudioClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            return collection;
        }

        private enum Instance0Registers
        {
            VDD2_COMPPeripheralClockControl0 = 0x10,
            VDD2_COMPPeripheralClockControl1 = 0x14,
            VDD2_COMPPeripheralClockControl2 = 0x18,
            VDD2_COMPPeripheralClockControl3 = 0x1C,
            VDD2_COMPPeripheralClockControl4 = 0x20,
            VDD2_COMPPeripheralClockControl5 = 0x24,
            VDD2_COMPPeripheralClockControl0Set = 0x40,
            VDD2_COMPPeripheralClockControl1Set = 0x44,
            VDD2_COMPPeripheralClockControl2Set = 0x48,
            VDD2_COMPPeripheralClockControl3Set = 0x4C,
            VDD2_COMPPeripheralClockControl4Set = 0x50,
            VDD2_COMPPeripheralClockControl5Set = 0x54,
            VDD2_COMPPeripheralClockControl0Clear = 0x70,
            VDD2_COMPPeripheralClockControl1Clear = 0x74,
            VDD2_COMPPeripheralClockControl2Clear = 0x78,
            VDD2_COMPPeripheralClockControl3Clear = 0x7C,
            VDD2_COMPPeripheralClockControl4Clear = 0x80,
            VDD2_COMPPeripheralClockControl5Clear = 0x84,
            OneSourceClockSliceEnable = 0x90,
            FRO_TUNER0AndFRO_TUNER1ClockStatus = 0x118,
            FRO0MAXClockDomainEnable = 0x128,
            VDD2_COMPMainClockDivider = 0x400,
            VDD2_COMPBaseClockSelectSource = 0x420,
            VDD2_DSPBaseClockSelectSource = 0x424,
            VDD2_COMBaseClockSelectSource = 0x428,
            VDD2_COMPMainClockSourceSelect = 0x434,
            VDD2_DSPClockDivider = 0x440,
            VDD2_DSPClockSelectSource = 0x444,
            RAMClockSelectSource = 0x450,
            RAMClockDivider = 0x45C,
            TPIUFunctionalClockSelectSource = 0x560,
            TPIUFunctionalClockDivider = 0x564,
            XSPI0FunctionalClockSourceSelect = 0x600,
            XSPI0FunctionalClockDivider = 0x604,
            XSPI1FunctionalClockSelectSource = 0x620,
            XSPI1FunctionalClockDivider = 0x624,
            SCTFunctionalClockSourceSelect = 0x640,
            SCTFunctionalClockDivider = 0x644,
            UTICK0FunctionalClockSelectSource = 0x700,
            UTICK0FunctionalClockDivider = 0x704,
            WWDT0FunctionalClockSourceSelect = 0x720,
            WWDT1FunctionalClockSourceSelect = 0x740,
            SYSTICKFunctionalClockSourceSelect = 0x760,
            SYSTICKFunctionalClockDivider = 0x764,
            LP_FLEXCOMM0to13ClockSource0Select = 0x800,
            LP_FLEXCOMM0to13ClockSource0Divider = 0x804,
            LP_FLEXCOMM0ClockSourceSelect = 0x808,
            LP_FLEXCOMM0to13ClockSource1Select = 0x820,
            LP_FLEXCOMM0to13ClockSource1Divider = 0x824,
            LP_FLEXCOMM1ClockSourceSelect = 0x828,
            LP_FLEXCOMM0to13ClockSource2Select = 0x840,
            LP_FLEXCOMM0to13ClockSource2Divider = 0x844,
            LP_FLEXCOMM2ClockSourceSelect = 0x848,
            LP_FLEXCOMM0to13ClockSource3Select = 0x860,
            LP_FLEXCOMM0to13ClockSource3Divider = 0x864,
            LP_FLEXCOMM3ClockSourceSelect = 0x868,
            LP_FLEXCOMM4ClockSourceSelect = 0x888,
            LP_FLEXCOMM5ClockSourceSelect = 0x8A8,
            LP_FLEXCOMM6ClockSourceSelect = 0x8C8,
            LP_FLEXCOMM7ClockSourceSelect = 0x8E8,
            LP_FLEXCOMM8ClockSourceSelect = 0x908,
            LP_FLEXCOMM9ClockSourceSelect = 0x928,
            LP_FLEXCOMM10ClockSourceSelect = 0x948,
            LP_FLEXCOMM11ClockSourceSelect = 0x968,
            LP_FLEXCOMM12ClockSourceSelect = 0x988,
            LP_FLEXCOMM13ClockSourceSelect = 0x9A8,
            SAI0SAI1AndSAI2ClockSourceSelect = 0x9C8,
            SAI0SAI1AndSAI2FunctionalClockDivider = 0x9CC,
            CTIMER0FunctionalClockDivider = 0xA00,
            CTIMER1FunctionalClockDivider = 0xA04,
            CTIMER2FunctionalClockDivider = 0xA08,
            CTIMER3FunctionalClockDivider = 0xA0C,
            CTIMER4FunctionalClockDivider = 0xA10,
            CTIMERindexFunctionalClockSourceSelect = 0xAA0,
            TRNGFunctionalClockSourceSelect = 0xAC0,
            TRNGFCLKClockDivider = 0xAC4,
            I3C0AndI3C1FunctionalClockSourceSelect = 0xB00,
            I3C0AndI3C1PCLKSourceSelect = 0xB04,
            I3C0AndI3C1PCLKDivider = 0xB08,
            I3C0AndI3C1FunctionalClockDivider = 0xB10,
            CLKOUT_VDD2ClockSelectSource = 0xB20,
            CLKOUT_VDD2ClockDivider = 0xB24,
            VDD2_COMPAudioClockSourceSelect = 0xB30,
        }

    }
}
