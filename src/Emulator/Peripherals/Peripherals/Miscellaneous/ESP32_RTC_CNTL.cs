//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2024 Sean "xobs" Cross
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus.Wrappers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32_RTC_CNTL : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32_RTC_CNTL(IMachine machine, ESP32Generation generation = ESP32Generation.ESP32S3) : base(machine)
        {
            this.generation = generation;
            generationMapper = new RegisterMapper(typeof(Registers));
            generationMapper.RegisterEnumMapping(generation switch
            {
                ESP32Generation.ESP32S2 => typeof(RegistersESP32S2),
                ESP32Generation.ESP32S3 => typeof(RegistersESP32S3),
                _ => throw new ConstructionException($"{generation} is not supported by this model; the ESP32 (LX6) RTC controller has a different layout"),
            });
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            latchedTime = 0;
        }

        public override string OffsetToString(long offset) => generationMapper.ToString(offset);

        public long Size => 0x200;

        private void DefineRegisters()
        {
            Registers.Options0.Define(this, 0x1C00A000)
                // Writing 0x86 to a SW_STALL pair stalls that CPU; nothing here can stall, so
                // these read back zero and no software believes a core is halted.
                .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "SW_STALL_APPCPU_C0")
                .WithValueField(2, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "SW_STALL_PROCPU_C0")
                .WithFlag(4, FieldMode.Write, name: "SW_APPCPU_RST")
                .WithFlag(5, FieldMode.Write, name: "SW_PROCPU_RST")
                .WithFlag(6, name: "BB_I2C_FORCE_PD")
                .WithFlag(7, name: "BB_I2C_FORCE_PU")
                .WithFlag(8, name: "BBPLL_I2C_FORCE_PD")
                .WithFlag(9, name: "BBPLL_I2C_FORCE_PU")
                .WithFlag(10, name: "BBPLL_FORCE_PD")
                .WithFlag(11, name: "BBPLL_FORCE_PU")
                .WithFlag(12, name: "XTL_FORCE_PD")
                .WithFlag(13, name: "XTL_FORCE_PU")
                .WithValueField(14, 4, name: "XTL_EN_WAIT")
                .WithReservedBits(18, 5)
                .WithFlag(23, name: "XTL_FORCE_ISO")
                .WithFlag(24, name: "PLL_FORCE_ISO")
                .WithFlag(25, name: "ANALOG_FORCE_ISO")
                .WithFlag(26, name: "XTL_FORCE_NOISO")
                .WithFlag(27, name: "PLL_FORCE_NOISO")
                .WithFlag(28, name: "ANALOG_FORCE_NOISO")
                .WithFlag(29, name: "DG_WRAP_FORCE_RST")
                .WithFlag(30, name: "DG_WRAP_FORCE_NORST")
                .WithFlag(31, FieldMode.Write, name: "SW_SYS_RST");

            Registers.TimeUpdate.Define(this)
                .WithReservedBits(0, 27)
                .WithFlag(27, name: "TIMER_SYS_STALL")
                .WithFlag(28, name: "TIMER_XTL_OFF")
                .WithFlag(29, name: "TIMER_SYS_RST")
                // Undocumented on both generations, but the mask ROM polls it after asking for an update
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => true, name: "TIME_VALID")
                .WithFlag(31, FieldMode.Read | FieldMode.Write, valueProviderCallback: _ => false,
                    writeCallback: (_, value) => { if(value) { latchedTime = CurrentRtcTicks(); } }, name: "TIME_UPDATE");

            Registers.TimeLow0.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)latchedTime, name: "TIMER_VALUE0_LOW");

            Registers.TimeHigh0.Define(this)
                .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)(latchedTime >> 32), name: "TIMER_VALUE0_HIGH")
                .WithReservedBits(16, 16);

            Registers.ResetState.Define(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => PoweronResetCause, name: "RESET_CAUSE_PROCPU")
                .WithValueField(6, 6, FieldMode.Read, valueProviderCallback: _ => PoweronResetCause, name: "RESET_CAUSE_APPCPU")
                .WithFlag(12, name: "APPCPU_STAT_VECTOR_SEL")
                .WithFlag(13, name: "PROCPU_STAT_VECTOR_SEL")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => true, name: "RESET_FLAG_PROCPU")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => true, name: "RESET_FLAG_APPCPU")
                .WithFlag(16, FieldMode.Write, name: "RESET_FLAG_PROCPU_CLR")
                .WithFlag(17, FieldMode.Write, name: "RESET_FLAG_APPCPU_CLR")
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => true, name: "PROCPU_OCD_HALT_ON_RESET")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => true, name: "RESET_FLAG_APPCPU2")
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => true, name: "RESET_FLAG_JTAG_PROCPU")
                .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => true, name: "RESET_FLAG_JTAG_APPCPU")
                .WithFlag(22, FieldMode.Write, name: "RESET_FLAG_JTAG_PROCPU_CLR")
                .WithFlag(23, FieldMode.Write, name: "RESET_FLAG_JTAG_APPCPU_CLR")
                .WithFlag(24, name: "APP_DRESET_MASK")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "PRO_DRESET_MASK")
                .WithReservedBits(26, 6);

            switch(generation)
            {
            case ESP32Generation.ESP32S2:
                TagRemainingRegisters(typeof(RegistersESP32S2));
                break;
            case ESP32Generation.ESP32S3:
                TagRemainingRegisters(typeof(RegistersESP32S3));
                break;
            }
            TagRemainingRegisters(typeof(Registers));
        }

        private void TagRemainingRegisters(Type registers)
        {
            foreach(Enum register in Enum.GetValues(registers))
            {
                var offset = Convert.ToInt64(register);
                if(!RegistersCollection.HasRegisterAtOffset(offset))
                {
                    RegistersCollection.DefineRegister(offset).WithTag(register.ToString(), 0, 32);
                }
            }
        }

        // Sampled only via TIME_UPDATE, so virtual time is enough
        private ulong CurrentRtcTicks()
        {
            return (ulong)(machine.ElapsedVirtualTime.TimeElapsed.TotalMicroseconds * SlowClockFrequency / 1000000.0);
        }

        private ulong latchedTime;

        private readonly ESP32Generation generation;
        private readonly RegisterMapper generationMapper;

        // RC_SLOW is the default source of the RTC slow clock
        private const double SlowClockFrequency = 136000.0;
        // Renode always starts a machine from cold
        private const ulong PoweronResetCause = 0x01;

        // Shared
        private enum Registers : long
        {
            Options0        = 0x000, // RTC_CNTL_OPTIONS0
            SlpTimer0       = 0x004, // RTC_CNTL_SLP_TIMER0
            SlpTimer1       = 0x008, // RTC_CNTL_SLP_TIMER1
            TimeUpdate      = 0x00C, // RTC_CNTL_TIME_UPDATE
            TimeLow0        = 0x010, // RTC_CNTL_TIME_LOW0
            TimeHigh0       = 0x014, // RTC_CNTL_TIME_HIGH0
            State0          = 0x018, // RTC_CNTL_STATE0
            Timer1          = 0x01C, // RTC_CNTL_TIMER1
            Timer2          = 0x020, // RTC_CNTL_TIMER2
            Timer3          = 0x024, // RTC_CNTL_TIMER3
            Timer4          = 0x028, // RTC_CNTL_TIMER4
            Timer5          = 0x02C, // RTC_CNTL_TIMER5
            Timer6          = 0x030, // RTC_CNTL_TIMER6
            AnaConfig       = 0x034, // RTC_CNTL_ANA_CONF
            ResetState      = 0x038, // RTC_CNTL_RESET_STATE
            WakeupState     = 0x03C, // RTC_CNTL_WAKEUP_STATE
            IntEnableRtc    = 0x040, // RTC_CNTL_INT_ENA_RTC
            IntRawRtc       = 0x044, // RTC_CNTL_INT_RAW_RTC
            IntStatusRtc    = 0x048, // RTC_CNTL_INT_ST_RTC
            IntClearRtc     = 0x04C, // RTC_CNTL_INT_CLR_RTC
            Store0          = 0x050, // RTC_CNTL_STORE0
            Store1          = 0x054, // RTC_CNTL_STORE1
            Store2          = 0x058, // RTC_CNTL_STORE2
            Store3          = 0x05C, // RTC_CNTL_STORE3
            ExtXtlConfig    = 0x060, // RTC_CNTL_EXT_XTL_CONF
            ExtWakeupConfig = 0x064, // RTC_CNTL_EXT_WAKEUP_CONF
            SlpRejectConfig = 0x068, // RTC_CNTL_SLP_REJECT_CONF
            CpuPeriodConfig = 0x06C, // RTC_CNTL_CPU_PERIOD_CONF
            SdioActConfig   = 0x070, // RTC_CNTL_SDIO_ACT_CONF
            ClkConfig       = 0x074, // RTC_CNTL_CLK_CONF
            SlowClkConfig   = 0x078, // RTC_CNTL_SLOW_CLK_CONF
            SdioConfig      = 0x07C, // RTC_CNTL_SDIO_CONF
            BiasConfig      = 0x080, // RTC_CNTL_BIAS_CONF
            Pwc             = 0x088, // RTC_CNTL_PWC
        }

        private enum RegistersESP32S2 : long
        {
            Reg                 = 0x084, // RTC_CNTL_REG
            DigPwc              = 0x08C, // RTC_CNTL_DIG_PWC
            DigIso              = 0x090, // RTC_CNTL_DIG_ISO
            Wdtconfig0          = 0x094, // RTC_CNTL_WDTCONFIG0
            Wdtconfig1          = 0x098, // RTC_CNTL_WDTCONFIG1
            Wdtconfig2          = 0x09C, // RTC_CNTL_WDTCONFIG2
            Wdtconfig3          = 0x0A0, // RTC_CNTL_WDTCONFIG3
            Wdtconfig4          = 0x0A4, // RTC_CNTL_WDTCONFIG4
            Wdtfeed             = 0x0A8, // RTC_CNTL_WDTFEED
            Wdtwprotect         = 0x0AC, // RTC_CNTL_WDTWPROTECT
            SwdConfig           = 0x0B0, // RTC_CNTL_SWD_CONF
            SwdWprotect         = 0x0B4, // RTC_CNTL_SWD_WPROTECT
            SwCpuStall          = 0x0B8, // RTC_CNTL_SW_CPU_STALL
            Store4              = 0x0BC, // RTC_CNTL_STORE4
            Store5              = 0x0C0, // RTC_CNTL_STORE5
            Store6              = 0x0C4, // RTC_CNTL_STORE6
            Store7              = 0x0C8, // RTC_CNTL_STORE7
            LowPowerStatus      = 0x0CC, // RTC_CNTL_LOW_POWER_ST
            Diag0               = 0x0D0, // RTC_CNTL_DIAG0
            PadHold             = 0x0D4, // RTC_CNTL_PAD_HOLD
            DigPadHold          = 0x0D8, // RTC_CNTL_DIG_PAD_HOLD
            ExtWakeup1          = 0x0DC, // RTC_CNTL_EXT_WAKEUP1
            ExtWakeup1Status    = 0x0E0, // RTC_CNTL_EXT_WAKEUP1_STATUS
            BrownOut            = 0x0E4, // RTC_CNTL_BROWN_OUT
            TimeLow1            = 0x0E8, // RTC_CNTL_TIME_LOW1
            TimeHigh1           = 0x0EC, // RTC_CNTL_TIME_HIGH1
            Xtal32kClkFactor    = 0x0F0, // RTC_CNTL_XTAL32K_CLK_FACTOR
            Xtal32kConfig       = 0x0F4, // RTC_CNTL_XTAL32K_CONF
            UlpCpTimer          = 0x0F8, // RTC_CNTL_ULP_CP_TIMER
            UlpCpControl        = 0x0FC, // RTC_CNTL_ULP_CP_CTRL
            CocpuControl        = 0x100, // RTC_CNTL_COCPU_CTRL
            TouchCtrl1          = 0x104, // RTC_CNTL_TOUCH_CTRL1
            TouchCtrl2          = 0x108, // RTC_CNTL_TOUCH_CTRL2
            TouchScanControl    = 0x10C, // RTC_CNTL_TOUCH_SCAN_CTRL
            TouchSlpThres       = 0x110, // RTC_CNTL_TOUCH_SLP_THRES
            TouchApproach       = 0x114, // RTC_CNTL_TOUCH_APPROACH
            TouchFilterControl  = 0x118, // RTC_CNTL_TOUCH_FILTER_CTRL
            UsbConfig           = 0x11C, // RTC_CNTL_USB_CONF
            TouchTimeoutControl = 0x120, // RTC_CNTL_TOUCH_TIMEOUT_CTRL
            SlpRejectCause      = 0x124, // RTC_CNTL_SLP_REJECT_CAUSE
            Options1            = 0x128, // RTC_CNTL_OPTIONS1
            SlpWakeupCause      = 0x12C, // RTC_CNTL_SLP_WAKEUP_CAUSE
            UlpCpTimer1         = 0x130, // RTC_CNTL_ULP_CP_TIMER_1
            Date                = 0x138, // RTC_CNTL_DATE
        }

        private enum RegistersESP32S3 : long
        {
            Rtc                 = 0x084, // RTC_CNTL_RTC
            RegulatorDrvControl = 0x08C, // RTC_CNTL_REGULATOR_DRV_CTRL
            DigPwc              = 0x090, // RTC_CNTL_DIG_PWC
            DigIso              = 0x094, // RTC_CNTL_DIG_ISO
            Wdtconfig0          = 0x098, // RTC_CNTL_WDTCONFIG0
            Wdtconfig1          = 0x09C, // RTC_CNTL_WDTCONFIG1
            Wdtconfig2          = 0x0A0, // RTC_CNTL_WDTCONFIG2
            Wdtconfig3          = 0x0A4, // RTC_CNTL_WDTCONFIG3
            Wdtconfig4          = 0x0A8, // RTC_CNTL_WDTCONFIG4
            Wdtfeed             = 0x0AC, // RTC_CNTL_WDTFEED
            Wdtwprotect         = 0x0B0, // RTC_CNTL_WDTWPROTECT
            SwdConfig           = 0x0B4, // RTC_CNTL_SWD_CONF
            SwdWprotect         = 0x0B8, // RTC_CNTL_SWD_WPROTECT
            SwCpuStall          = 0x0BC, // RTC_CNTL_SW_CPU_STALL
            Store4              = 0x0C0, // RTC_CNTL_STORE4
            Store5              = 0x0C4, // RTC_CNTL_STORE5
            Store6              = 0x0C8, // RTC_CNTL_STORE6
            Store7              = 0x0CC, // RTC_CNTL_STORE7
            LowPowerStatus      = 0x0D0, // RTC_CNTL_LOW_POWER_ST
            Diag0               = 0x0D4, // RTC_CNTL_DIAG0
            PadHold             = 0x0D8, // RTC_CNTL_PAD_HOLD
            DigPadHold          = 0x0DC, // RTC_CNTL_DIG_PAD_HOLD
            ExtWakeup1          = 0x0E0, // RTC_CNTL_EXT_WAKEUP1
            ExtWakeup1Status    = 0x0E4, // RTC_CNTL_EXT_WAKEUP1_STATUS
            BrownOut            = 0x0E8, // RTC_CNTL_BROWN_OUT
            TimeLow1            = 0x0EC, // RTC_CNTL_TIME_LOW1
            TimeHigh1           = 0x0F0, // RTC_CNTL_TIME_HIGH1
            Xtal32kClkFactor    = 0x0F4, // RTC_CNTL_XTAL32K_CLK_FACTOR
            Xtal32kConfig       = 0x0F8, // RTC_CNTL_XTAL32K_CONF
            UlpCpTimer          = 0x0FC, // RTC_CNTL_ULP_CP_TIMER
            UlpCpControl        = 0x100, // RTC_CNTL_ULP_CP_CTRL
            CocpuControl        = 0x104, // RTC_CNTL_COCPU_CTRL
            TouchCtrl1          = 0x108, // RTC_CNTL_TOUCH_CTRL1
            TouchCtrl2          = 0x10C, // RTC_CNTL_TOUCH_CTRL2
            TouchScanControl    = 0x110, // RTC_CNTL_TOUCH_SCAN_CTRL
            TouchSlpThres       = 0x114, // RTC_CNTL_TOUCH_SLP_THRES
            TouchApproach       = 0x118, // RTC_CNTL_TOUCH_APPROACH
            TouchFilterControl  = 0x11C, // RTC_CNTL_TOUCH_FILTER_CTRL
            UsbConfig           = 0x120, // RTC_CNTL_USB_CONF
            TouchTimeoutControl = 0x124, // RTC_CNTL_TOUCH_TIMEOUT_CTRL
            SlpRejectCause      = 0x128, // RTC_CNTL_SLP_REJECT_CAUSE
            Option1             = 0x12C, // RTC_CNTL_OPTION1
            SlpWakeupCause      = 0x130, // RTC_CNTL_SLP_WAKEUP_CAUSE
            UlpCpTimer1         = 0x134, // RTC_CNTL_ULP_CP_TIMER_1
            IntEnableRtcW1ts    = 0x138, // RTC_CNTL_INT_ENA_RTC_W1TS
            IntEnableRtcW1tc    = 0x13C, // RTC_CNTL_INT_ENA_RTC_W1TC
            RetentionControl    = 0x140, // RTC_CNTL_RETENTION_CTRL
            PgControl           = 0x144, // RTC_CNTL_PG_CTRL
            FibSel              = 0x148, // RTC_CNTL_FIB_SEL
            TouchDac            = 0x14C, // RTC_CNTL_TOUCH_DAC
            TouchDac1           = 0x150, // RTC_CNTL_TOUCH_DAC1
            CocpuDisable        = 0x154, // RTC_CNTL_COCPU_DISABLE
            Date                = 0x1FC, // RTC_CNTL_DATE
        }
    }
}
