//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class AndesATCRTC100 : BasicDoubleWordPeripheral, IKnownSize
    {
        public AndesATCRTC100(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            internalTimer = new LimitTimer(machine.ClockSource, 2, this, "RTC Tick", limit: 1, eventEnabled: true);
            internalTimer.LimitReached += RTCTick;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            internalTimer.Reset();
            halfSecondPassed = false;
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public TimeInterval TimePassed
        {
            get
            {
                var seconds = secondCounter.Value;
                seconds += minuteCounter.Value * 60;
                seconds += hourCounter.Value * 60 * 60;
                seconds += dayCounter.Value * 60 * 60 * 24;
                return TimeInterval.FromSeconds(seconds);
            }
        }

        private void UpdateInterrupt()
        {
            var irq = false;
            irq |= halfSecondIntEn.Value && halfSecondIntStatus.Value;
            irq |= secondIntEn.Value && secondIntStatus.Value;
            irq |= minuteIntEn.Value && minuteIntStatus.Value;
            irq |= hourIntEn.Value && hourIntStatus.Value;
            irq |= dayIntEn.Value && dayIntStatus.Value;
            irq |= alarmIntEn.Value && alarmIntStatus.Value;
            IRQ.Set(irq);
        }

        private void RTCTick()
        {
            halfSecondIntStatus.Value = true;
            if(!halfSecondPassed)
            {
                // Every other tick we only have set the half second interrupt
                halfSecondPassed = true;
                UpdateInterrupt();
                return;
            }
            // Full second tick, update the RTC values
            halfSecondPassed = false;

            secondIntStatus.Value = true;
            if(secondCounter.Value < 59)
            {
                secondCounter.Value += 1;
            }
            else
            {
                // Minute tick
                secondCounter.Value = 0;
                minuteIntStatus.Value = true;
                if(minuteCounter.Value < 59)
                {
                    minuteCounter.Value += 1;
                }
                else
                {
                    // Hour Tick
                    minuteCounter.Value = 0;
                    hourIntStatus.Value = true;
                    if(hourCounter.Value < 23)
                    {
                        hourCounter.Value += 1;
                    }
                    else
                    {
                        // Day tick
                        hourCounter.Value = 0;
                        dayIntStatus.Value = true;
                        if(dayCounter.Value < BitHelper.Bits(0, dayCounter.Width))
                        {
                            dayCounter.Value += 1;
                        }
                        else
                        {
                            // Day overflow, reset to 0
                            dayCounter.Value = 0;
                        }
                    }
                }
            }
            UpdateAlarm();
            UpdateInterrupt();
        }

        private void UpdateAlarm()
        {
            var alarm = hourAlarm.Value == hourCounter.Value;
            alarm &= minuteAlarm.Value == minuteCounter.Value;
            alarm &= secondAlarm.Value == secondCounter.Value;
            alarmIntStatus.Value |= alarm;
        }

        private void DefineRegisters()
        {
            Registers.IdAndRevision.Define(this)
                .WithValueField(8, 24, FieldMode.Read, valueProviderCallback: _ => 0x030110, name: "ID")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x0, name: "Major")
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x0, name: "Minor");
            Registers.Counter.Define(this)
                .WithValueField(17, 15, out dayCounter, name: "Days")
                .WithValueField(12, 5, out hourCounter, name: "Hours")
                .WithValueField(6, 6, out minuteCounter, name: "Minutes")
                .WithValueField(0, 6, out secondCounter, name: "Seconds");
            Registers.Alarm.Define(this)
                .WithValueField(12, 5, out hourAlarm, name: "Hour")
                .WithValueField(6, 6, out minuteAlarm, name: "Minute")
                .WithValueField(0, 6, out secondAlarm, name: "Second");
            Registers.Control.Define(this)
                .WithTaggedFlag("Freq_Test_En", 8)
                .WithFlag(7, out halfSecondIntEn, name: "Half Second Interrupt Enable")
                .WithFlag(6, out secondIntEn, name: "Second Interrupt Enable")
                .WithFlag(5, out minuteIntEn, name: "Minute Interrupt Enable")
                .WithFlag(4, out hourIntEn, name: "Hour Interrupt Enable")
                .WithFlag(3, out dayIntEn, name: "Day Interrupt Enable")
                .WithFlag(2, out alarmIntEn, name: "Alarm Interrupt Enable")
                .WithFlag(1, out alarmWakeup, name: "Alarm Wakeup Enable")
                .WithFlag(0, name: "RTC_en", writeCallback: (_, val) => internalTimer.Enabled = val)
                .WithChangeCallback((_, __) => UpdateInterrupt());
            Registers.Status.Define(this)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "WriteDone")
                .WithReservedBits(8, 8)
                .WithFlag(7, out halfSecondIntStatus, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Half Second Interrupt Status")
                .WithFlag(6, out secondIntStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "Second Interrupt Status")
                .WithFlag(5, out minuteIntStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "Minute Interrupt Status")
                .WithFlag(4, out hourIntStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "Hour Interrupt Status")
                .WithFlag(3, out dayIntStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "Day Interrupt Status")
                .WithFlag(2, out alarmIntStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "Alarm Interrupt Status")
                .WithReservedBits(0, 2)
                .WithChangeCallback((_, __) => UpdateInterrupt());
            Registers.DigitalTrim.Define(this).Tag("Digital Trim Register", 0, 32);
        }

        private IValueRegisterField dayCounter, hourCounter, minuteCounter, secondCounter;
        private IValueRegisterField hourAlarm, minuteAlarm, secondAlarm;
        private IFlagRegisterField alarmWakeup, alarmIntEn, dayIntEn, hourIntEn, minuteIntEn, secondIntEn, halfSecondIntEn;
        private IFlagRegisterField alarmIntStatus, dayIntStatus, hourIntStatus, minuteIntStatus, secondIntStatus, halfSecondIntStatus;
        private bool halfSecondPassed;

        private readonly LimitTimer internalTimer;

        private enum Registers
        {
            IdAndRevision = 0x00,
            Counter = 0x10,
            Alarm = 0x14,
            Control = 0x18,
            Status = 0x1C,
            DigitalTrim = 0x20,
        }
    }
}