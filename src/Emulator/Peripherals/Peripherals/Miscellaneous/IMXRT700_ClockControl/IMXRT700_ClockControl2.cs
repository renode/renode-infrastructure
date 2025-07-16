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
        private DoubleWordRegisterCollection DefineInstance2Registers()
        {
            var collection = new DoubleWordRegisterCollection(this);
            Instance2Registers.VDDN_COMPeripheralclock0.Define(collection, 0x18)
                .WithValueField(0, 32, out control0);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance2Registers.VDDN_COMPeripheralClock0Set.Define(collection)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("SYSCON2", 3)
                .WithTaggedFlag("IOPCTL2", 4)
                .WithReservedBits(5, 27)
                .WithWriteCallback((_, val) => { control0.Value |= val; });

            Instance2Registers.VDDN_COMPeripheralClock0Clear.Define(collection)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("SYSCON2", 3)
                .WithTaggedFlag("IOPCTL2", 4)
                .WithReservedBits(5, 27)
                .WithWriteCallback((_, val) => { control0.Value &= ~val; });

            Instance2Registers.VDDN_COMClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.VDDN_COMClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance2Registers.XTALOscillatorControl0.Define(collection)
                .WithTaggedFlag("LP_ENABLE", 0)
                .WithTaggedFlag("BYPASS_ENABLE", 1)
                .WithReservedBits(2, 30);

            Instance2Registers.OSCBypassClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.USB0ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.VDDN_COMBaseClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.USB1ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.MainPLL0ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.MainPLL0Control0.Define(collection, 0x160002)
                .WithTaggedFlag("BYPASS", 0)
                .WithTaggedFlag("RESET", 1)
                .WithReservedBits(2, 11)
                .WithTaggedFlag("HOLD_RING_OFF_ENA", 13)
                .WithReservedBits(14, 2)
                .WithTag("MULT", 16, 8)
                .WithReservedBits(24, 8);

            Instance2Registers.MainPLL0LockTimeDiv2.Define(collection, 0xCAFE)
                .WithTag("LOCKTIMEDIV2", 0, 16)
                .WithReservedBits(16, 16);

            Instance2Registers.MainPLL0Numerator.Define(collection, 0x4DD2F15)
                .WithTag("NUM", 0, 30)
                .WithReservedBits(30, 2);

            Instance2Registers.MainPLL0Denominator.Define(collection, 0x1FFFFFDB)
                .WithTag("DENOM", 0, 30)
                .WithReservedBits(30, 2);

            Instance2Registers.MainPLL0PFD.Define(collection) // Manual says reset value is 0x80808080, but it should be 0x0 when ready
                .WithValueField(0, 6, name: "PFD0")
                .WithFlag(6, out fractionalDivider0Ready, name: "PFD0_CLKRDY")
                .WithFlag(7, out fractionalDivider0Enable, writeCallback: (_, val) => { fractionalDivider0Ready.Value = true; }, name: "PFD0_CLKGATE")
                .WithValueField(8, 6, name: "PFD1")
                .WithFlag(14, out fractionalDivider1Ready, name: "PFD1_CLKRDY")
                .WithFlag(15, out fractionalDivider1Enable, writeCallback: (_, val) => { fractionalDivider1Ready.Value = true; }, name: "PFD1_CLKGATE")
                .WithValueField(16, 6, name: "PFD2")
                .WithFlag(22, out fractionalDivider2Ready, name: "PFD2_CLKRDY")
                .WithFlag(23, out fractionalDivider2Enable, writeCallback: (_, val) => { fractionalDivider2Ready.Value = true; }, name: "PFD2_CLKGATE")
                .WithValueField(24, 6, name: "PFD3")
                .WithFlag(30, out fractionalDivider3Ready, name: "PFD3_CLKRDY")
                .WithFlag(31, out fractionalDivider3Enable, writeCallback: (_, val) => { fractionalDivider3Ready.Value = true; }, name: "PFD3_CLKGATE");

            Instance2Registers.MainPLL0PFDClockDomainEnable.Define(collection, 0x7F7F7F7F)
                .WithTaggedFlag("PFD0_OF_CMPT", 0)
                .WithTaggedFlag("PFD0_OF_VDD1_SENSE", 1)
                .WithTaggedFlag("PFD0_OF_VDD2_DSP", 2)
                .WithTaggedFlag("PFD0_OF_MD2", 3)
                .WithTaggedFlag("PFD0_OF_MDN", 4)
                .WithTaggedFlag("PFD0_OF_VDD2_COM", 5)
                .WithTaggedFlag("PFD0_OF_COMN", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("PFD1_OF_CMPT", 8)
                .WithTaggedFlag("PFD1_OF_VDD1_SENSE", 9)
                .WithTaggedFlag("PFD1_OF_VDD2_DSP", 10)
                .WithTaggedFlag("PFD1_OF_MD2", 11)
                .WithTaggedFlag("PFD1_OF_MDN", 12)
                .WithTaggedFlag("PFD1_OF_VDD2_COM", 13)
                .WithTaggedFlag("PFD1_OF_COMN", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("PFD2_OF_CMPT", 16)
                .WithTaggedFlag("PFD2_OF_VDD1_SENSE", 17)
                .WithTaggedFlag("PFD2_OF_VDD2_DSP", 18)
                .WithTaggedFlag("PFD2_OF_MD2", 19)
                .WithTaggedFlag("PFD2_OF_MDN", 20)
                .WithTaggedFlag("PFD2_OF_VDD2_COM", 21)
                .WithTaggedFlag("PFD2_OF_COMN", 22)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("PFD3_OF_CMPT", 24)
                .WithTaggedFlag("PFD3_OF_VDD1_SENSE", 25)
                .WithTaggedFlag("PFD3_OF_VDD2_DSP", 26)
                .WithTaggedFlag("PFD3_OF_MD2", 27)
                .WithTaggedFlag("PFD3_OF_MDN", 28)
                .WithTaggedFlag("PFD3_OF_VDD2_COM", 29)
                .WithTaggedFlag("PFD3_OF_COMN", 30)
                .WithReservedBits(31, 1);

            Instance2Registers.AudioPLL0ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance2Registers.AudioPLL0Control0.Define(collection, 0x160002)
                .WithTaggedFlag("BYPASS", 0)
                .WithTaggedFlag("RESET", 1)
                .WithReservedBits(2, 11)
                .WithTaggedFlag("HOLD_RING_OFF_ENA", 13)
                .WithReservedBits(14, 2)
                .WithTag("MULT", 16, 8)
                .WithTaggedFlag("VCO_OUT_ENABLE", 24)
                .WithReservedBits(25, 7);

            Instance2Registers.AudioPLL0LockTimeDivideby2.Define(collection, 0xCAFE)
                .WithTag("LOCKTIMEDIV2", 0, 16)
                .WithReservedBits(16, 16);

            Instance2Registers.AudioPLL0Numerator.Define(collection, 0x4DD2F15)
                .WithTag("NUM", 0, 30)
                .WithReservedBits(30, 2);

            Instance2Registers.AudioPLL0Denominator.Define(collection, 0x1FFFFFDB)
                .WithTag("DENOM", 0, 30)
                .WithReservedBits(30, 2);

            Instance2Registers.AudioPLL0PFD.Define(collection, 0x0)
                .WithValueField(0, 6, name: "PFD0")
                .WithFlag(6, out audioFractionalDivider0Ready, FieldMode.Read, name: "PFD0_CLKRDY")
                .WithFlag(7, out audioFractionalDivider0Enable, writeCallback: (_, val) => { if(val) audioFractionalDivider0Ready.Value = true; }, name: "PFD0_CLKGATE")
                .WithValueField(8, 6, name: "PFD1")
                .WithFlag(14, out audioFractionalDivider1Ready, FieldMode.Read, name: "PFD1_CLKRDY")
                .WithFlag(15, out audioFractionalDivider1Enable, writeCallback: (_, val) => { if(val) audioFractionalDivider1Ready.Value = true; }, name: "PFD1_CLKGATE")
                .WithValueField(16, 6, name: "PFD2")
                .WithFlag(22, out audioFractionalDivider2Ready, FieldMode.Read, name: "PFD2_CLKRDY")
                .WithFlag(23, out audioFractionalDivider2Enable, writeCallback: (_, val) => { if(val) audioFractionalDivider2Ready.Value = true; }, name: "PFD2_CLKGATE")
                .WithValueField(24, 6, name: "PFD3")
                .WithFlag(30, out audioFractionalDivider3Ready, FieldMode.Read, name: "PFD3_CLKRDY")
                .WithFlag(31, out audioFractionalDivider3Enable, writeCallback: (_, val) => { if(val) audioFractionalDivider3Ready.Value = true; }, name: "PFD3_CLKGATE");

            Instance2Registers.AudioPLL0PFDClockEnable.Define(collection, 0x7F7F7F7F)
                .WithTaggedFlag("PFD0_OF_CMPT", 0)
                .WithTaggedFlag("PFD0_OF_VDD1_SENSE", 1)
                .WithTaggedFlag("PFD0_OF_VDD2_DSP", 2)
                .WithTaggedFlag("PFD0_OF_MD2", 3)
                .WithTaggedFlag("PFD0_OF_MDN", 4)
                .WithTaggedFlag("PFD0_OF_VDD2_COM", 5)
                .WithTaggedFlag("PFD0_OF_COMN", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("PFD1_OF_CMPT", 8)
                .WithTaggedFlag("PFD1_OF_VDD1_SENSE", 9)
                .WithTaggedFlag("PFD1_OF_VDD2_DSP", 10)
                .WithTaggedFlag("PFD1_OF_MD2", 11)
                .WithTaggedFlag("PFD1_OF_MDN", 12)
                .WithTaggedFlag("PFD1_OF_VDD2_COM", 13)
                .WithTaggedFlag("PFD1_OF_COMN", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("PFD2_OF_CMPT", 16)
                .WithTaggedFlag("PFD2_OF_VDD1_SENSE", 17)
                .WithTaggedFlag("PFD2_OF_VDD2_DSP", 18)
                .WithTaggedFlag("PFD2_OF_MD2", 19)
                .WithTaggedFlag("PFD2_OF_MDN", 20)
                .WithTaggedFlag("PFD2_OF_VDD2_COM", 21)
                .WithTaggedFlag("PFD2_OF_COMN", 22)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("PFD3_OF_CMPT", 24)
                .WithTaggedFlag("PFD3_OF_VDD1_SENSE", 25)
                .WithTaggedFlag("PFD3_OF_VDD2_DSP", 26)
                .WithTaggedFlag("PFD3_OF_MD2", 27)
                .WithTaggedFlag("PFD3_OF_MDN", 28)
                .WithTaggedFlag("PFD3_OF_VDD2_COM", 29)
                .WithTaggedFlag("PFD3_OF_COMN", 30)
                .WithReservedBits(31, 1);

            Instance2Registers.AudioPLL0VCOClockEnable.Define(collection, 0x7F)
                .WithTaggedFlag("VCO_OF_CMPT", 0)
                .WithTaggedFlag("VCO_OF_VDD1_SENSE", 1)
                .WithTaggedFlag("VCO_OF_VDD2_DSP", 2)
                .WithTaggedFlag("VCO_OF_MD2", 3)
                .WithTaggedFlag("VCO_OF_MDN", 4)
                .WithTaggedFlag("VCO_OF_VDD2_COM", 5)
                .WithTaggedFlag("VCO_OF_COMN", 6)
                .WithReservedBits(7, 25);

            Instance2Registers.CKIL32kHzClockGate.Define(collection, 0x1)
                .WithTaggedFlag("CKIL_32K_EN", 0)
                .WithTaggedFlag("GATED_FLAG", 1)
                .WithReservedBits(2, 30);
            return collection;
        }

        private IFlagRegisterField fractionalDivider0Enable;
        private IFlagRegisterField fractionalDivider0Ready;
        private IFlagRegisterField fractionalDivider1Enable;
        private IFlagRegisterField fractionalDivider1Ready;
        private IFlagRegisterField fractionalDivider2Enable;
        private IFlagRegisterField fractionalDivider2Ready;
        private IFlagRegisterField fractionalDivider3Enable;
        private IFlagRegisterField fractionalDivider3Ready;
        private IFlagRegisterField audioFractionalDivider0Enable;
        private IFlagRegisterField audioFractionalDivider0Ready;
        private IFlagRegisterField audioFractionalDivider1Enable;
        private IFlagRegisterField audioFractionalDivider1Ready;
        private IFlagRegisterField audioFractionalDivider2Enable;
        private IFlagRegisterField audioFractionalDivider2Ready;
        private IFlagRegisterField audioFractionalDivider3Enable;
        private IFlagRegisterField audioFractionalDivider3Ready;

        private enum Instance2Registers
        {
            VDDN_COMPeripheralclock0 = 0x10,
            VDDN_COMPeripheralClock0Set = 0x40,
            VDDN_COMPeripheralClock0Clear = 0x70,
            VDDN_COMClockSourceSelect = 0xA4,
            VDDN_COMClockDivider = 0xAC,
            XTALOscillatorControl0 = 0x100,
            OSCBypassClockSourceSelect = 0x108,
            USB0ClockSourceSelect = 0x10C,
            VDDN_COMBaseClockSourceSelect = 0x110,
            USB1ClockSourceSelect = 0x11C,
            MainPLL0ClockSourceSelect = 0x200,
            MainPLL0Control0 = 0x204,
            MainPLL0LockTimeDiv2 = 0x20C,
            MainPLL0Numerator = 0x210,
            MainPLL0Denominator = 0x214,
            MainPLL0PFD = 0x218,
            MainPLL0PFDClockDomainEnable = 0x220,
            AudioPLL0ClockSourceSelect = 0x400,
            AudioPLL0Control0 = 0x404,
            AudioPLL0LockTimeDivideby2 = 0x40C,
            AudioPLL0Numerator = 0x410,
            AudioPLL0Denominator = 0x414,
            AudioPLL0PFD = 0x418,
            AudioPLL0PFDClockEnable = 0x420,
            AudioPLL0VCOClockEnable = 0x424,
            CKIL32kHzClockGate = 0x600,
        }
    }
}
