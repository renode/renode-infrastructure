//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MAX32650_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_RTC(Machine machine, bool subSecondsMSBOverwrite = false, string baseDateTime = null) : base(machine)
        {
            BaseDateTime = Misc.UnixEpoch;
            if(baseDateTime != null)
            {
                if(!DateTime.TryParse(baseDateTime, out var parsedBaseDateTime))
                {
                    throw new Exceptions.ConstructionException($"Invalid 'baseDateTime': {baseDateTime}");
                }
                BaseDateTime = parsedBaseDateTime;
            }
            machine.RealTimeClockModeChanged += _ => SetDateTimeFromMachine();

            DefineRegisters();

            internalClock = new LimitTimer(machine.ClockSource, SubSecondCounterResolution, this, "rtc_tick", limit: 1, enabled: false, eventEnabled: true);
            internalClock.LimitReached += SubsecondTick;

            this.subSecondsMSBOverwrite = subSecondsMSBOverwrite;

            IRQ = new GPIO();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            internalClock.Reset();

            secondsCounter = 0;
            subSecondAlarmCounter = 0;
            subSecondsCounter = 0;

            SetDateTimeFromMachine(hushLog: true);
        }

        public string PrintPreciseCurrentDateTime()
        {
            return CurrentDateTime.ToString("o");
        }

        public void SetDateTime(int? year = null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, double? millisecond = null)
        {
            SetDateTime(CurrentDateTime.With(year, month, day, hour, minute, second, millisecond));
        }

        public DateTime BaseDateTime { get; }

        public DateTime CurrentDateTime => BaseDateTime + TimePassedSinceBaseDateTime;

        public GPIO IRQ { get; }

        public long Size => 0x400;

        public byte SubSecondsSignificantBits => (byte)(subSecondsCounter >> 8);

        public TimeSpan TimePassedSinceBaseDateTime => TimeSpan.FromSeconds(secondsCounter + ((double)subSecondsCounter / SubSecondCounterResolution));

        private static uint CalculateSubSeconds(double seconds)
        {
            var subSecondFraction = seconds % 1;
            return (uint)(subSecondFraction * SubSecondCounterResolution);
        }

        private void DefineRegisters()
        {
            Registers.Seconds.Define(this)
                .WithValueField(0, 32, name: "RTC_SEC.sec",
                    valueProviderCallback: _ => secondsCounter,
                    writeCallback: (_, value) => { lock(countersLock) secondsCounter = (uint)value; });
            Registers.SubSeconds.Define(this)
                .WithValueField(0, 8, name: "RTC_SSEC.ssec",
                    valueProviderCallback: _ => (byte)subSecondsCounter,
                    writeCallback: (_, value) => { lock(countersLock) subSecondsCounter = (subSecondsMSBOverwrite ? 0xF00 : (subSecondsCounter & 0xF00)) | (uint)value; })
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

        private void SetDateTime(DateTime dateTime, bool hushLog = false)
        {
            if(dateTime < BaseDateTime)
            {
                this.Log(LogLevel.Warning, "Tried to set DateTime older than the RTC's BaseDateTime ({0}): {1:o}", BaseDateTime, dateTime);
                return;
            }
            var sinceBaseDateTime = dateTime - BaseDateTime;

            lock(countersLock)
            {
                secondsCounter = (uint)sinceBaseDateTime.TotalSeconds;
                subSecondsCounter = CalculateSubSeconds(sinceBaseDateTime.TotalSeconds);

                if(!hushLog)
                {
                    this.Log(LogLevel.Info, "New date time set: {0:o}", CurrentDateTime);
                }
            }
        }

        private void SetDateTimeFromMachine(bool hushLog = false)
        {
            SetDateTime(machine.RealTimeClockDateTime, hushLog);
        }

        private void SubsecondTick()
        {
            lock(countersLock)
            {
                subSecondsCounter += 1;
                if(subSecondsCounter >= SubSecondCounterResolution)
                {
                    subSecondsCounter = 0;
                    secondsCounter += 1;

                    if(timeOfDayAlarmEnabled.Value && (secondsCounter & TimeOfDayAlarmMask) == timeOfDayAlarm.Value)
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
        }

        private void UpdateInterrupts()
        {
            var interruptPending = readyInterruptEnabled.Value;
            interruptPending |= timeOfDayAlarmEnabled.Value && timeOfDayAlarmFlag.Value;
            interruptPending |= subSecondAlarmEnabled.Value && subSecondAlarmFlag.Value;
            IRQ.Set(interruptPending);
        }

        private uint secondsCounter;
        private ulong subSecondAlarmCounter;
        private uint subSecondsCounter;

        private IFlagRegisterField canBeToggled;
        private IFlagRegisterField readyInterruptEnabled;
        private IFlagRegisterField subSecondAlarmEnabled;
        private IFlagRegisterField subSecondAlarmFlag;
        private IFlagRegisterField timeOfDayAlarmEnabled;
        private IFlagRegisterField timeOfDayAlarmFlag;

        private IValueRegisterField subSecondAlarm;
        private IValueRegisterField timeOfDayAlarm;

        private readonly object countersLock = new object();
        private readonly LimitTimer internalClock;
        private readonly bool subSecondsMSBOverwrite;

        private const ulong SubSecondAlarmMaxValue = 0xFFFFFFFF;
        private const uint SubSecondCounterResolution = 4096;
        private const ulong TimeOfDayAlarmMask = 0xFFFFF;

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
