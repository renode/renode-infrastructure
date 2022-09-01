//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MAX32650_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_RTC(Machine machine, bool subSecondsMSBOverwrite = false) : base(machine)
        {
            DefineRegisters();

            internalClock = new LimitTimer(machine.ClockSource, 0x1000, this, "rtc_tick", limit: 1, enabled: false, eventEnabled: true);
            internalClock.LimitReached += SubsecondTick;

            this.subSecondsMSBOverwrite = subSecondsMSBOverwrite;

            IRQ = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            internalClock.Reset();

            subSecondAlarmCounter = 0;
            subSecondsCounter = 0;
        }

        public byte SubSecondsSignificantBits => (byte)(subSecondsCounter >> 8);

        public long Size => 0x400;

        public GPIO IRQ { get; }

        private void SubsecondTick()
        {
            subSecondsCounter += 1;
            if(subSecondsCounter > SubSecondTickLimit)
            {
                subSecondsCounter = 0;
                secondsCounter.Value += 1;

                if(timeOfDayAlarmEnabled.Value && (secondsCounter.Value & TimeOfDayAlarmMask) == timeOfDayAlarm.Value)
                {
                    timeOfDayAlarmFlag.Value = true;
                }
            }

            if(subSecondAlarmEnabled.Value)
            {
                subSecondAlarmCounter += 1;
                if(subSecondAlarmCounter > SubSecondAlarmMaxValue)
                {
                    subSecondAlarmCounter = subSecondAlarm.Value;
                    subSecondAlarmFlag.Value = true;
                }
            }

            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.Seconds.Define(this)
                .WithValueField(0, 32, out secondsCounter, name: "RTC_SEC.sec");
            Registers.SubSeconds.Define(this)
                .WithValueField(0, 8, name: "RTC_SSEC.ssec",
                    valueProviderCallback: _ => (byte)subSecondsCounter,
                    writeCallback: (_, value) => subSecondsCounter = (subSecondsMSBOverwrite ? 0xF00 : (subSecondsCounter & 0xF00)) | value)
                .WithReservedBits(8, 24);
            Registers.TimeOfDayAlarm.Define(this)
                .WithValueField(0, 20, out timeOfDayAlarm, name: "RTC_TODA.tod_alarm")
                .WithReservedBits(20, 12);
            Registers.SubSecondAlarm.Define(this)
                .WithValueField(0, 32, out subSecondAlarm, name: "RTC_SSECA.ssec_alarm");
            Registers.Control.Define(this)
                .WithFlag(0, name: "RTC_CTRL.enable",
                    valueProviderCallback: _ => internalClock.Enabled,
                    changeCallback: (_, value) =>
                    {
                        if(!canBeToggled.Value)
                        {
                            this.Log(LogLevel.Warning, "Tried to write RTC_CTRL.enable with RTC_CTRL.write_en disabled");
                            return;
                        }
                        internalClock.Enabled = value;
                    })
                .WithFlag(1, out timeOfDayAlarmEnabled, name: "RTC_CTRL.tod_alarm_en")
                .WithFlag(2, out subSecondAlarmEnabled, name: "RTC_CTRL.ssec_alarm_en",
                    writeCallback: (_, value) =>
                    {
                        subSecondAlarmCounter = subSecondAlarm.Value;
                    })
                .WithFlag(3, name: "RTC_CTRL.busy", valueProviderCallback: _ => false)
                // It seems that on real HW, semantic of the READY bit is inverted, that is
                // when RTC_CTRL.ready is set to false, then software is able to read
                // correct data from RTC_SEC and RTC_SSEC registers.
                .WithFlag(4, name: "RTC_CTRL.ready", valueProviderCallback: _ => false)
                .WithFlag(5, out readyInterruptEnabled, name: "RTC_CTRL.ready_int_en")
                .WithFlag(6, out timeOfDayAlarmFlag, name: "RTC_CTRL.tod_alarm_fl")
                .WithFlag(7, out subSecondAlarmFlag, name: "RTC_CTRL.ssec_alarm_fl")
                .WithTaggedFlag("RTC_CTRL.sqwout_en", 8)
                .WithTag("RTC_CTRL.freq_sel", 9, 2)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("RTC_CTRL.acre", 14)
                .WithFlag(15, out canBeToggled, name: "RTC_CTRL.write_en")
                .WithReservedBits(16, 16);
            Registers.OscillatorControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("RTC_OSCCTRL.bypass", 4)
                .WithTaggedFlag("RTC_OSCCTRL.32kout", 5)
                .WithReservedBits(6, 26);
        }

        private void UpdateInterrupts()
        {
            var interruptPending = readyInterruptEnabled.Value;
            interruptPending |= timeOfDayAlarmEnabled.Value && timeOfDayAlarmFlag.Value;
            interruptPending |= subSecondAlarmEnabled.Value && subSecondAlarmFlag.Value;
            IRQ.Set(interruptPending);
        }

        private ulong subSecondAlarmCounter;
        private ulong subSecondsCounter;

        private IValueRegisterField secondsCounter;

        private IFlagRegisterField canBeToggled;
        private IFlagRegisterField readyInterruptEnabled;
        private IFlagRegisterField timeOfDayAlarmEnabled;
        private IFlagRegisterField subSecondAlarmEnabled;
        private IFlagRegisterField timeOfDayAlarmFlag;
        private IFlagRegisterField subSecondAlarmFlag;

        private IValueRegisterField timeOfDayAlarm;
        private IValueRegisterField subSecondAlarm;

        private readonly bool subSecondsMSBOverwrite;
        private readonly LimitTimer internalClock;

        private const ulong SubSecondTickLimit = 0xFFF;
        private const ulong TimeOfDayAlarmMask = 0xFFFFF;
        private const ulong SubSecondAlarmMaxValue = 0xFFFFFFFF;

        private enum Registers
        {
            Seconds = 0x00,
            SubSeconds = 0x04,
            TimeOfDayAlarm = 0x08,
            SubSecondAlarm = 0x0C,
            Control = 0x10,
            OscillatorControl = 0x18,
        }
    }
}
