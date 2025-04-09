//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ZynqMP_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public ZynqMP_RTC(IMachine machine, long frequency = 32767) : base(machine)
        {
            DefineRegisters();
            SecondIRQ = new GPIO();
            AlarmIRQ = new GPIO();

            // Calibration adjusts the period of the ticker up or down. It's done in units of 1/16
            // if calibrationFractionalTicksEnable or 1 otherwise. We always do the multiply here
            // to simplify the implementation.
            // We also set limit == frequency to have the event at 1 Hz by default.
            var fractionalTicks = frequency * FractionalTicksPerTick;
            ticker = new LimitTimer(machine.ClockSource, fractionalTicks, this, nameof(ticker), (ulong)fractionalTicks, Direction.Ascending, enabled: true, eventEnabled: true);
            ticker.LimitReached += HandleTick;
            machine.RealTimeClockModeChanged += _ => SetTimeFromMachine();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            ticker.Reset();
            SetTimeFromMachine();
            UpdateInterrupts();
        }

        public GPIO SecondIRQ { get; }
        public GPIO AlarmIRQ { get; }
        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.SetTimeWrite.Define(this)
                .WithValueField(0, 32, out setTime, name: nameof(setTime))
                .WithWriteCallback((_, __) => SetTimeFromUnixTimestamp(setTime.Value))
            ;

            Registers.SetTimeRead.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: nameof(setTime), valueProviderCallback: _ => setTime.Value)
            ;

            Registers.CalibrationWrite.Define(this)
                .WithValueField(0, 16, out calibrationTicks, name: nameof(calibrationTicks))
                .WithValueField(16, 4, out calibrationFractionalTicks, name: nameof(calibrationFractionalTicks))
                .WithFlag(20, out calibrationFractionalTicksEnable, name: nameof(calibrationFractionalTicksEnable))
                .WithReservedBits(21, 11)
                .WithChangeCallback((_, __) => ApplyCalibration())
            ;

            Registers.CalibrationRead.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: nameof(calibrationTicks), valueProviderCallback: _ => calibrationTicks.Value)
                .WithValueField(16, 4, FieldMode.Read, name: nameof(calibrationFractionalTicks), valueProviderCallback: _ => calibrationFractionalTicks.Value)
                .WithFlag(20, FieldMode.Read, name: nameof(calibrationFractionalTicksEnable), valueProviderCallback: _ => calibrationFractionalTicksEnable.Value)
                .WithReservedBits(21, 11)
            ;

            Registers.CurrentTime.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: nameof(currentTime), valueProviderCallback: _ => DateTimeToUnixTimestamp(currentTime))
            ;

            Registers.Alarm.Define(this)
                .WithValueField(0, 32, out alarmTimestamp, name: nameof(alarmTimestamp))
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, out secondInterruptFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: nameof(secondInterruptFlag))
                .WithFlag(1, out alarmInterruptFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: nameof(alarmInterruptFlag))
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out secondInterruptEnable, name: nameof(secondInterruptEnable))
                .WithFlag(1, out alarmInterruptEnable, name: nameof(alarmInterruptEnable))
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, name: nameof(secondInterruptEnable), writeCallback: (_, value) => { if(value) secondInterruptEnable.Value = true; })
                .WithFlag(1, name: nameof(alarmInterruptEnable), writeCallback: (_, value) => { if(value) alarmInterruptEnable.Value = true; })
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlag(0, name: nameof(secondInterruptEnable), writeCallback: (_, value) => { if(value) secondInterruptEnable.Value = false; })
                .WithFlag(1, name: nameof(alarmInterruptEnable), writeCallback: (_, value) => { if(value) alarmInterruptEnable.Value = false; })
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Control.Define(this)
                .WithReservedBits(0, 31)
                .WithFlag(31, name: "batteryEnable")
            ;
        }

        private void ApplyCalibration()
        {
            var newFrequency = (long)calibrationTicks.Value * FractionalTicksPerTick;
            if(calibrationFractionalTicksEnable.Value)
            {
                newFrequency += (long)calibrationFractionalTicks.Value;
            }
            ticker.Frequency = newFrequency;
        }

        private void SetTime(DateTime dt)
        {
            currentTime = dt;
        }

        private void SetTimeFromMachine()
        {
            SetTime(machine.RealTimeClockDateTime);
        }

        private void SetTimeFromUnixTimestamp(ulong timestamp)
        {
            currentTime = UnixTimestampToDateTime(timestamp);
        }

        private void HandleTick()
        {
            currentTime = currentTime.AddSeconds(1);
            secondInterruptFlag.Value = true;
            // There is no alarm enable bit, so treat the timestamp being 0 as "alarm disabled".
            // The XilinxProcessorIPLib's XRtcPsu_SetAlarm function forbids setting the alarm to timestamp 0
            // so this seems to be a reasonable guess about its behavior.
            alarmInterruptFlag.Value |= alarmTimestamp.Value != 0 && DateTimeToUnixTimestamp(currentTime) >= alarmTimestamp.Value;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var newSecond = secondInterruptFlag.Value && secondInterruptEnable.Value;
            if(SecondIRQ.IsSet != newSecond)
            {
                this.DebugLog("Setting {0} to {1}", nameof(SecondIRQ), newSecond);
                SecondIRQ.Set(newSecond);
            }
            var newAlarm = alarmInterruptFlag.Value && alarmInterruptEnable.Value;
            if(AlarmIRQ.IsSet != newAlarm)
            {
                this.DebugLog("Setting {0} to {1}", nameof(AlarmIRQ), newAlarm);
                AlarmIRQ.Set(newAlarm);
            }
        }

        private static DateTime UnixTimestampToDateTime(ulong timestamp)
        {
            return Misc.UnixEpoch.AddSeconds(timestamp);
        }

        private static ulong DateTimeToUnixTimestamp(DateTime dateTime)
        {
            return (ulong)(dateTime - Misc.UnixEpoch).TotalSeconds;
        }

        private IValueRegisterField setTime;
        private IValueRegisterField alarmTimestamp;
        private IValueRegisterField calibrationTicks;
        private IValueRegisterField calibrationFractionalTicks;
        private IFlagRegisterField calibrationFractionalTicksEnable;
        private IFlagRegisterField secondInterruptFlag;
        private IFlagRegisterField alarmInterruptFlag;
        private IFlagRegisterField secondInterruptEnable;
        private IFlagRegisterField alarmInterruptEnable;

        private DateTime currentTime;

        private readonly LimitTimer ticker;

        private const int FractionalTicksPerTick = 16;

        private enum Registers : long
        {
            SetTimeWrite = 0x00,
            SetTimeRead = 0x04,
            CalibrationWrite = 0x08,
            CalibrationRead = 0x0C,
            CurrentTime = 0x10,
            // CurrentTick = 0x14, // mystery register not used by the XilinxProcessorIPLib or Linux
            Alarm = 0x18,
            InterruptStatus = 0x20,
            InterruptMask = 0x24,
            InterruptEnable = 0x28,
            InterruptDisable = 0x2C,
            Control = 0x40,
        }
    }
}
