//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
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

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x20;

        protected override void OnLimitReached()
        {
            base.OnLimitReached();

            var dateTime = new DateTime(
                (int)clock.Year,
                (int)clock.Month,
                (int)clock.Day,
                (int)clock.Hours,
                (int)clock.Minutes,
                (int)clock.Seconds
            ).AddSeconds(1);

            clock.Year = (uint)dateTime.Year;
            clock.Month = (uint)dateTime.Month;
            clock.Day = (uint)dateTime.Day;
            clock.Hours = (uint)dateTime.Hour;
            clock.Minutes = (uint)dateTime.Minute;
            clock.Seconds = (uint)dateTime.Second;

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
                    valueProviderCallback: _ => clock.Seconds,
                    writeCallback: (_, value) => clock.Seconds = (uint)value)
                .WithValueField(6, 2, name: "MINUTES[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Minutes, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Minutes, (uint)value, 0, 2))
            ;

            Registers.Clock1.Define(this)
                .WithValueField(0, 4, name: "MINUTES[5:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Minutes, 2, 4),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Minutes, (uint)value, 2, 4))
                .WithValueField(4, 4, name: "HOURS[3:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Hours, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Hours, (uint)value, 0, 4))
            ;

            Registers.Clock2.Define(this)
                .WithValueField(0, 1, name: "HOURS[4]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Hours, 4, 1),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Hours, (uint)value, 4, 1))
                .WithValueField(1, 5, name: "DAY[4:0]",
                    valueProviderCallback: _ => clock.Day,
                    writeCallback: (_, value) => clock.Day = (uint)value)
                .WithValueField(6, 2, name: "MONTH[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Month, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Month, (uint)value, 0, 2))
            ;

            Registers.Clock3.Define(this)
                .WithValueField(0, 2, name: "MONTH[3:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(clock.Month, 2, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref clock.Month, (uint)value, 2, 2))
                .WithValueField(2, 6, name: "YEAR[3:2]",
                    valueProviderCallback: _ => clock.Year,
                    writeCallback: (_, value) => clock.Year = (uint)value)
            ;

            Registers.Alarm0.Define(this)
                .WithValueField(0, 6, name: "SECONDS[5:0]",
                    valueProviderCallback: _ => alarm.Seconds,
                    writeCallback: (_, value) => alarm.Seconds = (uint)value)
                .WithValueField(6, 2, name: "MINUTES[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Minutes, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Minutes, (uint)value, 0, 2))
            ;

            Registers.Alarm1.Define(this)
                .WithValueField(0, 4, name: "MINUTES[5:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Minutes, 2, 4),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Minutes, (uint)value, 2, 4))
                .WithValueField(4, 4, name: "HOURS[3:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Hours, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Hours, (uint)value, 0, 4))
            ;

            Registers.Alarm2.Define(this)
                .WithValueField(0, 1, name: "HOURS[4]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Hours, 4, 1),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Hours, (uint)value, 4, 1))
                .WithValueField(1, 5, name: "DAY[4:0]",
                    valueProviderCallback: _ => alarm.Day,
                    writeCallback: (_, value) => alarm.Day = (uint)value)
                .WithValueField(6, 2, name: "MONTH[1:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Month, 0, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Month, (uint)value, 0, 2))
            ;

            Registers.Alarm3.Define(this)
                .WithValueField(0, 2, name: "MONTH[3:2]",
                    valueProviderCallback: _ => BitHelper.GetValue(alarm.Month, 2, 2),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref alarm.Month, (uint)value, 2, 2))
                .WithValueField(2, 6, name: "YEAR[3:2]",
                    valueProviderCallback: _ => alarm.Year,
                    writeCallback: (_, value) => alarm.Year = (uint)value)
            ;
        }

        private bool interruptAlarmEnabled;

        private IFlagRegisterField interruptAlarmPending;

        private Time clock = new Time();
        private Time alarm = new Time();

        private struct Time
        {
            public uint Seconds;
            public uint Minutes;
            public uint Hours;
            public uint Day;
            public uint Month;
            public uint Year;
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