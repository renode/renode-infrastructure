//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class AmbiqApollo4_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_RTC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            machine.RealTimeClockModeChanged += _ => SetDateTimeFromMachine();

            var baseDateTime = Misc.UnixEpoch;
            internalTimer = new RTCTimer(machine, this, baseDateTime, alarmAction: () => InterruptStatus = true);

            DefineRegisters();
            Reset();
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(offset == (long)Registers.CountersLower || offset == (long)Registers.CountersUpper)
            {
                // Cannot be done in read callback because field values are established first.
                UpdateCounterFields();
            }

            return base.ReadDoubleWord(offset);
        }

        public override void Reset()
        {
            interruptStatus = false;
            lastUpdateTimerValue = ulong.MaxValue;
            writeBusy = false;
            valueReadWithCountersLower = 0;

            InitializeBCDValueFields();
            IRQ.Unset();
            internalTimer.Reset();

            base.Reset();

            SetDateTimeFromMachine();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(!counterWritesEnabled.Value &&
                (offset == (long)Registers.CountersLower || offset == (long)Registers.CountersUpper))
            {
                this.Log(LogLevel.Warning, "The {0} register ({1}) cannot be written to; WRTC isn't set!", (Registers)offset, offset);
                return;
            }

            base.WriteDoubleWord(offset, value);
        }

        public string PrintNextAlarmDateTime()
        {
            return internalTimer.IsAlarmSet() ? internalTimer.GetNextAlarmDateTime().ToString("o") : "Alarm not set.";
        }

        public string PrintPreciseCurrentDateTime()
        {
            return CurrentDateTime.ToString("o");
        }

        public void SetDateTime(int? year = null, int? month = null, int? day = null, int? hours = null, int? minutes = null, int? seconds = null, int? secondHundredths = null)
        {
            UpdateCounterFields();

            if(year == null)
            {
                year = CalculateYear(centuryBit.Value, yearsOfCentury);
            }
            else
            {
                // The 200 years range simply makes it possible to tell a specific year from the century bit and a two-digit year.
                if(year < 1970 || year > 2169)
                {
                    throw new RecoverableException("Year has to be in range: 1970 .. 2169.");
                }
            }

            try
            {
                SetDateTimeInternal(new DateTime(
                    year.Value,
                    month ?? this.month,
                    day ?? this.day,
                    hours ?? this.hours,
                    minutes ?? this.minutes,
                    seconds ?? this.seconds,
                    (secondHundredths ?? this.secondHundredths) * 10));
            }
            catch(ArgumentOutOfRangeException)
            {
                throw new RecoverableException("Provided date or time is invalid.");
            }
        }

        public void SetDateTimeFromMachine()
        {
            // Normally the warning is logged if the millisecond value isn't a multiple of 10 since that's an RTC
            // precision. It doesn't make sense to log such a warning for the value taken from the machine.
            SetDateTimeInternal(machine.RealTimeClockDateTime, hushPrecisionWarning: true);
        }

        public DateTime CurrentDateTime => internalTimer.GetCurrentDateTime();

        public GPIO IRQ { get; }

        public long Size => 0x210;

        static private int CalculateYear(bool centuryBit, int yearsOfCentury)
        {
            // The century bit set indicates "1900s/2100s" according to the documentation.
            // To make it specific, the range supported has to be 200 years. The arbitrarily chosen range is 1970-2169
            // because of the default Machine's RTC Mode, 'Epoch', which starts in 1970. Hence the century bit set
            // translates to 1900 only if a two-digit year number ('yearsOfCentury') is >= 70 and to 2100 otherwise.
            if(centuryBit)
            {
                return (yearsOfCentury < 70 ? 2100 : 1900) + yearsOfCentury;
            }
            else
            {
                return 2000 + yearsOfCentury;
            }
        }

        private void DefineRegisters()
        {
            Registers.AlarmsLower.Define(this)
                .WithValueField(0, 8, name: "ALM100", writeCallback: (_, newValue) => alarmSecondHundredths.BCDSet((byte)newValue), valueProviderCallback: _ => alarmSecondHundredths.BCDGet())
                .WithValueField(8, 7, name: "ALMSEC", writeCallback: (_, newValue) => alarmSeconds.BCDSet((byte)newValue), valueProviderCallback: _ => alarmSeconds.BCDGet())
                .WithReservedBits(15, 1)
                .WithValueField(16, 7, name: "ALMMIN", writeCallback: (_, newValue) => alarmMinutes.BCDSet((byte)newValue), valueProviderCallback: _ => alarmMinutes.BCDGet())
                .WithReservedBits(23, 1)
                .WithValueField(24, 6, name: "ALMHR", writeCallback: (_, newValue) => alarmHours.BCDSet((byte)newValue), valueProviderCallback: _ => alarmHours.BCDGet())
                .WithReservedBits(30, 2)
                .WithChangeCallback((_, __) => UpdateAlarm())
                ;

            Registers.AlarmsUpper.Define(this)
                .WithValueField(0, 6, name: "ALMDATE", writeCallback: (_, newValue) => alarmDay.BCDSet((byte)newValue), valueProviderCallback: _ => alarmDay.BCDGet())
                .WithReservedBits(6, 2)
                .WithValueField(8, 5, name: "ALMMO", writeCallback: (_, newValue) => alarmMonth.BCDSet((byte)newValue), valueProviderCallback: _ => alarmMonth.BCDGet())
                .WithReservedBits(13, 3)
                .WithValueField(16, 3, name: "ALMWKDY", writeCallback: (_, newValue) => alarmWeekday.BCDSet((byte)newValue), valueProviderCallback: _ => alarmWeekday.BCDGet())
                .WithReservedBits(19, 13)
                .WithChangeCallback((_, __) => UpdateAlarm())
                ;

            Registers.Control.Define(this)
                .WithFlag(0, out counterWritesEnabled, name: "WRTC")
                .WithEnumField(1, 3, out alarmRepeatInterval, name: "RPT", changeCallback: (_, __) => UpdateAlarm())
                .WithFlag(4, name: "RSTOP", writeCallback: (_, newValue) => { internalTimer.Enabled = !newValue; }, valueProviderCallback: _ => !internalTimer.Enabled)
                .WithReservedBits(5, 27)
                ;

            Registers.CountersLower.Define(this)
                .WithValueField(0, 8, name: "CTR100", writeCallback: (_, newValue) => secondHundredths.BCDSet((byte)newValue), valueProviderCallback: _ => secondHundredths.BCDGet())
                .WithValueField(8, 7, name: "CTRSEC", writeCallback: (_, newValue) => seconds.BCDSet((byte)newValue), valueProviderCallback: _ => seconds.BCDGet())
                .WithReservedBits(15, 1)
                .WithValueField(16, 7, name: "CTRMIN", writeCallback: (_, newValue) => minutes.BCDSet((byte)newValue), valueProviderCallback: _ => minutes.BCDGet())
                .WithReservedBits(23, 1)
                .WithValueField(24, 6, name: "CTRHR", writeCallback: (_, newValue) => hours.BCDSet((byte)newValue), valueProviderCallback: _ => hours.BCDGet())
                .WithReservedBits(30, 2)
                .WithReadCallback((_, __) => { valueReadWithCountersLower = internalTimer.Value; readError.Value = false; })
                .WithWriteCallback((_, __) => writeBusy = true)
                ;

            Registers.CountersUpper.Define(this)
                .WithValueField(0, 6, name: "CTRDATE", writeCallback: (_, newValue) => day.BCDSet((byte)newValue), valueProviderCallback: _ => day.BCDGet())
                .WithReservedBits(6, 2)
                .WithValueField(8, 5, name: "CTRMO", writeCallback: (_, newValue) => month.BCDSet((byte)newValue), valueProviderCallback: _ => month.BCDGet())
                .WithReservedBits(13, 3)
                .WithValueField(16, 8, name: "CTRYR", writeCallback: (_, newValue) => yearsOfCentury.BCDSet((byte)newValue), valueProviderCallback: _ => yearsOfCentury.BCDGet())
                .WithValueField(24, 3, name: "CTRWKDY", writeCallback: (_, newValue) => weekday.BCDSet((byte)newValue), valueProviderCallback: _ => weekday.BCDGet())
                .WithReservedBits(27, 1)
                // Documentation on Century Bit set: "Century is 1900s/2100s". In this model the century bit is set for years 1970-1999 and 2100-2169.
                .WithFlag(28, out centuryBit, name: "CB")
                .WithFlag(29, out centuryChangeEnabled, name: "CEB")
                .WithReservedBits(30, 1)
                .WithFlag(31, out readError, name: "CTERR")
                .WithWriteCallback((_, __) =>
                {
                    readError.Value = valueReadWithCountersLower == internalTimer.Value;
                    if(!writeBusy)
                    {
                        this.Log(LogLevel.Warning, "The Counters Upper register written without prior write to the Counters Lower register!", Registers.CountersLower);
                    }
                    writeBusy = false;

                    var year = CalculateYear(centuryBit.Value, yearsOfCentury);
                    var newDateTime = new DateTime(year, month, day, hours, minutes, seconds, secondHundredths * 10);

                    // Check if weekday matches (Sunday is 0 for both).
                    if(weekday != (int)newDateTime.DayOfWeek)
                    {
                        this.Log(LogLevel.Warning, "Weekday given doesn't match the given date! New date's day of week: {0} ({1})", newDateTime.DayOfWeek, (int)newDateTime.DayOfWeek);
                    }

                    SetDateTimeInternal(newDateTime);
                })
                ;

            Registers.InterruptClear.Define(this)
                .WithFlag(0, FieldMode.Write, name: "ALM", writeCallback: (_, newValue) => { if(newValue) InterruptStatus = false; })
                .WithReservedBits(1, 31)
                ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptEnable, name: "ALM", changeCallback: (_, __) => UpdateInterrupt())
                .WithReservedBits(1, 31)
                ;

            Registers.InterruptSet.Define(this)
                .WithFlag(0, FieldMode.Write, name: "ALM", writeCallback: (_, newValue) => { if(newValue) InterruptStatus = true; })
                .WithReservedBits(1, 31)
                ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "ALM", valueProviderCallback: _ => InterruptStatus)
                .WithReservedBits(1, 31)
                ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "WRITEBUSY", valueProviderCallback: _ => writeBusy)
                .WithReservedBits(1, 31)
                ;
        }

        private void InitializeBCDValueFields()
        {
            alarmDay = new BCDValueField(this, "days", 0x31, zeroAllowed: false);
            alarmHours = new BCDValueField(this, "hours", 0x23);
            alarmMinutes = new BCDValueField(this, "minutes", 0x59);
            alarmMonth = new BCDValueField(this, "months", 0x12, zeroAllowed: false);
            alarmSeconds = new BCDValueField(this, "seconds", 0x59);
            alarmSecondHundredths = new BCDValueField(this, "hundredths of a second");
            alarmWeekday = new BCDValueField(this, "weekdays", 0x6);
            day = new BCDValueField(this, "days", 0x31, zeroAllowed: false);
            hours = new BCDValueField(this, "hours", 0x23);
            minutes = new BCDValueField(this, "minutes", 0x59);
            month = new BCDValueField(this, "months", 0x12, zeroAllowed: false);
            secondHundredths = new BCDValueField(this, "hundredths of a second");
            seconds = new BCDValueField(this, "seconds", 0x59);
            weekday = new BCDValueField(this, "weekdays", 0x6);
            yearsOfCentury = new BCDValueField(this, "years of a century");
        }

        private void SetDateTimeInternal(DateTime dateTime, bool hushPrecisionWarning = false)
        {
            internalTimer.SetDateTime(dateTime, hushPrecisionWarning);

            // All the other registers will be updated before reading any of the Counters registers
            // but the century bit might not get updated if the centuryChangeEnabled is false.
            UpdateCenturyBit(dateTime.Year);

            UpdateAlarm();
        }

        private void UpdateAlarm()
        {
            internalTimer.UpdateAlarm(alarmRepeatInterval.Value, alarmMonth, alarmWeekday, alarmDay, alarmHours, alarmMinutes, alarmSeconds, alarmSecondHundredths * 10);
        }

        private void UpdateCenturyBit(int year)
        {
            centuryBit.Value = year < 2000 || year >= 2100;
        }

        private void UpdateCounterFields()
        {
            if(lastUpdateTimerValue == internalTimer.Value)
            {
                return;
            }

            var dateTime = internalTimer.GetCurrentDateTime();

            secondHundredths.SetFromInteger((int)Math.Round(dateTime.Millisecond / 10.0, 0));
            seconds.SetFromInteger(dateTime.Second);
            minutes.SetFromInteger(dateTime.Minute);
            hours.SetFromInteger(dateTime.Hour);
            day.SetFromInteger(dateTime.Day);
            month.SetFromInteger(dateTime.Month);
            yearsOfCentury.SetFromInteger(dateTime.Year % 100);
            weekday.SetFromInteger((int)dateTime.DayOfWeek);
            if(centuryChangeEnabled.Value)
            {
                UpdateCenturyBit(dateTime.Year);
            }

            lastUpdateTimerValue = internalTimer.Value;
        }

        private void UpdateInterrupt()
        {
            var newIrqState = interruptEnable.Value && interruptStatus;
            if(newIrqState != IRQ.IsSet)
            {
                this.Log(LogLevel.Debug, "IRQ {0}", newIrqState ? "set" : "reset");
                IRQ.Set(newIrqState);
            }
        }

        private bool InterruptStatus
        {
            get => interruptStatus;
            set
            {
                interruptStatus = value;
                UpdateInterrupt();
            }
        }

        private readonly RTCTimer internalTimer;

        private bool interruptStatus;
        private ulong lastUpdateTimerValue;
        private bool writeBusy;
        private ulong valueReadWithCountersLower;

        private BCDValueField alarmDay;
        private BCDValueField alarmHours;
        private BCDValueField alarmMinutes;
        private BCDValueField alarmMonth;
        private BCDValueField alarmSeconds;
        private BCDValueField alarmSecondHundredths;
        private BCDValueField alarmWeekday;
        private BCDValueField day;
        private BCDValueField hours;
        private BCDValueField minutes;
        private BCDValueField month;
        private BCDValueField secondHundredths;  // 0.01s
        private BCDValueField seconds;
        private BCDValueField weekday;
        private BCDValueField yearsOfCentury;

        private IEnumRegisterField<AlarmRepeatIntervals> alarmRepeatInterval;
        private IFlagRegisterField centuryBit;
        private IFlagRegisterField centuryChangeEnabled;
        private IFlagRegisterField counterWritesEnabled;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField readError;

        private class BCDValueField
        {
            static public implicit operator int(BCDValueField field)
            {
                return field.GetInteger();
            }

            public BCDValueField(IPeripheral owner, string fieldTypeName, byte maxValueBCD = 0x99, bool zeroAllowed = true)
            {
                this.fieldTypeName = fieldTypeName;
                this.maxValueBCD = maxValueBCD;
                this.owner = owner;
                this.zeroAllowed = zeroAllowed;
            }

            public byte BCDGet()
            {
                return value;
            }

            public void BCDSet(byte bcdValue)
            {
                if(bcdValue > maxValueBCD || (!zeroAllowed && bcdValue == 0x0))
                {
                    owner.Log(LogLevel.Warning, "Invalid value for {0}: {1:X}", fieldTypeName, bcdValue);
                    return;
                }

                value = bcdValue;
            }

            public int GetInteger()
            {
                return BCDHelper.DecodeFromBCD(value);
            }

            public void SetFromInteger(int value)
            {
                BCDSet(BCDHelper.EncodeToBCD((byte)value));
            }

            public override string ToString()
            {
                // BCD value printed in hex is always equal to its decimal value.
                return $"{value:X}";
            }

            private readonly string fieldTypeName;
            private readonly byte maxValueBCD;
            private readonly IPeripheral owner;
            private readonly bool zeroAllowed;

            private byte value;
        }

        private class RTCTimer : LimitTimer
        {
            public RTCTimer(IMachine machine, IBusPeripheral owner, DateTime baseDateTime, Action alarmAction) : base(machine.ClockSource, Frequency, owner, "RTC",
                limit: ulong.MaxValue, direction: Direction.Ascending, enabled: true, workMode: WorkMode.Periodic, eventEnabled: true)
            {
                this.alarmAction = alarmAction;
                baseDateTimeTicks = baseDateTime.Ticks;
                this.owner = owner;
                systemBus = machine.GetSystemBus(owner);

                // It can be reached only after setting up the alarm.
                LimitReached += AlarmHandler;
            }

            public DateTime GetCurrentDateTime()
            {
                return ValueToDateTime(Value);
            }

            public DateTime GetNextAlarmDateTime()
            {
                return ValueToDateTime(nextAlarmValue);
            }

            public bool IsAlarmSet()
            {
                return Limit != ulong.MaxValue;
            }

            public override void Reset()
            {
                ResetAlarm();
                base.Reset();
            }

            public void SetDateTime(DateTime newDateTime, bool hushPrecisionWarning = false)
            {
                ResetAlarm();
                Value = ValueFromDateTime(newDateTime, hushPrecisionWarning);

                // The format is the same as 'o' but with only first two millisecond digits.
                // Further digits, if nonzero, were ignored setting RTC's value so let's not print them.
                owner.Log(LogLevel.Info, "New date time set: {0:yyyy-MM-ddTHH:mm:ss.ffK}", newDateTime);
            }

            public void UpdateAlarm(AlarmRepeatIntervals interval, int month, int weekday, int day, int hour, int minute, int second, int millisecond)
            {
                if(interval == AlarmRepeatIntervals.Month || interval == AlarmRepeatIntervals.Year)
                {
                    if(day == 0)
                    {
                        owner.Log(LogLevel.Warning, "Day cannot be zero for the {0} alarm repeat interval! Using 1st as an alarm day.", interval);
                        day = 1;
                    }
                }

                if(interval == AlarmRepeatIntervals.Year)
                {
                    if(month == 0)
                    {
                        owner.Log(LogLevel.Warning, "Month cannot be zero for the {0} alarm repeat interval! Using January as an alarm month.", interval);
                        month = 1;
                    }
                }

                var currentDateTime = GetCurrentDateTime();
                DateTime firstAlarm, intervalDateTime;
                switch(interval)
                {
                    case AlarmRepeatIntervals.Second:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day,
                            currentDateTime.Hour, currentDateTime.Minute, currentDateTime.Second, millisecond);
                        intervalDateTime = new DateTime().AddSeconds(1);
                        break;
                    case AlarmRepeatIntervals.Minute:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day,
                            currentDateTime.Hour, currentDateTime.Minute, second, millisecond);
                        intervalDateTime = new DateTime().AddMinutes(1);
                        break;
                    case AlarmRepeatIntervals.Hour:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day,
                            currentDateTime.Hour, minute, second, millisecond);
                        intervalDateTime = new DateTime().AddHours(1);
                        break;
                    case AlarmRepeatIntervals.Day:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, hour,
                            minute, second, millisecond);
                        intervalDateTime = new DateTime().AddDays(1);
                        break;
                    case AlarmRepeatIntervals.Week:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, hour,
                            minute, second, millisecond);
                        // This can take us "back in time" but we're always adjusting such a 'firstAlarm' nevertheless.
                        var daysToTheNearestAlarmWeekday = weekday - (int)firstAlarm.DayOfWeek;
                        firstAlarm = firstAlarm.AddDays(daysToTheNearestAlarmWeekday);
                        intervalDateTime = new DateTime().AddDays(7);
                        break;
                    case AlarmRepeatIntervals.Month:
                        firstAlarm = new DateTime(currentDateTime.Year, currentDateTime.Month, day, hour, minute, second,
                            millisecond);
                        intervalDateTime = new DateTime().AddMonths(1);
                        break;
                    case AlarmRepeatIntervals.Year:
                        firstAlarm = new DateTime(currentDateTime.Year, month, day, hour, minute, second, millisecond);
                        intervalDateTime = new DateTime().AddYears(1);
                        break;
                    case AlarmRepeatIntervals.Disabled:
                        ResetAlarm();
                        return;
                    default:
                        throw new ArgumentException("Something's very wrong; this should never happen.");
                }

                // Before this adjustment 'firstAlarm' can be in the past.
                if(firstAlarm < currentDateTime)
                {
                    firstAlarm = firstAlarm.AddTicks(intervalDateTime.Ticks);
                }

                alarmIntervalTicks = (ulong)intervalDateTime.Ticks / TimerTickToDateTimeTicks;
                nextAlarmValue = ValueFromDateTime(firstAlarm);
                Limit = nextAlarmValue;
                owner.Log(LogLevel.Debug, "First alarm set to: {0:o}, alarm repeat interval: {1}", firstAlarm, interval);
            }

            public new ulong Value
            {
                get
                {
                    if(systemBus.TryGetCurrentCPU(out var cpu))
                    {
                        // being here means we are on the CPU thread
                        cpu.SyncTime();
                    }
                    else
                    {
                        owner.Log(LogLevel.Noisy, "Couldn't synchronize time: returned value might lack precision");
                    }
                    return base.Value;
                }

                set => base.Value = value;
            }

            static private readonly ulong TimerTickToDateTimeTicks = (ulong)new DateTime().AddSeconds(1.0 / Frequency).Ticks;

            private void AlarmHandler()
            {
                alarmAction();

                // Value is automatically reset when the limit is reached.
                Value = nextAlarmValue;

                nextAlarmValue += alarmIntervalTicks;
                Limit = nextAlarmValue;

                owner.Log(LogLevel.Debug, "Alarm occurred at: {0:o}; next alarm: {1:o}", GetCurrentDateTime(), GetNextAlarmDateTime());
            }

            private void ResetAlarm()
            {
                Limit = ulong.MaxValue;
                alarmIntervalTicks = 0;
                nextAlarmValue = 0;
            }

            private ulong ValueFromDateTime(DateTime dateTime, bool hushPrecisionWarning = false)
            {
                var newDateTimeTicks = (ulong)(dateTime.Ticks - baseDateTimeTicks);
                if(!hushPrecisionWarning && (newDateTimeTicks % TimerTickToDateTimeTicks != 0))
                {
                    owner.Log(LogLevel.Warning, "Requested time for RTC is more precise than it supports (0.01s): {0:o}", dateTime);
                }
                return newDateTimeTicks / TimerTickToDateTimeTicks;
            }

            private DateTime ValueToDateTime(ulong value)
            {
                var dateTimeTicksPassed = value * TimerTickToDateTimeTicks;
                return new DateTime(baseDateTimeTicks + (long)dateTimeTicksPassed);
            }

            private readonly Action alarmAction;
            private readonly long baseDateTimeTicks;
            private readonly IPeripheral owner;
            private readonly IBusController systemBus;

            private ulong alarmIntervalTicks;
            private ulong nextAlarmValue;

            private new const long Frequency = 100;
        }

        private enum AlarmRepeatIntervals
        {
            Disabled,
            Year,
            Month,
            Week,
            Day,
            Hour,
            Minute,
            // Docs: "Interrupt every second/10th/100th".
            Second,
        }

        private enum Registers : long
        {
            Control = 0x0,
            Status = 0x4,
            CountersLower = 0x20,
            CountersUpper = 0x24,
            AlarmsLower = 0x30,
            AlarmsUpper = 0x34,
            InterruptEnable = 0x200,
            InterruptStatus = 0x204,
            InterruptClear = 0x208,
            InterruptSet = 0x20C,
        }
    }
}
