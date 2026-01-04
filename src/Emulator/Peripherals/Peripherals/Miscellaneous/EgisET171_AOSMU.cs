//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class EgisET171_AOSMU : BasicDoubleWordPeripheral, IKnownSize
    {
        public EgisET171_AOSMU(IMachine machine, IHasFrequency mtimer, IHasFrequency pit, IHasFrequency wdt) : base(machine)
        {
            this.mtimer = mtimer;
            this.pit = pit;
            this.wdt = wdt;
            DefineRegisters();
            InnerReset();
            // On first start we set the reset cause to Power On
            resetEventPor.Value = true;
        }

        public override void Reset()
        {
            if(isWarmReset)
            {
                // On a warm reset the values of `Secure_Con`, `Reset_Vector`, and `Dummy` are preserved
                var secureConValue = ReadDoubleWord((long)Registers.SecureControl);
                var resetVectorValue = ReadDoubleWord((long)Registers.ResetVector);
                var dummyRegisterValue = ReadDoubleWord((long)Registers.Dummy);

                InnerReset();

                WriteDoubleWord((long)Registers.SecureControl, secureConValue);
                WriteDoubleWord((long)Registers.ResetVector, resetVectorValue);
                WriteDoubleWord((long)Registers.Dummy, dummyRegisterValue);
            }
            else
            {
                InnerReset();
            }
        }

        public long Size => 0x1000;

        public ulong ResetVector => resetVector.Value;

        public long RootClockFrequency
        {
            get
            {
                long frequency;
                switch(rootClock.Value)
                {
                // Base values taken from egis hal, https://github.com/EgisMCU/hal_egis/blob/545f4b8044cb7df0952692a0db22069fef7a7a0c/et171/src/et171_hal_smu.c#L17
                case RootClockSelect.OSC_100K:
                    frequency = 102560 / 3;
                    break;
                case RootClockSelect.OSC_200M:
                    frequency = 205000000;
                    break;
                default:
                    throw new Exception("Unreachable");
                }

                return frequency / DecodeClockDivider(rootClockDivider.Value);
            }
        }

        public long APBClockFrequency
        {
            get
            {
                // APB base clock is defined as current root clock / 2
                var frequency = RootClockFrequency / 2;
                return frequency / DecodeClockDivider(apbClockDivider.Value);
            }
        }

        private static uint DecodeClockDivider(ulong regValue)
        {
            switch(regValue)
            {
            case 0x0:
                return 1;
            case 0x1:
                return 2;
            case 0x2:
                return 3;
            case 0x3:
                return 4;
            case 0x4:
                return 6;
            case 0x5:
                return 8;
            case 0x6:
                return 16;
            default:
                throw new Exception("Unreachable");
            }
        }

        private void InnerReset()
        {
            base.Reset();
            isWarmReset = false;
            UpdateFrequencies();
        }

        private void RequestWarmReset()
        {
            isWarmReset = true;
            machine.RequestReset();
        }

        private void UpdateFrequencies()
        {
            mtimer.Frequency = APBClockFrequency;
            pit.Frequency = APBClockFrequency;
            wdt.Frequency = APBClockFrequency;
        }

        private void DefineRegisters()
        {
            Registers.Version.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(4, 12, mode: FieldMode.Read, valueProviderCallback: _ => 0x0, name: "Project")
                .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Revision");

            Registers.ClockSource.Define(this, 0x41)
                .WithReservedBits(7, 25)
                .WithValueField(4, 3, out rootClockDivider, name: "Root_clk_div")
                .WithValueField(1, 3, out apbClockDivider, name: "APB_clk_div")
                .WithEnumField(0, 1, out rootClock, name: "Root_clk_sel")
                .WithChangeCallback((_, __) => UpdateFrequencies());

            Registers.BootStrapping.Define(this)
                .WithReservedBits(4, 28)
                .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ => 0x0, name: "Boot_strapping");

            Registers.SecureControl.Define(this, 0x40)
                .WithTaggedFlag("SPIS_No_Access", 31)
                .WithReservedBits(18, 12)
                .WithTaggedFlag("Bootloader_bypass_bootstrap", 17)
                .WithTaggedFlag("Bootloader_DFU_en", 16)
                .WithTaggedFlag("Sk_d_no_read", 15)
                .WithTaggedFlag("Sk_d2_no_read", 14)
                .WithTaggedFlag("Sk_d3_no_read", 13)
                .WithReservedBits(11, 2)
                .WithTaggedFlag("Sk_d_disable", 10)
                .WithTag("Sk_d_sel", 8, 2)
                .WithTag("Sk_d_key_size", 6, 2)
                .WithTaggedFlag("Fw_debug_en", 5)
                .WithTaggedFlag("Core_clk_en", 4)
                .WithTaggedFlag("Mcu_reset", 3)
                .WithFlag(2, mode: FieldMode.Write, writeCallback: (_, val) => { if(val) RequestWarmReset(); }, name: "Warm Reset")
                .WithFlag(1, mode: FieldMode.Write, writeCallback: (_, val) => { if(val) machine.RequestReset(); }, name: "Reset")
                .WithReservedBits(0, 1);

            Registers.ResetVector.Define(this, 0x70000000)
                .WithValueField(0, 32, out resetVector, name: "Reset_vector");

            // These flags, when set to false should turn off the respective IP core
            // Not implemented, but the software expects the flags to keep their value
            Registers.ClockEnable.Define(this, 0x3FFFFF)
                .WithReservedBits(22, 10)
                .WithFlag(21, name: "SySRAM1_clk_en")
                .WithFlag(20, name: "LIN_clk_en")
                .WithFlag(19, name: "CAN_clk_en")
                .WithFlag(18, name: "SPIM3_clk_en")
                .WithFlag(17, name: "SySRAM3_clk_en")
                .WithFlag(16, name: "SySRAM2_clk_en")
                .WithFlag(15, name: "SySRAM0_clk_en")
                .WithFlag(14, name: "SPIM2_clk_en")
                .WithFlag(13, name: "SPIM1_clk_en")
                .WithFlag(12, name: "SPIS_clk_en")
                .WithFlag(11, name: "Crypto_clk_en")
                .WithFlag(10, name: "DMAC_clk_en")
                .WithFlag(9, name: "USB2_clk_en")
                .WithFlag(8, name: "HWA_clk_en")
                .WithFlag(7, name: "HWAv2_clk_en")
                .WithFlag(6, name: "GPIO_clk_en")
                .WithFlag(5, name: "PITPWM_clk_en")
                .WithFlag(4, name: "I2C_clk_en")
                .WithFlag(3, name: "UART_clk_en")
                .WithFlag(2, name: "OTPC_clk_en")
                .WithFlag(1, name: "WDT_clk_en")
                .WithFlag(0, name: "RTC_clk_en")
                .WithChangeCallback((_, val) => this.WarningLog("Clock Enable not implemented, ignoring write (0x{0})", val));

            // These flags, when set false and then true should reset the respective IP core
            // Not implemented, but the software expects the flags to keep their value
            Registers.SoftwareReset.Define(this, 0x3FFFFF)
                .WithReservedBits(22, 10)
                .WithFlag(21, name: "SySRAM1_sw_rst")
                .WithFlag(20, name: "LIN_sw_rst")
                .WithFlag(19, name: "CAN_sw_rst")
                .WithFlag(18, name: "SPIM3_sw_rst")
                .WithFlag(17, name: "SySRAM3_sw_rst")
                .WithFlag(16, name: "SySRAM2_sw_rst")
                .WithFlag(15, name: "SySRAM0_sw_rst")
                .WithFlag(14, name: "SPIM2_sw_rst")
                .WithFlag(13, name: "SPIM1_sw_rst")
                .WithFlag(12, name: "SPIS_sw_rst")
                .WithFlag(11, name: "Crypto_sw_rst")
                .WithFlag(10, name: "DMAC_sw_rst")
                .WithFlag(9, name: "USB2_sw_rst")
                .WithFlag(8, name: "HWA_sw_rst")
                .WithFlag(7, name: "HWAv2_sw_rst")
                .WithFlag(6, name: "GPIO_sw_rst")
                .WithFlag(5, name: "PITPWM_sw_rst")
                .WithFlag(4, name: "I2C_sw_rst")
                .WithFlag(3, name: "UART_sw_rst")
                .WithFlag(2, name: "OTPC_sw_rst")
                .WithFlag(1, name: "WDT_sw_rst")
                .WithFlag(0, name: "RTC_sw_rst")
                .WithChangeCallback((_, val) => this.WarningLog("Software reset not implemented, ignoring write (0x{0})", val));

            Registers.Dummy.Define(this, 0xFFFF0000)
                .WithFlag(31, FieldMode.Read, name: "USB2_pwr_rdy", valueProviderCallback: _ => true)
                .WithTaggedFlag("SPIS2_CSN_no_wake", 30)
                .WithValueField(18, 12, name: "Dummy1")
                .WithTag("AO_WDT_ON", 16, 2)
                .WithTaggedFlag("SPIS_CSN_no_wake", 15)
                .WithFlag(14, out resetEventRstnPin, name: "RESET_EVENT_RSTN_PIN")
                .WithFlag(13, out resetEventPor, name: "RESET_EVENT_POR")
                .WithFlag(12, out resetEventWdt, name: "RESET_EVENT_WDT")
                .WithValueField(2, 9, name: "Dummy0")
                .WithTaggedFlag("Bypass_BROM_update", 1)
                .WithFlag(0, name: "Sleep_wakeup_flag");

            Registers.PowerMode.Define(this, 0xD0030008)
                .WithTaggedFlag("PHY_p33ready_09v", 31)
                .WithTaggedFlag("PHY_p09ready_33v_09v", 30)
                .WithTaggedFlag("USB2_iso_en", 29)
                .WithTaggedFlag("USB2_pwr_en", 28)
                .WithTag("SySRAM_ret_ctrl", 16, 12)
                .WithTag("Sel_AO0_wake", 14, 2)
                .WithTag("Sel_AO1_wake", 12, 2)
                .WithTag("Sel_AO2_wake", 10, 2)
                .WithTaggedFlag("Fod_int_polarity", 9)
                .WithReservedBits(4, 4)
                .WithTaggedFlag("USB2_pwr_rdy", 3)
                .WithTaggedFlag("Force_pd", 2)
                .WithTaggedFlag("Force_sleep", 1)
                .WithTaggedFlag("WFI_sleep", 0);

            Registers.PowerMode2.Define(this, 0x200000)
                .WithTag("Pwr2rstn_cycles", 24, 8)
                .WithTag("Sleep_DLDO_delta", 20, 4)
                .WithTag("Cnt_initial_value", 0, 20);

            Registers.PowerWake.Define(this, 0x40)
                .WithReservedBits(7, 25)
                .WithTaggedFlag("Sleep_fod_int_en", 6)
                .WithTaggedFlag("Sleep_usb2_int_en", 5)
                .WithTaggedFlag("Sleep_AO0_int_en", 4)
                .WithTaggedFlag("Sleep_AO1_int_en", 3)
                .WithTaggedFlag("Sleep_AO2_int_en", 2)
                .WithTaggedFlag("Sleep_spis_int_en", 1)
                .WithTaggedFlag("Sleep_cnt_int_en", 0);

            Registers.PowerWake2.Define(this)
                .WithReservedBits(7, 25)
                .WithFlag(6, FieldMode.WriteOneToClear, name: "Sleep_fod_int_pending")
                .WithFlag(5, FieldMode.WriteOneToClear, name: "Sleep_usb2_int_pending")
                .WithFlag(4, FieldMode.WriteOneToClear, name: "Sleep_AO0_int_pending")
                .WithFlag(3, FieldMode.WriteOneToClear, name: "Sleep_AO1_int_pending")
                .WithFlag(2, FieldMode.WriteOneToClear, name: "Sleep_AO2_int_pending")
                .WithFlag(1, FieldMode.WriteOneToClear, name: "Sleep_spis_int_pending")
                .WithFlag(0, FieldMode.WriteOneToClear, name: "Sleep_cnt_int_pending");

            Registers.PadOutputEnableStable.Define(this)
                .Tag("Pad_OE_Stable", 0, 32);

            Registers.PadDigitalOutputStable.Define(this)
                .Tag("Pad_DO_Stable", 0, 32);

            Registers.PadPullDown.Define(this)
                .Tag("Pad_PD", 0, 32);

            Registers.PadDrivingStrength.Define(this)
                .Tag("Pad_DS", 0, 32);

            Registers.PadInputEnable.Define(this, 0xFFFFFFFF)
                .Tag("Pad_IE", 0, 32);

            Registers.AnalogLDO30.Define(this)
                .Tag("AnalogLDO30", 0, 32);

            Registers.AnalogLDO18.Define(this)
                .Tag("AnalogLDO18", 0, 32);

            Registers.AnalogLDO11.Define(this)
                .Tag("AnalogLDO11", 0, 32);

            Registers.AnalogOscillator100K.Define(this, 0x17000000)
                .Tag("Analog_OSC100k", 0, 32);

            Registers.AnalogOscillator360M.Define(this, 0x40700000)
                .Tag("Analog_OSC360M", 0, 32);

            Registers.USB2PHY.Define(this)
                .Tag("USB2PHY", 0, 32);
        }

        private IValueRegisterField resetVector;
        private IValueRegisterField rootClockDivider;
        private IValueRegisterField apbClockDivider;
        private IFlagRegisterField resetEventRstnPin;
        private IFlagRegisterField resetEventPor;
        private IFlagRegisterField resetEventWdt;
        private IEnumRegisterField<RootClockSelect> rootClock;
        private bool isWarmReset = false;
        private readonly IHasFrequency pit;
        private readonly IHasFrequency mtimer;
        private readonly IHasFrequency wdt;

        private enum RootClockSelect
        {
            OSC_100K = 0x0,
            OSC_200M = 0x1,
        }

        private enum Registers
        {
            Version = 0x000,
            ClockSource = 0x004,
            BootStrapping = 0x008,
            SecureControl = 0x00C,
            ResetVector = 0x010,
            ClockEnable = 0x014,
            SoftwareReset = 0x018,
            Dummy = 0x01C,
            PowerMode = 0x020,
            PowerMode2 = 0x024,
            PowerWake = 0x028,
            PowerWake2 = 0x02C,
            PadOutputEnableStable = 0x030,
            PadDigitalOutputStable = 0x040,
            PadPullDown = 0x050,
            PadDrivingStrength = 0x060,
            PadInputEnable = 0x070,
            // Gap intentional
            AnalogLDO30 = 0x200,
            AnalogLDO18 = 0x204,
            AnalogLDO11 = 0x208,
            AnalogOscillator100K = 0x20C,
            AnalogOscillator360M = 0x210,
            // Gap intentional
            USB2PHY = 0x600,
        }
    }
}