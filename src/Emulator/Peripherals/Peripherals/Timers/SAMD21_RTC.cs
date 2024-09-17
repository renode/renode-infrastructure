//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SAMD21_RTC : LimitTimer, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public SAMD21_RTC(IMachine machine) : base(machine.ClockSource, 1, eventEnabled: true)
        {
            RegistersCollection = new ByteRegisterCollection(this);

            DefineRegisters();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x20;

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        protected override void OnLimitReached()
        {
            base.OnLimitReached();

            var dateTime = new DateTime(
                (int)clock.year,
                (int)clock.month,
                (int)clock.day,
                (int)clock.hours,
                (int)clock.minutes,
                (int)clock.seconds
            ).AddSeconds(1);

            clock.year = (uint)dateTime.Year;
            clock.month = (uint)dateTime.Month;
            clock.day = (uint)dateTime.Day;
            clock.hours = (uint)dateTime.Hour;
            clock.minutes = (uint)dateTime.Minute;
            clock.seconds = (uint)dateTime.Second;

            if(clock.Equals(alarm))
            {
                interruptAlarmPending.Value = true;
                UpdateInterrupts();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interruptAlarmEnabled && interruptAlarmPending.Value);
            this.Log(LogLevel.Debug, "Changed IRQ to {0}", IRQ.IsSet);
        }

        private void DefineRegisters()
        {
            Registers.Control0.Define(this)
                .WithTaggedFlag("SWRST", 0)
                .WithFlag(1, name: "ENABLE",
                    valueProviderCallback: _ => Enabled,
                    changeCallback: (_, value) => Enabled = value)
                .WithEnumField<ByteRegister, RTCMode>(2, 2, name: "MODE",
                    valueProviderCallback: _ => RTCMode.RTC,
                    changeCallback: (_, value) =>
                    {
                        if(value != RTCMode.RTC)
                        {
                            this.Log(LogLevel.Warning, "Tried to change mode to {0}, but only RTC mode is supported; ignoring", value);
                            return;
                        }
                    })
                .WithReservedBits(4, 2)
                .WithTaggedFlag("CLKREP", 6)
                .WithTaggedFlag("MATCHCLR", 7)
            ;

            Registers.Control1.Define(this)
                .WithTag("PRESCALER", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.InterruptClear.Define(this)
                .WithFlag(0, name: "ALARM0",
                    valueProviderCallback: _ => interruptAlarmEnabled,
                    writeCallback: (_, value) => { if(value) interruptAlarmEnabled = false; })
                .WithReservedBits(1, 5)
                .WithTaggedFlag("SYNCRDY", 6)
                .WithTaggedFlag("OVF", 7)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptSet.Define(this)
                .WithFlag(0, name: "ALARM0",
                    valueProviderCallback: _ => interruptAlarmEnabled,
                    writeCallback: (_, value) => { if(value) interruptAlarmEnabled = true; })
                .WithReservedBits(1, 5)
                .WithTaggedFlag("SYNCRDY", 6)
                .WithTaggedFlag("OVF", 7)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptFlags.Define(this)
                .WithFlag(0, out interruptAlarmPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "ALARM0")
                .WithReservedBits(1, 5)
                .WithTaggedFlag("SYNCRDY", 6)
                .WithTaggedFlag("OVF", 7)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Clock0.Define(this)
                .WithValueField(0, 6, name: "SECONDS[5:0]",
                    valueProviderCallback: _ => clock.seconds,
                    writeCallback: (_, value) => clock.seconds = (uint)value)
                .WithValueField(6, 2, name: "MINUTES[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.minutes, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.minutes, (uint)value, 0, 2))
            ;

            Registers.Clock1.Define(this)
                .WithValueField(0, 4, name: "MINUTES[5:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.minutes, 2, 4),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.minutes, (uint)value, 2, 4))
                .WithValueField(4, 4, name: "HOURS[3:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.hours, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.hours, (uint)value, 0, 4))
            ;

            Registers.Clock2.Define(this)
                .WithValueField(0, 1, name: "HOURS[4]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.hours, 4, 1),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.hours, (uint)value, 4, 1))
                .WithValueField(1, 5, name: "DAY[4:0]",
                    valueProviderCallback: _ => clock.day,
                    writeCallback: (_, value) => clock.day = (uint)value)
                .WithValueField(6, 2, name: "MONTH[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.month, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.month, (uint)value, 0, 2))
            ;

            Registers.Clock3.Define(this)
                .WithValueField(0, 2, name: "MONTH[3:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.month, 2, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.month, (uint)value, 2, 2))
                .WithValueField(2, 6, name: "YEAR[3:2]",
                    valueProviderCallback: _ => clock.year,
                    writeCallback: (_, value) => clock.year = (uint)value)
            ;

            Registers.Alarm0.Define(this)
                .WithValueField(0, 6, name: "SECONDS[5:0]",
                    valueProviderCallback: _ => alarm.seconds,
                    writeCallback: (_, value) => alarm.seconds = (uint)value)
                .WithValueField(6, 2, name: "MINUTES[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.minutes, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.minutes, (uint)value, 0, 2))
            ;

            Registers.Alarm1.Define(this)
                .WithValueField(0, 4, name: "MINUTES[5:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.minutes, 2, 4),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.minutes, (uint)value, 2, 4))
                .WithValueField(4, 4, name: "HOURS[3:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.hours, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.hours, (uint)value, 0, 4))
            ;

            Registers.Alarm2.Define(this)
                .WithValueField(0, 1, name: "HOURS[4]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.hours, 4, 1),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.hours, (uint)value, 4, 1))
                .WithValueField(1, 5, name: "DAY[4:0]",
                    valueProviderCallback: _ => alarm.day,
                    writeCallback: (_, value) => alarm.day = (uint)value)
                .WithValueField(6, 2, name: "MONTH[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.month, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.month, (uint)value, 0, 2))
            ;

            Registers.Alarm3.Define(this)
                .WithValueField(0, 2, name: "MONTH[3:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.month, 2, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.month, (uint)value, 2, 2))
                .WithValueField(2, 6, name: "YEAR[3:2]",
                    valueProviderCallback: _ => alarm.year,
                    writeCallback: (_, value) => alarm.year = (uint)value)
            ;
        }

        private bool interruptAlarmEnabled;

        private IFlagRegisterField interruptAlarmPending;

        private Time clock = new Time();
        private Time alarm = new Time();

        private struct Time
        {
            public uint seconds;
            public uint minutes;
            public uint hours;
            public uint day;
            public uint month;
            public uint year;
        }

        private enum RTCMode
        {
            Timer32,
            Timer16,
            RTC,
        }

        private enum Registers
        {
            Control0 = 0x00,
            Control1 = 0x01,
            ReadRequest0 = 0x02,
            ReadRequest1 = 0x03,
            EventControl0 = 0x04,
            EventControl1 = 0x05,
            InterruptClear = 0x06,
            InterruptSet = 0x07,
            InterruptFlags = 0x08,

            Status = 0x0A,
            DebugControl = 0xB,
            FrequencyCorrection = 0x0C,

            Clock0 = 0x10,
            Clock1 = 0x11,
            Clock2 = 0x12,
            Clock3 = 0x13,

            Alarm0 = 0x18,
            Alarm1 = 0x19,
            Alarm2 = 0x1A,
            Alarm3 = 0x1B,

            Mask,
        }
    }
}
