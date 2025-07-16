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
        private DoubleWordRegisterCollection DefineInstance4Registers()
        {
            var collection = new DoubleWordRegisterCollection(this);
            Instance4Registers.VDD2_COMPMediaPeripheralClockControl0.Define(collection, 0x20802000)
                .WithValueField(0, 32, out control0);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance4Registers.VDD2_COMPMediaPeripheralClockControl1.Define(collection)
                .WithValueField(0, 32, out control1);  // Same fields as the Set/Clear equivalent, single field for the same of simplicity of code

            Instance4Registers.VDD2_COMPMediaPeripheralClockControl0Set.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("VGPU", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MIPI_DSI_HOST", 4)
                .WithTaggedFlag("LPSPI16", 5)
                .WithTaggedFlag("LPSPI14", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("XSPI2", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("MMU2", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("GLIKEY5", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("FLEXIO0", 15)
                .WithReservedBits(16, 6)
                .WithTaggedFlag("LCDIF", 22)
                .WithTaggedFlag("SYSCON4", 23)
                .WithTaggedFlag("JPEGDEC", 24)
                .WithTaggedFlag("PNGDEC", 25)
                .WithTaggedFlag("EZHV", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("AXBS_EZH", 28)
                .WithTaggedFlag("GLIKEY2", 29)
                .WithReservedBits(30, 2)
                .WithWriteCallback((_, val) => control0.Value |= val);

            Instance4Registers.VDD2_COMPMediaPeripheralClockControl1Set.Define(collection)
                .WithTaggedFlag("USB0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("USB1", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("uSDHC0", 4)
                .WithTaggedFlag("uSDHC1", 5)
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, val) => control1.Value |= val);

            Instance4Registers.VDD2_COMPMediaPeripheralClockControl0Clear.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("VGPU", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MIPI_DSI_HOST", 4)
                .WithTaggedFlag("LPSPI16", 5)
                .WithTaggedFlag("LPSPI14", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("XSPI2", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("MMU2", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("GLIKEY5", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("FLEXIO0", 15)
                .WithReservedBits(16, 6)
                .WithTaggedFlag("LCDIF", 22)
                .WithTaggedFlag("SYSCON4", 23)
                .WithTaggedFlag("JPEGDEC", 24)
                .WithTaggedFlag("PNGDEC", 25)
                .WithTaggedFlag("EZHV", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("AXBS_EZH", 28)
                .WithTaggedFlag("GLIKEY2", 29)
                .WithReservedBits(30, 2)
                .WithWriteCallback((_, val) => control0.Value &= ~val);

            Instance4Registers.VDD2_COMPMediaPeripheralClockControl1Clear.Define(collection)
                .WithTaggedFlag("USB0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("USB1", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("uSDHC0", 4)
                .WithTaggedFlag("uSDHC1", 5)
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, val) => control1.Value &= ~val);

            Instance4Registers.OneSourceClockSliceEnable.Define(collection)

                .WithFlag(0, name: "USBPHY_REFCLK_EN")
                .WithReservedBits(1, 31);

            Instance4Registers.VDDN_MEDIAClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance4Registers.VDDN_MEDIAClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.MediaMainClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance4Registers.MediaMainClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.VDDN_MEDIABaseClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance4Registers.VDD2_MEDIABaseClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithReservedBits(2, 30);

            Instance4Registers.XSPI2FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.XSPI2FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.USB0FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithTaggedFlag("GATED_FLAG", 3)
                .WithReservedBits(4, 28);

            Instance4Registers.USB1FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithTaggedFlag("GATED_FLAG", 3)
                .WithReservedBits(4, 28);

            Instance4Registers.SDIO0FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.SDIO0FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.SDIO1FunctionalClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.SDIO1FunctionalClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.MIPI_DSI_HostPHYClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.MIPI_DSI_HostPHYClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.MIPI_DSI_HostDPHYEscapeModeClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.MIPI_DSI_HostDPHYEscapeModeReceiveClockDivider.Define(collection, 0x40000010)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.MIPI_DSI_HostDPHYEscapeModeTransmitClockDivider.Define(collection, 0x40000011)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.VGPUClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.VGPUClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.LPSPI14ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.LPSPI14ClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.LPSPI16ClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.LPSPI16ClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.FLEXIOClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.FLEXIOClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.LCDIFPixelClockSourceSelect.Define(collection)
                .WithValueField(0, 2, name: "SEL")
                .WithFlag(2, name: "SEL_EN")
                .WithReservedBits(3, 29);

            Instance4Registers.LCDIFPixelClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 20)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.LowfrequencyClockDivider.Define(collection, 0x40000001)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(16, 12)
                .WithTaggedFlag("BUSY", 28)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl0.Define(collection, 0x20802000)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("VGPU", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MIPI_DSI_HOST", 4)
                .WithTaggedFlag("LPSPI16", 5)
                .WithTaggedFlag("LPSPI14", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("XSPI2", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("MMU2", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("GLIKEY5", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("FLEXIO0", 15)
                .WithReservedBits(16, 6)
                .WithTaggedFlag("LCDIF", 22)
                .WithTaggedFlag("SYSCON4", 23)
                .WithTaggedFlag("JPEGDEC", 24)
                .WithTaggedFlag("PNGDEC", 25)
                .WithTaggedFlag("EZHV", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("AXBS_EZH", 28)
                .WithTaggedFlag("GLIKEY2", 29)
                .WithReservedBits(30, 2);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl1.Define(collection)
                .WithTaggedFlag("USB0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("USB1", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("uSDHC0", 4)
                .WithTaggedFlag("uSDHC1", 5)
                .WithReservedBits(6, 26);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl0Set.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("VGPU", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MIPI_DSI_HOST", 4)
                .WithTaggedFlag("LPSPI16", 5)
                .WithTaggedFlag("LPSPI14", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("XSPI2", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("MMU2", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("GLIKEY5", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("FLEXIO0", 15)
                .WithReservedBits(16, 6)
                .WithTaggedFlag("LCDIF", 22)
                .WithTaggedFlag("SYSCON4", 23)
                .WithTaggedFlag("JPEGDEC", 24)
                .WithTaggedFlag("PNGDEC", 25)
                .WithTaggedFlag("EZHV", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("AXBS_EZH", 28)
                .WithTaggedFlag("GLIKEY2", 29)
                .WithReservedBits(30, 2);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl1Set.Define(collection)
                .WithTaggedFlag("USB0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("USB1", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("uSDHC0", 4)
                .WithTaggedFlag("uSDHC1", 5)
                .WithReservedBits(6, 26);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl0Clear.Define(collection)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("VGPU", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MIPI_DSI_HOST", 4)
                .WithTaggedFlag("LPSPI16", 5)
                .WithTaggedFlag("LPSPI14", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("XSPI2", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("MMU2", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("GLIKEY5", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("FLEXIO0", 15)
                .WithReservedBits(16, 6)
                .WithTaggedFlag("LCDIF", 22)
                .WithTaggedFlag("SYSCON4", 23)
                .WithTaggedFlag("JPEGDEC", 24)
                .WithTaggedFlag("PNGDEC", 25)
                .WithTaggedFlag("EZHV", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("AXBS_EZH", 28)
                .WithTaggedFlag("GLIKEY2", 29)
                .WithReservedBits(30, 2);

            Instance4Registers.VDD1_SENSEMediaPeripheralClockControl1Clear.Define(collection)
                .WithTaggedFlag("USB0", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("USB1", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("uSDHC0", 4)
                .WithTaggedFlag("uSDHC1", 5)
                .WithReservedBits(6, 26);

            Instance4Registers.OneSourceClockSliceEnableForVVD1_SENSE.Define(collection)

                .WithFlag(0, name: "USBPHY_REFCLK_EN")
                .WithReservedBits(1, 31);

            return collection;
        }

        private enum Instance4Registers
        {
            VDD2_COMPMediaPeripheralClockControl0 = 0x10,
            VDD2_COMPMediaPeripheralClockControl1 = 0x14,
            VDD2_COMPMediaPeripheralClockControl0Set = 0x40,
            VDD2_COMPMediaPeripheralClockControl1Set = 0x44,
            VDD2_COMPMediaPeripheralClockControl0Clear = 0x70,
            VDD2_COMPMediaPeripheralClockControl1Clear = 0x74,
            OneSourceClockSliceEnable = 0x90,
            VDDN_MEDIAClockSourceSelect = 0xA4,
            VDDN_MEDIAClockDivider = 0xAC,
            MediaMainClockSourceSelect = 0x104,
            MediaMainClockDivider = 0x10C,
            VDDN_MEDIABaseClockSourceSelect = 0x110,
            VDD2_MEDIABaseClockSourceSelect = 0x114,
            XSPI2FunctionalClockSourceSelect = 0x200,
            XSPI2FunctionalClockDivider = 0x204,
            USB0FunctionalClockSourceSelect = 0x220,
            USB1FunctionalClockSourceSelect = 0x240,
            SDIO0FunctionalClockSourceSelect = 0x260,
            SDIO0FunctionalClockDivider = 0x264,
            SDIO1FunctionalClockSourceSelect = 0x280,
            SDIO1FunctionalClockDivider = 0x284,
            MIPI_DSI_HostPHYClockSourceSelect = 0x300,
            MIPI_DSI_HostPHYClockDivider = 0x304,
            MIPI_DSI_HostDPHYEscapeModeClockSourceSelect = 0x308,
            MIPI_DSI_HostDPHYEscapeModeReceiveClockDivider = 0x30C,
            MIPI_DSI_HostDPHYEscapeModeTransmitClockDivider = 0x310,
            VGPUClockSourceSelect = 0x320,
            VGPUClockDivider = 0x324,
            LPSPI14ClockSourceSelect = 0x328,
            LPSPI14ClockDivider = 0x32C,
            LPSPI16ClockSourceSelect = 0x330,
            LPSPI16ClockDivider = 0x334,
            FLEXIOClockSourceSelect = 0x338,
            FLEXIOClockDivider = 0x33C,
            LCDIFPixelClockSourceSelect = 0x340,
            LCDIFPixelClockDivider = 0x344,
            LowfrequencyClockDivider = 0x700,
            VDD1_SENSEMediaPeripheralClockControl0 = 0x810,
            VDD1_SENSEMediaPeripheralClockControl1 = 0x814,
            VDD1_SENSEMediaPeripheralClockControl0Set = 0x840,
            VDD1_SENSEMediaPeripheralClockControl1Set = 0x844,
            VDD1_SENSEMediaPeripheralClockControl0Clear = 0x870,
            VDD1_SENSEMediaPeripheralClockControl1Clear = 0x874,
            OneSourceClockSliceEnableForVVD1_SENSE = 0x890,
        }
    }
}
