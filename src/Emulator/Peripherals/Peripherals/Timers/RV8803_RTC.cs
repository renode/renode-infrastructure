//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Globalization;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RV8803_RTC : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public RV8803_RTC(IMachine machine)
        {
            IRQ = new GPIO();

            periodicTimer = new LimitTimer(machine.ClockSource, DefaultPeriodicTimerFrequency, this, "periodicTimer", limit: DefaultPeriodicTimerLimit, eventEnabled: true);
            periodicTimer.LimitReached += () =>
            {
                periodicTimerFlag.Value = true;
                if(periodicTimerInterruptEnable.Value)
                {
                    this.Log(LogLevel.Noisy, "IRQ blink");
                    // yes, it blinks
                    IRQ.Blink();
                }
            };

            realTimeCounter = new RTCTimer(machine, this);

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public string CurrentTime
        {
            get => realTimeCounter.CurrentTime.ToString();
            set
            {
                const string format = "dd-MM-yyyy HH:mm:ss";
                if(!DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    throw new RecoverableException($"Couldn't parse time: {value}. Provide it in the {format} format (e.g., 31-12-2022 13:22:31)");
                }
                realTimeCounter.CurrentTime = DateTimeWithCustomWeekday.FromDateTime(dt);
                this.Log(LogLevel.Debug, "RTC time set to {0}", dt);
            }
        }

        public GPIO IRQ { get; }

        public void Reset()
        {
            realTimeCounter.Reset();
            periodicTimer.Reset();

            RegistersCollection.Reset();

            currentState = State.WaitingForRegister;
            register = default(Registers);

            IRQ.Unset();
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Debug, "Written {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            foreach(var d in data)
            {
                HandleIncomingByte(d);
            }
        }

        private void HandleIncomingByte(byte data)
        {
            switch(currentState)
            {
                case State.WaitingForRegister:
                    register = (Registers)data;
                    this.Log(LogLevel.Debug, "Register set to {0}", register);

                    currentState = State.HandlingData;
                    break;

                case State.HandlingData:
                    HandleWrite(data);
                    break;
            }
        }

        private void HandleWrite(byte data)
        {
            this.Log(LogLevel.Debug, "Handling write of 0x{0:X} to {1}", data, register);
            RegistersCollection.Write((long)register, data);
            register++;
        }

        public byte[] Read(int count = 1)
        {
            var result = new byte[count];
            for(var i = 0; i < count; i++)
            {
                result[i] = HandleRead();
            }
            return result;
        }

        private byte HandleRead()
        {
            var result = RegistersCollection.Read((long)register);
            this.Log(LogLevel.Debug, "Read value 0x{0:X} from {1}", result, register);
            register++;
            return result;
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Debug, "Finished the transmission");
            currentState = State.WaitingForRegister;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.TimerCounter0.Define(this)
                .WithValueField(0, 8, name: "Timer Counter 0",
                    // When read, only the preset value is returned and not the actual value.
                    writeCallback: (_, newValue) =>
                    {
                        var timerValue = periodicTimer.Value;
                        timerValue &= 0xF00;
                        timerValue |= newValue;

                        periodicTimer.Limit = DefaultPeriodicTimerLimit;
                        periodicTimer.Value = timerValue;
                        periodicTimer.Limit = timerValue;
                    });

            Registers.TimerCounter1.Define(this)
                .WithValueField(0, 4, name: "Timer Counter 1",
                    // When read, only the preset value is returned and not the actual value.
                    writeCallback: (_, newValue) =>
                    {
                        var timerValue = periodicTimer.Value;
                        timerValue &= 0x0FF;
                        timerValue |= (newValue << 8);

                        periodicTimer.Limit = DefaultPeriodicTimerLimit;
                        periodicTimer.Value = timerValue;
                        periodicTimer.Limit = timerValue;
                    })
                .WithFlag(4, name: "GP2 - General Purpose Bit")
                .WithFlag(5, name: "GP3 - General Purpose Bit")
                .WithFlag(6, name: "GP4 - General Purpose Bit")
                .WithFlag(7, name: "GP5 - General Purpose Bit");

            var extensionRegister = new ByteRegister(this)
                .WithTag("TD - Timer Clock Frequency", 0, 2)
                .WithTag("FD - CLKOUT Frequency", 2, 2)
                .WithFlag(4, out periodicCountdownTimerEnable, name: "TE - Periodic Countdown Timer Enable",
                    writeCallback: (_, val) =>
                    {
                        UpdateTimersEnable();
                    }
                )
                .WithTag("USEL - Update Interrupt Select", 5, 1)
                .WithTag("WADA - Weekday Alarm / Date Alarm Select", 6, 1)
                .WithTag("TEST", 7, 1);

            RegistersCollection.AddRegister((long)Registers.ExtensionRegister, extensionRegister);
            RegistersCollection.AddRegister((long)Registers.ExtensionRegister_Extended1, extensionRegister);

            var flagRegister = new ByteRegister(this)
                .WithTag("V1F - Voltage Low Flag 1", 0, 1)
                .WithTag("V2F - Voltage Low Flag 2", 1, 1)
                .WithTag("EVF - External Event Flag", 2, 1)
                .WithTag("AF - Alarm Flag", 3, 1)
                .WithFlag(4, out periodicTimerFlag, name: "TF - Periodic Countdown Timer Flag")
                .WithTag("UF - Periodic Time Update Flag", 5, 1)
                .WithReservedBits(6, 2);

            RegistersCollection.AddRegister((long)Registers.FlagRegister, flagRegister);
            RegistersCollection.AddRegister((long)Registers.FlagRegister_Extended1, flagRegister);

            var controlRegister = new ByteRegister(this)
                .WithFlag(0, out reset, name: "RESET",
                    writeCallback: (_, val) =>
                    {
                        UpdateTimersEnable();
                    })
                .WithReservedBits(1, 1)
                .WithTag("EIE - External Event Interrupt Enable", 2, 1)
                .WithTag("AIE - Alarm Interrupt Enable", 3, 1)
                .WithFlag(4, out periodicTimerInterruptEnable, name :"TIE - Periodic Countdown Timer Interrupt Enable")
                .WithTag("UIE - Periodic Time Update Interrupt Enable", 5, 1)
                .WithReservedBits(6, 2)
            ;

            RegistersCollection.AddRegister((long)Registers.ControlRegister, controlRegister);
            RegistersCollection.AddRegister((long)Registers.ControlRegister_Extended1, controlRegister);

            Registers.RAM.Define(this)
                .WithValueField(0, 8, name: "RAM");

            var secondsRegister = new ByteRegister(this)
                .WithValueField(0, 7, name: "Seconds",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Second),
                    writeCallback: (_, value) => realTimeCounter.Second = BCDHelper.DecodeFromBCD((byte)value))
                .WithReservedBits(7, 1)
            ;

            RegistersCollection.AddRegister((long)Registers.Seconds, secondsRegister);
            RegistersCollection.AddRegister((long)Registers.Seconds_Extended1, secondsRegister);

            var minutesRegister = new ByteRegister(this)
                .WithValueField(0, 7, name: "Minutes",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Minute),
                    writeCallback: (_, value) => realTimeCounter.Minute = BCDHelper.DecodeFromBCD((byte)value))
                .WithReservedBits(7, 1)
            ;

            RegistersCollection.AddRegister((long)Registers.Minutes, minutesRegister);
            RegistersCollection.AddRegister((long)Registers.Minutes_Extended1, minutesRegister);

            var hoursRegister = new ByteRegister(this)
                .WithValueField(0, 6, name: "Hours",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Hour),
                    writeCallback: (_, value) => realTimeCounter.Hour = BCDHelper.DecodeFromBCD((byte)value))
                .WithReservedBits(6, 2)
            ;

            RegistersCollection.AddRegister((long)Registers.Hours, hoursRegister);
            RegistersCollection.AddRegister((long)Registers.Hours_Extended1, hoursRegister);

            var weekdayRegister = new ByteRegister(this)
                .WithValueField(0, 7, name: "Weekday",
                    valueProviderCallback: _ => (uint)realTimeCounter.Weekday,
                    writeCallback: (_, value) => realTimeCounter.Weekday = (int)value)
                .WithReservedBits(7, 1)
            ;

            RegistersCollection.AddRegister((long)Registers.Weekday, weekdayRegister);
            RegistersCollection.AddRegister((long)Registers.Weekday_Extended1, weekdayRegister);

            var dateRegister = new ByteRegister(this)
                .WithValueField(0, 6, name: "Date",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Day),
                    writeCallback: (_, value) => realTimeCounter.Day = BCDHelper.DecodeFromBCD((byte)value))
                .WithReservedBits(6, 2)
            ;

            RegistersCollection.AddRegister((long)Registers.Date, dateRegister);
            RegistersCollection.AddRegister((long)Registers.Date_Extended1, dateRegister);

            var monthRegister = new ByteRegister(this)
                .WithValueField(0, 5, name: "Month",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Month),
                    writeCallback: (_, value) => realTimeCounter.Month = BCDHelper.DecodeFromBCD((byte)value))
                .WithReservedBits(5, 3)
            ;

            RegistersCollection.AddRegister((long)Registers.Month, monthRegister);
            RegistersCollection.AddRegister((long)Registers.Month_Extended1, monthRegister);

            var yearRegister = new ByteRegister(this)
                .WithValueField(0, 8, name: "Year",
                    valueProviderCallback: _ => BCDHelper.EncodeToBCD((byte)realTimeCounter.Year),
                    writeCallback: (_, value) => realTimeCounter.Year = BCDHelper.DecodeFromBCD((byte)value))
            ;

            RegistersCollection.AddRegister((long)Registers.Year, yearRegister);
            RegistersCollection.AddRegister((long)Registers.Year_Extended1, yearRegister);
        }

        private void UpdateTimersEnable()
        {
            periodicTimer.Enabled = !reset.Value && periodicCountdownTimerEnable.Value;
            realTimeCounter.Enabled = !reset.Value;
        }

        private IFlagRegisterField periodicCountdownTimerEnable;
        private IFlagRegisterField reset;
        private IFlagRegisterField periodicTimerFlag;
        private IFlagRegisterField periodicTimerInterruptEnable;

        private State currentState;
        private Registers register;

        private readonly LimitTimer periodicTimer;
        private readonly RTCTimer realTimeCounter;

        private const int DefaultPeriodicTimerFrequency = 4096;
        private const int DefaultPeriodicTimerLimit = 4096;

        private enum Registers
        {
            Seconds = 0x00,
            Minutes = 0x01,
            Hours = 0x02,
            Weekday = 0x03,
            Date = 0x04,
            Month = 0x05,
            Year = 0x06,
            RAM = 0x07,
            MinutesAlarm = 0x08,
            HoursAlarm = 0x09,
            WeekdayAlarm_DateAlarm = 0x0A,
            TimerCounter0 = 0x0B,
            TimerCounter1 = 0x0C,
            ExtensionRegister = 0x0D,
            FlagRegister = 0x0E,
            ControlRegister = 0x0F,

            Seconds100th_Extended1 = 0x10,
            Seconds_Extended1 = 0x11,
            Minutes_Extended1 = 0x12,
            Hours_Extended1 = 0x13,
            Weekday_Extended1 = 0x14,
            Date_Extended1 = 0x15,
            Month_Extended1 = 0x16,
            Year_Extended1 = 0x17,
            MinutesAlarm_Extended1 = 0x18,
            HoursAlarm_Extended1 = 0x19,
            WeekdayAlarm_DateAlarm_Extended1 = 0x1A,
            TimerCounter0_Extended1 = 0x1B,
            TimerCounter1_Extended1 = 0x1C,
            ExtensionRegister_Extended1 = 0x1D,
            FlagRegister_Extended1 = 0x1E,
            ControlRegister_Extended1 = 0x1F,

            Seconds100thCP_Extended2 = 0x20,
            SecondsCP_Extended2 = 0x21,
            Offset_Extended2 = 0x2C,
            EventControl_Extended2 = 0x2F,
        }

        private enum State
        {
            WaitingForRegister,
            HandlingData,
        }

        private enum Operation
        {
            Write = 0,
            Read = 1
        }

        private class RTCTimer
        {
            public RTCTimer(IMachine machine, IPeripheral parent)
            {
                this.parent = parent;

                innerTimer = new LimitTimer(machine.ClockSource, RTCFrequency, parent, "RTC timer", limit: RTCLimit);
                innerTimer.LimitReached += () =>
                {
                    // this is called every second
                    currentTime.AddSeconds(1);
                };

                Reset();
            }

            public void Reset()
            {
                currentTime = new DateTimeWithCustomWeekday();
                innerTimer.Reset();
            }

            public DateTimeWithCustomWeekday CurrentTime
            {
                get => currentTime;
                set
                {
                    currentTime = value;
                }
            }

            public bool Enabled
            {
                get => innerTimer.Enabled;
                set
                {
                    innerTimer.Enabled = value;
                }
            }

            public int Second
            {
                get => currentTime.Second;
                set
                {
                    try
                    {
                        currentTime.Second = value;
                    }
                    catch(ArgumentException e)
                    {
                        parent.Log(LogLevel.Warning, e.Message);
                    }
                }
            }

            public int Minute
            {
                get => currentTime.Minute;
                set
                {
                    try
                    {
                        currentTime.Minute = value;
                    }
                    catch(ArgumentException e)
                    {
                        parent.Log(LogLevel.Warning, e.Message);
                    }
                }
            }

            public int Hour
            {
                get => currentTime.Hour;
                set
                {
                    try
                    {
                        currentTime.Hour = value;
                    }
                    catch(ArgumentException e)
                    {
                        parent.Log(LogLevel.Warning, e.Message);
                    }
                }
            }

            public int Day
            {
                get => currentTime.Day;
                set
                {
                    try
                    {
                        currentTime.Day = value;
                    }
                    catch(ArgumentException e)
                    {
                        parent.Log(LogLevel.Warning, e.Message);
                    }
                }
            }

            public int Month
            {
                get => currentTime.Month;
                set
                {
                    try
                    {
                        currentTime.Month = value;
                    }
                    catch(ArgumentException e)
                    {
                        parent.Log(LogLevel.Warning, e.Message);
                    }
                }
            }

            public int Year
            {
                get => currentTime.Year % 100;
                set
                {
                    if(value < 0 || value > 99)
                    {
                        parent.Log(LogLevel.Warning, "Year value out of range: {0}", value);
                        return;
                    }

                    currentTime.Year = 2000 + value;
                }
            }

            // Encoded as a single bit at positions 0, 1, 2, 3, 4, 5 or 6
            public int Weekday
            {
                get
                {
                    var v = (int)currentTime.Weekday;
                    if(v < 1)
                    {
                        v += 7;
                    }
                    return 1 << (v - 1);
                }
                set
                {
                    if(value <= 0)
                    {
                        parent.Log(LogLevel.Warning, "Weekday value (0x{0:X}) out of range, expected exactly one bit to be set", value);
                        return;
                    }

                    var originalValue = value;
                    var newWeekDay = 0;
                    while(!BitHelper.IsBitSet((uint)value, 0))
                    {
                        newWeekDay++;
                        value >>= 1;
                    }

                    if(value != 0 || newWeekDay > 6)
                    {
                        parent.Log(LogLevel.Warning, "Weekday value (0x{0:X}) out of range, expected exactly one bit to be set", originalValue);
                        return;
                    }

                    currentTime.Weekday = (DayOfWeek)newWeekDay;
                }
            }

            private DateTimeWithCustomWeekday currentTime;

            private readonly IPeripheral parent;
            private readonly LimitTimer innerTimer;

            private const int RTCFrequency = 1;
            private const int RTCLimit = 1;
        }
    }
}
