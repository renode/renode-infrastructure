//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using System;
using static Antmicro.Renode.Utilities.BitHelper;
using Antmicro.Renode.Utilities;
using System.Globalization;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PSE_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public PSE_RTC(Machine machine) : base(machine)
        {
            DefineRegisters();
            WakeupIRQ = new GPIO();
            MatchIRQ = new GPIO();

            ticker = new LimitTimer(machine.ClockSource, 1, this, nameof(ticker), 1, Direction.Ascending, eventEnabled: true);
            ticker.LimitReached += HandleTick;
            ResetInnerTimer();
        }

        public override void Reset()
        {
            base.Reset();
            WakeupIRQ.Set(false);
            MatchIRQ.Set(false);
            ResetInnerTimer();
            ticker.Reset();
        }

        public GPIO MatchIRQ { get; private set; }
        public GPIO WakeupIRQ { get; private set; }
        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, writeCallback: (_, value) => { if(value) ticker.Enabled = true; }, valueProviderCallback: _ => ticker.Enabled, name: "Start/Running")
                .WithFlag(1, writeCallback: (_, value) => { if(value) ticker.Enabled = false; }, valueProviderCallback: _ => ticker.Enabled, name: "Stop/Running")
                .WithFlag(2, writeCallback: (_, value) => { if(value) alarmEnabled = true; }, valueProviderCallback: _ => alarmEnabled, name: "Alarm_on/Alarm_enabled")
                .WithFlag(3, writeCallback: (_, value) => { if(value) alarmEnabled = false; }, valueProviderCallback: _ => alarmEnabled, name: "Alarm_off/Alarm_enabled")
                .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) ResetInnerTimer(); }, name: "Reset")
                .WithFlag(5, writeCallback: (_, value) => { if(value) { currentTime = timeToUpload; }; }, name: "Upload")
                .WithFlag(6, writeCallback: (_, value) => { if(value) { timeToUpload = currentTime; }; }, name: "Download")
                .WithFlag(7, out match, FieldMode.Read, name: "Match")
                .WithFlag(8, out wakeup, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) WakeupIRQ.Set(false) ; }, name: "Wakeup_clear/Wakeup")
                .WithFlag(9, FieldMode.Write, writeCallback: (_, value) => { if(value) { wakeup.Value = true; WakeupIRQ.Set(true); } }, name: "Wakeup_set")
                .WithFlag(10, out updated, FieldMode.Read | FieldMode.WriteOneToClear, name: "Updated")
            ;

            Registers.Mode.Define(this)
                .WithEnumField<DoubleWordRegister, ClockMode>(0, 1, out clockMode, changeCallback: (_, __) => UpdateState(), name: "clock_mode")
                .WithFlag(1, out wakeEnable, name: "wake_enable")
                .WithFlag(2, out wakeReset, name: "wake_reset")
                .WithFlag(3, out wakeContinue, name: "wake_continue")
                .WithTag("wake_reset_ps", 4, 1) // we don't support prescaler at all
            ;

            Registers.AlarmLow.Define(this)
                .WithValueField(0, 32, out alarmLow, changeCallback: (_, __) => UpdateState(), name: "alarm Lower")
            ;

            Registers.AlarmHigh.Define(this)
                .WithValueField(0, 32, out alarmHigh, changeCallback: (_, __) => UpdateState(), name: "alarm Upper")
            ;

            Registers.CompareLow.Define(this)
                .WithValueField(0, 32, out compareLow, changeCallback: (_, __) => UpdateState(), name: "compare Lower")
            ;

            Registers.CompareHigh.Define(this)
                .WithValueField(0, 32, out compareHigh, changeCallback: (_, __) => UpdateState(), name: "compare Higher")
            ;

            Registers.DateTimeLow.Define(this)
                .WithValueField(0, 32, name: "datetime Lower",
                    valueProviderCallback: _ =>
                    {
                        switch(clockMode.Value)
                        {
                            case ClockMode.BinaryCounter:
                                return (uint)CalculateElapsedSeconds(currentTime);
                            case ClockMode.DateTimeCounter:
                                return GetDateTimeAlarmCompareLower().Bits.AsUInt32();
                            default:
                                throw new ArgumentException("Unexpected clock mode");
                        }
                    },
                    writeCallback: (_, value) =>
                    {
                        switch(clockMode.Value)
                        {
                            case ClockMode.BinaryCounter:
                                var currentValue = CalculateElapsedSeconds(timeToUpload);
                                var newValue = currentValue.ReplaceBits(source: value, width: 32);
                                timeToUpload = ResetTimeValue.AddSeconds(newValue);
                                break;
                            case ClockMode.DateTimeCounter:
                                timeToUpload = timeToUpload.With(
                                    second: (int)BitHelper.GetMaskedValue(value, 0, 8),
                                    minute: (int)BitHelper.GetMaskedValue(value, 8, 8),
                                    hour: (int)BitHelper.GetMaskedValue(value, 16, 8),
                                    day: (int)BitHelper.GetMaskedValue(value, 24, 8)
                                );
                                break;
                            default:
                                throw new ArgumentException("Unexpected clock mode");
                        }
                    })
            ;

            Registers.DateTimeHigh.Define(this)
                .WithValueField(0, 32, name: "datetime Higher",
                    valueProviderCallback: _ =>
                    {
                        switch(clockMode.Value)
                        {
                            case ClockMode.BinaryCounter:
                                return (uint)(CalculateElapsedSeconds(currentTime) >> 32);
                            case ClockMode.DateTimeCounter:
                                return GetDateTimeAlarmCompareUpper().Bits.AsUInt32();
                            default:
                                throw new ArgumentException("Unexpected clock mode");
                        }
                    },
                    writeCallback: (_, value) =>
                    {
                        switch(clockMode.Value)
                        {
                            case ClockMode.BinaryCounter:
                                var currentValue = CalculateElapsedSeconds(timeToUpload);
                                var newValue = currentValue.ReplaceBits(source: value, width: 11, destinationPosition: 32);
                                timeToUpload = ResetTimeValue.AddSeconds(newValue);
                                break;
                            case ClockMode.DateTimeCounter:
                                timeToUpload = timeToUpload.With(
                                    month: (int)BitHelper.GetMaskedValue(value, 0, 8),
                                    year: (int)BitHelper.GetMaskedValue(value, 8, 8)
                                    // WARNING
                                    // -------
                                    // The rest of bits:
                                    // bits 16-23: weekday,
                                    // bits 24-29: week
                                    // are intentionally *ignored* as those values can be inferred from day+month+year.
                                    // This might lead to an inconsistency between Renode and actual hardware.
                                );
                                break;
                            default:
                                throw new ArgumentException("Unexpected clock mode");
                        }
                    })
            ;

            Registers.DateTimeSynchronizedSeconds.Define(this)
                .WithValueField(0, 6, name: "Second",
                    valueProviderCallback: _ =>
                    {
                        // an excerpt from the documentation:
                        // "The complete RTC data is read and stored internally when the second value is read,
                        // reads of minutes etc returns the value when seconds was read."
                        bufferedCurrentTime = currentTime;
                        return (uint)bufferedCurrentTime.Second;
                    },
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(second: (int)value))
            ;

            Registers.DateTimeSynchronizedMinutes.Define(this)
                .WithValueField(0, 6, name: "Minute",
                    valueProviderCallback: _ => (uint)bufferedCurrentTime.Minute,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(minute: (int)value))
            ;

            Registers.DateTimeSynchronizedHours.Define(this)
                .WithValueField(0, 5, name: "Hour",
                    valueProviderCallback: _ => (uint)bufferedCurrentTime.Hour,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(hour: (int)value))
            ;

            Registers.DateTimeSynchronizedDay.Define(this)
                .WithValueField(0, 5, name: "Day",
                    valueProviderCallback: _ => (uint)bufferedCurrentTime.Day,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(day: (int)value))
            ;

            Registers.DateTimeSynchronizedMonth.Define(this)
                .WithValueField(0, 4, name: "Month",
                    valueProviderCallback: _ => (uint)bufferedCurrentTime.Month,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(month: (int)value))
            ;

            Registers.DateTimeSynchronizedYear.Define(this)
                .WithValueField(0, 8, name: "Year",
                    valueProviderCallback: _ => CalculateYear(bufferedCurrentTime),
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(year: CalculateYear(value)))
            ;

            Registers.DateTimeSynchronizedWeekday.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => CalculateWeekday(bufferedCurrentTime), name: "Weekday")
                // WARNING
                // -------
                // The write to this register intentionally *ignored* as the weekday can be inferred from day+month+year.
                // This might lead to an inconsistency between Renode and actual hardware.
            ;

            Registers.DateTimeSynchronizedWeek.Define(this)
                .WithValueField(0, 6, valueProviderCallback: _ => CalculateWeek(bufferedCurrentTime), name: "Week")
                // WARNING
                // -------
                // The write to this register intentionally *ignored* as the week number can be inferred from day+month+year.
                // This might lead to an inconsistency between Renode and actual hardware.
            ;

            Registers.DateTimeSeconds.Define(this)
                .WithValueField(0, 6, name: "Second",
                    valueProviderCallback: _ => (uint)currentTime.Second,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(second: (int)value))
            ;

            Registers.DateTimeMinutes.Define(this)
                .WithValueField(0, 6, name: "Minute",
                    valueProviderCallback: _ => (uint)currentTime.Minute,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(minute: (int)value))
            ;

            Registers.DateTimeHours.Define(this)
                .WithValueField(0, 5, name: "Hour",
                    valueProviderCallback: _ => (uint)currentTime.Hour,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(hour: (int)value))
            ;

            Registers.DateTimeDay.Define(this)
                .WithValueField(0, 5, name: "Day",
                    valueProviderCallback: _ => (uint)currentTime.Day,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(day: (int)value))
            ;

            Registers.DateTimeMonth.Define(this)
                .WithValueField(0, 4, name: "Month",
                    valueProviderCallback: _ => (uint)currentTime.Month,
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(month: (int)value))
            ;

            Registers.DateTimeYear.Define(this)
                // year 0 means 2000
                .WithValueField(0, 8, name: "Year",
                    valueProviderCallback: _ => CalculateYear(currentTime),
                    writeCallback: (_, value) => timeToUpload = timeToUpload.With(year: CalculateYear(value)))
            ;

            Registers.DateTimeWeekday.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => CalculateWeekday(currentTime), name: "Weekday")
                // WARNING
                // -------
                // The write to this register intentionally *ignored* as the weekday can be inferred from day+month+year.
                // This might lead to an inconsistency between Renode and actual hardware.
            ;

            Registers.DateTimeWeek.Define(this)
                .WithValueField(0, 6, valueProviderCallback: _ => CalculateWeek(currentTime), name: "Week")
                // WARNING
                // -------
                // The write to this register intentionally *ignored* as the week number can be inferred from day+month+year.
                // This might lead to an inconsistency between Renode and actual hardware.
            ;
        }

        private uint CalculateWeek(DateTime dt)
        {
            // documentation says:
            // "The day of the week counter increments from 1 to 7 and the week counter is incremented as the day of week goes from 7 to 1."
            // "Weekday, 1: Sunday, 2: Monday ….  7:Saturday"
            return (uint)CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(dt, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }

        private uint CalculateYear(DateTime dt)
        {
            // documentation says:
            // "Reset date is Saturday 1 January 2000."
            return (uint)(dt.Year - 2000);
        }

        private int CalculateYear(uint year)
        {
            // documentation says:
            // "Reset date is Saturday 1 January 2000."
            return (int)year + 2000;
        }

        private uint CalculateWeekday(DateTime dt)
        {
            // documentation says:
            // "Weekday, 1: Sunday, 2: Monday ….  7:Saturday"
            return (uint)currentTime.DayOfWeek + 1;
        }

        private void ResetInnerTimer()
        {
            currentTime = ResetTimeValue;
            timeToUpload = new DateTime();
        }

        private ulong CalculateElapsedSeconds(DateTime dt)
        {
            return (ulong)(dt - ResetTimeValue).TotalSeconds;
        }

        private BitConcatenator GetDateTimeAlarmCompareLower(BitConcatenator source = null)
        {
             return (source ?? BitConcatenator.New())
                .StackAbove((uint)currentTime.Second, 8)
                .StackAbove((uint)currentTime.Minute, 8)
                .StackAbove((uint)currentTime.Hour, 8)
                .StackAbove((uint)currentTime.Day, 8);
        }

        private BitConcatenator GetDateTimeAlarmCompareUpper(BitConcatenator source = null)
        {
            return (source ?? BitConcatenator.New())
                .StackAbove((uint)currentTime.Month, 8)
                .StackAbove(CalculateYear(currentTime), 8)
                .StackAbove(CalculateWeekday(currentTime), 8)
                .StackAbove(CalculateWeek(currentTime), 6);
        }

        private void HandleTick()
        {
            currentTime = currentTime.AddSeconds(1);
            updated.Value = true;
            UpdateState();
        }

        private void UpdateState()
        {
            if(!alarmEnabled)
            {
                return;
            }

            var currentValue = (clockMode.Value == ClockMode.BinaryCounter)
                ? CalculateElapsedSeconds(currentTime)
                : GetDateTimeAlarmCompareUpper(GetDateTimeAlarmCompareLower()).Bits.AsUInt64();
            var compareMask = (compareHigh.Value << 32) | compareLow.Value;
            var alarmValue = (alarmHigh.Value << 32) | alarmLow.Value;
            var isAlarmMatched = (currentValue & compareMask) == (alarmValue & compareMask);

            match.Value = isAlarmMatched;
            MatchIRQ.Set(match.Value);

            wakeup.Value |= isAlarmMatched;
            if(wakeup.Value)
            {
                if(wakeReset.Value)
                {
                    ResetInnerTimer();
                }
                if(!wakeContinue.Value)
                {
                    ticker.Enabled = false;
                }
                if(wakeEnable.Value)
                {
                    WakeupIRQ.Set(true);
                }
            }
        }

        private IValueRegisterField compareLow;
        private IValueRegisterField compareHigh;
        private IValueRegisterField alarmLow;
        private IValueRegisterField alarmHigh;
        private IFlagRegisterField match;
        private IFlagRegisterField wakeup;
        private IFlagRegisterField wakeEnable;
        private IFlagRegisterField wakeReset;
        private IFlagRegisterField wakeContinue;
        private IFlagRegisterField updated;
        private IEnumRegisterField<ClockMode> clockMode;

        private bool alarmEnabled;
        private DateTime timeToUpload;
        private DateTime currentTime;
        private DateTime bufferedCurrentTime;

        private readonly LimitTimer ticker;

        private static readonly DateTime ResetTimeValue = new DateTime(2000, 1, 1);

        private enum ClockMode
        {
            BinaryCounter = 0,
            DateTimeCounter = 1
        }

        private enum Registers
        {
            Control = 0x0,
            Mode = 0x4,
            Prescaler = 0x8,
            AlarmLow = 0xC,
            AlarmHigh = 0x10,
            CompareLow = 0x14,
            CompareHigh = 0x18,
            DateTimeLow = 0x20,
            DateTimeHigh = 0x24,
            DateTimeSynchronizedSeconds = 0x30,
            DateTimeSynchronizedMinutes= 0x34,
            DateTimeSynchronizedHours = 0x38,
            DateTimeSynchronizedDay = 0x3C,
            DateTimeSynchronizedMonth = 0x40,
            DateTimeSynchronizedYear = 0x44,
            DateTimeSynchronizedWeekday = 0x48,
            DateTimeSynchronizedWeek = 0x4C,
            DateTimeSeconds = 0x50,
            DateTimeMinutes= 0x54,
            DateTimeHours = 0x58,
            DateTimeDay = 0x5C,
            DateTimeMonth = 0x60,
            DateTimeYear = 0x64,
            DateTimeWeekday = 0x68,
            DateTimeWeek = 0x6C,
        }
    }
}
