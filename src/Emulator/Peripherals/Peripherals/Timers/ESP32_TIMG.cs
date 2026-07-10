//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ESP32_TIMG : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32_TIMG(IMachine machine, ESP32Generation generation = ESP32Generation.ESP32S3, ulong xtalFrequency = 40000000) : base(machine)
        {
            this.generation = generation;
            this.xtalFrequency = xtalFrequency;
            generationMapper = new RegisterMapper(typeof(Registers));
            generationMapper.RegisterEnumMapping(generation switch
            {
                ESP32Generation.ESP32 => typeof(RegistersESP32),
                ESP32Generation.ESP32S2 => typeof(RegistersESP32S2),
                ESP32Generation.ESP32S3 => typeof(RegistersESP32S3),
                _ => throw new ConstructionException($"{generation} is not supported by this model"),
            });
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            calibrationValue = 0;
        }

        public override string OffsetToString(long offset) => generationMapper.ToString(offset);

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.RtcCalibrationConfig.Define(this, 0x0001_3000)
                .WithReservedBits(0, 12)
                .WithFlag(12, name: "RTC_CALI_START_CYCLING")
                .WithValueField(13, 2, out clockSelect, name: "RTC_CALI_CLK_SEL")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => true, name: "RTC_CALI_RDY")
                .WithValueField(16, 15, out maxCycles, name: "RTC_CALI_MAX")
                .WithFlag(31, FieldMode.Read | FieldMode.Write, valueProviderCallback: _ => false,
                    writeCallback: (_, value) => { if(value) { Calibrate(); } }, name: "RTC_CALI_START");

            Registers.RtcCalibrationConfig1.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "RTC_CALI_CYCLING_DATA_VLD")
                .WithReservedBits(1, 6)
                .WithValueField(7, 25, FieldMode.Read, valueProviderCallback: _ => calibrationValue, name: "RTC_CALI_VALUE");

            // RTCCALICFG2 holds the calibration timeout; the ESP32 has no such register.
            switch(generation)
            {
            case ESP32Generation.ESP32S2:
                DefineCalibrationTimeout((long)RegistersESP32S2.RtcCalibrationConfig2);
                break;
            case ESP32Generation.ESP32S3:
                DefineCalibrationTimeout((long)RegistersESP32S3.RtcCalibrationConfig2);
                break;
            }
        }

        private void DefineCalibrationTimeout(long offset)
        {
            RegistersCollection.DefineRegister(offset, 0xFFFF_FF98)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "RTC_CALI_TIMEOUT")
                .WithReservedBits(1, 2)
                .WithValueField(3, 4, name: "RTC_CALI_TIMEOUT_RST_CNT")
                .WithValueField(7, 25, name: "RTC_CALI_TIMEOUT_THRES");
        }

        private void Calibrate()
        {
            ulong clockFrequency;
            switch(clockSelect.Value)
            {
            case 1:
                clockFrequency = RcFastDividedFrequency;
                break;
            case 2:
                clockFrequency = Xtal32kFrequency;
                break;
            default:
                clockFrequency = RcSlowFrequency;
                break;
            }
            // The hardware counts crystal cycles over `maxCycles` periods of the selected clock.
            calibrationValue = (uint)((maxCycles.Value * xtalFrequency / clockFrequency) & CalibrationValueMask);
        }

        private uint calibrationValue;
        private IValueRegisterField clockSelect;
        private IValueRegisterField maxCycles;

        private readonly ESP32Generation generation;
        private readonly ulong xtalFrequency;
        private readonly RegisterMapper generationMapper;

        private const ulong RcSlowFrequency = 136000;
        private const ulong RcFastDividedFrequency = 68359; // RC_FAST divided by 256
        private const ulong Xtal32kFrequency = 32768;
        private const ulong CalibrationValueMask = 0x1FFFFFF;

        // Shared
        private enum Registers : long
        {
            Timer0Config          = 0x000, // TIMG_T0CONFIG
            Timer0Low             = 0x004, // TIMG_T0LO
            Timer0High            = 0x008, // TIMG_T0HI
            Timer0Update          = 0x00C, // TIMG_T0UPDATE
            Timer0AlarmLow        = 0x010, // TIMG_T0ALARMLO
            Timer0AlarmHigh       = 0x014, // TIMG_T0ALARMHI
            Timer0LoadLow         = 0x018, // TIMG_T0LOADLO
            Timer0LoadHigh        = 0x01C, // TIMG_T0LOADHI
            Timer0Load            = 0x020, // TIMG_T0LOAD
            Timer1Config          = 0x024, // TIMG_T1CONFIG
            Timer1Low             = 0x028, // TIMG_T1LO
            Timer1High            = 0x02C, // TIMG_T1HI
            Timer1Update          = 0x030, // TIMG_T1UPDATE
            Timer1AlarmLow        = 0x034, // TIMG_T1ALARMLO
            Timer1AlarmHigh       = 0x038, // TIMG_T1ALARMHI
            Timer1LoadLow         = 0x03C, // TIMG_T1LOADLO
            Timer1LoadHigh        = 0x040, // TIMG_T1LOADHI
            Timer1Load            = 0x044, // TIMG_T1LOAD
            WatchdogConfig0       = 0x048, // TIMG_WDTCONFIG0
            WatchdogConfig1       = 0x04C, // TIMG_WDTCONFIG1
            WatchdogConfig2       = 0x050, // TIMG_WDTCONFIG2
            WatchdogConfig3       = 0x054, // TIMG_WDTCONFIG3
            WatchdogConfig4       = 0x058, // TIMG_WDTCONFIG4
            WatchdogConfig5       = 0x05C, // TIMG_WDTCONFIG5
            WatchdogFeed          = 0x060, // TIMG_WDTFEED
            WatchdogWriteProtect  = 0x064, // TIMG_WDTWPROTECT
            RtcCalibrationConfig  = 0x068, // TIMG_RTCCALICFG
            RtcCalibrationConfig1 = 0x06C, // TIMG_RTCCALICFG1
        }

        private enum RegistersESP32 : long
        {
            LactConfig            = 0x070, // TIMG_LACTCONFIG
            LactRtc               = 0x074, // TIMG_LACTRTC
            LactLow               = 0x078, // TIMG_LACTLO
            LactHigh              = 0x07C, // TIMG_LACTHI
            LactUpdate            = 0x080, // TIMG_LACTUPDATE
            LactAlarmLow          = 0x084, // TIMG_LACTALARMLO
            LactAlarmHigh         = 0x088, // TIMG_LACTALARMHI
            LactLoadLow           = 0x08C, // TIMG_LACTLOADLO
            LactLoadHigh          = 0x090, // TIMG_LACTLOADHI
            LactLoad              = 0x094, // TIMG_LACTLOAD
            InterruptEnableTimers = 0x098, // TIMG_INT_ENA_TIMERS
            InterruptRawTimers    = 0x09C, // TIMG_INT_RAW_TIMERS
            InterruptStatusTimers = 0x0A0, // TIMG_INT_ST_TIMERS
            InterruptClearTimers  = 0x0A4, // TIMG_INT_CLR_TIMERS
            NTimersDate           = 0x0F8, // TIMG_NTIMERS_DATE
            TimerGroupClock       = 0x0FC, // TIMG_TIMGCLK
        }

        private enum RegistersESP32S2 : long
        {
            LactConfig            = 0x070, // TIMG_LACTCONFIG
            LactRtc               = 0x074, // TIMG_LACTRTC
            LactLow               = 0x078, // TIMG_LACTLO
            LactHigh              = 0x07C, // TIMG_LACTHI
            LactUpdate            = 0x080, // TIMG_LACTUPDATE
            LactAlarmLow          = 0x084, // TIMG_LACTALARMLO
            LactAlarmHigh         = 0x088, // TIMG_LACTALARMHI
            LactLoadLow           = 0x08C, // TIMG_LACTLOADLO
            LactLoadHigh          = 0x090, // TIMG_LACTLOADHI
            LactLoad              = 0x094, // TIMG_LACTLOAD
            InterruptEnableTimers = 0x098, // TIMG_INT_ENA_TIMERS
            InterruptRawTimers    = 0x09C, // TIMG_INT_RAW_TIMERS
            InterruptStatusTimers = 0x0A0, // TIMG_INT_ST_TIMERS
            InterruptClearTimers  = 0x0A4, // TIMG_INT_CLR_TIMERS
            RtcCalibrationConfig2 = 0x0A8, // TIMG_RTCCALICFG2
            TimersDate            = 0x0F8, // TIMG_TIMERS_DATE
            RegisterClock         = 0x0FC, // TIMG_REGCLK
        }

        private enum RegistersESP32S3 : long
        {
            InterruptEnableTimers = 0x070, // TIMG_INT_ENA_TIMERS
            InterruptRawTimers    = 0x074, // TIMG_INT_RAW_TIMERS
            InterruptStatusTimers = 0x078, // TIMG_INT_ST_TIMERS
            InterruptClearTimers  = 0x07C, // TIMG_INT_CLR_TIMERS
            RtcCalibrationConfig2 = 0x080, // TIMG_RTCCALICFG2
            NTimersDate           = 0x0F8, // TIMG_NTIMERS_DATE
            RegisterClock         = 0x0FC, // TIMG_REGCLK
        }
    }
}
