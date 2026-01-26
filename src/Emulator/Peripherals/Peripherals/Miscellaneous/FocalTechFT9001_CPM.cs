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
    public class FocalTechFT9001_CPM : BasicDoubleWordPeripheral, IKnownSize
    {
        public FocalTechFT9001_CPM(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.ClockSwitchConfig.Define(this)
                .WithTag("SOC_CLK_SOURCE", 0, 2)
                .WithFlag(9, name: "CLKSEL_ST_OSC160M", valueProviderCallback: (_) => true); // Flag is also called `CPM_CSWCFGR_OSC320M_SELECT` in a different driver

            Registers.OscillatorControlAndStatus.Define(this)
                .WithTaggedFlag("OSC8M_CLK_EN", 0)
                .WithTaggedFlag("PMU128K_CLK_EN", 1)
                .WithTaggedFlag("USBPHY240M_CLK_EN", 2)
                .WithTaggedFlag("OSC320M_CLK_EN", 3)
                .WithTaggedFlag("OSCEXT_CLK_EN", 4)
                .WithTaggedFlag("RTC32K_CLK_EN", 5)
                .WithTaggedFlag("PMU2K_CLK_EN", 6)
                .WithTaggedFlag("PLLNFC_EN", 7)
                .WithFlag(8, name: "OSC8M_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(9, name: "PMU128K_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(10, name: "USBPHY240M_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(11, name: "OSC320M_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(12, name: "OSCEXT_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(13, name: "RTC32K_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(14, name: "PMU2K_STABLE", valueProviderCallback: (_) => true)
                .WithFlag(15, name: "PLLNFC_STABLE", valueProviderCallback: (_) => true);

            Registers.ClockDividerEnable.Define(this)
                .WithFlag(0, name: "IPS_CLK_DIV_EN") // Not used in model, but driver expects it to keep its value
                .WithTaggedFlag("AHB3_CLK_DIV_EN", 2)
                .WithTaggedFlag("ARITH_CLK_DIV_EN", 3)
                .WithTaggedFlag("MCC_CLK_DIV_EN", 8)
                .WithTaggedFlag("ADC_CLK_DIV_EN", 10)
                .WithTaggedFlag("MESH_CLK_DIV_EN", 12)
                .WithTaggedFlag("TC_CLK_DIV_EN", 13)
                .WithTaggedFlag("TRACE_CLK_DIV_EN", 14)
                .WithTaggedFlag("CLKOUT_CLK_DIV_EN", 15)
                .WithTaggedFlag("I2S_M_CLK_DIV_EN", 22)
                .WithTaggedFlag("I2S_S_CLK_DIV_EN", 23);

            Registers.ChipConfig.Define(this).Tag("CHIPCFGR", 0, 32);
        }

        // Based on https://chromium.googlesource.com/chromiumos/third_party/zephyrproject/+/5874e86ccdd5abb2ecbc390f3a80b23a8c56149a/modules/hal/focaltech_module/focaltech/hal/ft/ft90/ft9001/standard_peripheral/source/drv/inc/cpm_reg.h
        private enum Registers
        {
            SleepConfiguration = 0x00,
            SleepControl = 0x04,

            SystemClockDivider = 0x08,
            SPeripheralClockDivider1 = 0x0C,
            SPeripheralClockDivider2 = 0x10,
            SPeripheralClockDivider3 = 0x14,

            ClockDividerUpdate = 0x18,
            ClockDividerEnable = 0x1C,

            OscillatorControlAndStatus = 0x20,
            ClockSwitchConfig = 0x24,
            CoreTickTimer = 0x28,
            ChipConfig = 0x2C,

            PowerControl = 0x30,
            SleepCounter = 0x34,
            WakeUpCounter = 0x38,
            MultipleClockGateControl = 0x3C,

            SystemClockGateControl = 0x40,
            AHB3ClockGateControl = 0x44,
            AlgorithmClockGateControl = 0x48,
            IPSClockGateControl = 0x4C,

            VCCGeneralTrim = 0x50,
            VCCLowVoltageDetectTrim = 0x54,
            VCCVRefTrim = 0x58,
            VCCCoreTestMode = 0x5C,

            OSC8MHzTrim = 0x60,
            RESERVED0 = 0x64,
            OSC320MHzTrim = 0x68,
            CardLDOTrim = 0x6C,

            OSCLStableTime = 0x70,
            OSCHStableTime = 0x74,
            OSCEStableTime = 0x78,
            PowerStatus = 0x7C,

            EPortSLPConfig = 0x80,
            EPortClockGateControl = 0x84,
            EPortRSTCR = 0x88,
            RTCTrim = 0x8C,

            PadWakeupInterruptControl = 0x90,
            WakeupFilterCounter = 0x94,
            CardPowerOnCounter = 0x98,
            RTC32kStableTime = 0x9C,

            MemoryPowerDownSleepControl = 0xA0,
            RESERVED1 = 0xA4,
            RESERVED2 = 0xA8,
            MultipleResetControl = 0xAC,

            SystemResetControl = 0xB0,
            AHB3ResetControl = 0xB4,
            AlgorithmResetControl = 0xB8,
            IPSResetControl = 0xBC,

            SleepConfiguration2 = 0xC0,
            RESERVED3 = 0xC4,
            RESERVED4 = 0xC8,
            RESERVED5 = 0xCC,

            PowerDownCounter = 0xD0,
            PowerOnCounter = 0xD4,
            PCDivR4 = 0xD8,
            RESERVED6 = 0xDC,
            NFCPLLConfig = 0xE0,
            NFCPLLSTimer = 0xE4,
        }
    }
}
