//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public sealed class SunxiTimer : IDoubleWordPeripheral, IKnownSize
    {
        public SunxiTimer(IMachine machine)
        {
            timers = new SunxiTimerUnit[NumberOfTimerUnits];
            for(int i = 0; i < NumberOfTimerUnits; ++i)
            {
                int j = i;
                timers[i] = new SunxiTimerUnit(machine, this);
                timers[i].LimitReached += () => OnTimerLimitReached(j);
            }
            timerInterruptEnabled = new IFlagRegisterField[NumberOfTimerUnits];
            timerInterruptStatus = new IFlagRegisterField[NumberOfTimerUnits];
            Timer0Irq = new GPIO();
            Timer1Irq = new GPIO();
            SetupRegisters();
        }

        public void Reset()
        {
            foreach(var timer in timers)
            {
                timer.Reset();
            }
            lowOscillatorControlRegister.Reset();
            timerIrqEnableRegister.Reset();
            timerStatusRegister.Reset();
            Update();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= FirstTimerOffset && offset < LastTimerOffset + TimerUnitSize)
            {
                var timerId = offset / TimerUnitSize - 1;
                var timerOffset = offset % TimerUnitSize;

                // Timers 2-5 are not used by the kernel and have a different structure than timers 0-1, so we didn't implement them.
                if(timerId > 1)
                {
                    this.Log(LogLevel.Warning, "Attempted to read from {1} register of Sunxi timer {2}, which is not implemented.", (Registers)timerOffset, timerId);
                    return 0;
                }

                switch((Registers)timerOffset)
                {
                case Registers.TimerXControl:
                    return timers[timerId].ControlRegister;
                case Registers.TimerXCurrentValue:
                    return (uint)timers[timerId].Value;
                case Registers.TimerXIntervalValue:
                    return (uint)timers[timerId].Limit;
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
            else
            {
                switch((Registers)offset)
                {
                case Registers.TimerIrqEnable:
                    return timerIrqEnableRegister.Read();
                case Registers.TimerStatus:
                    return timerStatusRegister.Read();
                case Registers.LowOscillatorControl:
                    return lowOscillatorControlRegister.Read();
                default:  
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= FirstTimerOffset && offset < LastTimerOffset + TimerUnitSize)
            {
                var timerId = offset / TimerUnitSize - 1;
                var timerOffset = offset % TimerUnitSize;

                // Timers 2-5 are not used by the kernel and have a different structure than timers 0-1, so we didn't implement them.
                if(timerId > 1)
                {
                    this.Log(LogLevel.Warning, "Attempted to write 0x{0:x} to {1} register of Sunxi timer {2}, which is not implemented.", value, (Registers)timerOffset, timerId);
                    return;
                }

                switch((Registers)timerOffset)
                {
                case Registers.TimerXControl:
                    timers[timerId].ControlRegister = value;
                    break;
                case Registers.TimerXCurrentValue:
                    timers[timerId].Value = value;
                    break;
                case Registers.TimerXIntervalValue:
                    timers[timerId].Limit = value;
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
            else
            {
                switch((Registers)offset)
                {
                case Registers.TimerIrqEnable:
                    timerIrqEnableRegister.Write(offset, value);
                    break;
                case Registers.TimerStatus:
                    timerStatusRegister.Write(offset, value);
                    Update();
                    break;
                case Registers.LowOscillatorControl:
                    lowOscillatorControlRegister.Write(offset, value);
                    break;
                default:  
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public GPIO Timer0Irq
        {
            get;
            private set;
        }

        public GPIO Timer1Irq
        {
            get;
            private set;
        }

        private void SetupRegisters()
        {
            timerIrqEnableRegister = new DoubleWordRegister(this);
            timerStatusRegister = new DoubleWordRegister(this);
            for(int i = 0; i < NumberOfTimerUnits; ++i)
            {
                timerInterruptEnabled[i] = timerIrqEnableRegister.DefineFlagField(i);
                timerInterruptStatus[i] = timerStatusRegister.DefineFlagField(i, FieldMode.WriteOneToClear | FieldMode.Read);
            }
            lowOscillatorControlRegister = new DoubleWordRegister(this, 0x4000);
            lowOscillatorControlRegister.DefineFlagField(0, changeCallback: (oldValue, newValue) => lowOscillatorFrequency = newValue ? 32768 : 32000);
        }

        private void OnTimerLimitReached(int timerId)
        {
            if(timerInterruptEnabled[timerId].Value)
            {
                timerInterruptStatus[timerId].Value = true;
                Update();
            }
        }

        private void Update()
        {
            if(timerInterruptStatus[0].Value)
            {
                Timer0Irq.Set();
            }
            else
            {
                Timer0Irq.Unset();
            }

            if(timerInterruptStatus[1].Value)
            {
                Timer1Irq.Set();
            }
            else
            {
                Timer1Irq.Unset();
            }
        }

        private SunxiTimerUnit[] timers;
        private DoubleWordRegister timerIrqEnableRegister, timerStatusRegister, lowOscillatorControlRegister;
        private IFlagRegisterField[] timerInterruptEnabled, timerInterruptStatus;
        private long lowOscillatorFrequency;

        private const int NumberOfTimerUnits = 2, TimerUnitSize = 0x10, FirstTimerOffset = 0x10, LastTimerOffset = 0x60;

        private sealed class SunxiTimerUnit : LimitTimer
        {
            public SunxiTimerUnit(IMachine machine, SunxiTimer parent) : base(machine.ClockSource, 24000000, direction: Antmicro.Renode.Time.Direction.Descending, enabled: false, eventEnabled: true)
            {
                timerGroup = parent;
                controlRegister = new DoubleWordRegister(this, 0x04);
                controlRegister.DefineFlagField(7, changeCallback: (oldValue, newValue) => Mode = newValue ? WorkMode.OneShot : WorkMode.Periodic);
                controlRegister.DefineValueField(4, 3, changeCallback: (oldValue, newValue) => Divider = 1 << (int)newValue);
                controlRegister.DefineFlagField(1, FieldMode.WriteOneToClear, writeCallback: (oldValue, newValue) => Value = Limit);
                controlRegister.DefineFlagField(0, changeCallback: (oldValue, newValue) => Enabled = newValue);
                controlRegister.DefineEnumField<ClockSource>(2, 2, changeCallback: OnClockSourceChange);
            }

            public uint ControlRegister
            {
                get
                {
                    return controlRegister.Read();
                }
                set
                {
                    controlRegister.Write((long)Registers.TimerXControl, value);
                }
            }

            // THIS IS A WORKAROUND FOR A BUG IN MONO
            // https://bugzilla.xamarin.com/show_bug.cgi?id=39444
            protected override void OnLimitReached()
            {
                base.OnLimitReached();
            }

            private void OnClockSourceChange(ClockSource oldValue, ClockSource newValue)
            {
                switch(newValue)
                {
                case ClockSource.LowSpeedOscillator:
                    Frequency = timerGroup.lowOscillatorFrequency;
                    break;
                case ClockSource.Osc24M:
                    Frequency = 24000000;
                    break;
                case ClockSource.Pll6:
                    Frequency = 200000000;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Invalid clock source value.");
                    break;
                }
            }
                
            private readonly DoubleWordRegister controlRegister;
            private readonly SunxiTimer timerGroup;

            private enum ClockSource
            {
                LowSpeedOscillator = 0x00,
                Osc24M = 0x01,
                Pll6 = 0x10
            }
        }

        private enum Registers
        {
            TimerIrqEnable = 0x00,
            TimerStatus = 0x4,
            TimerXControl = 0x00,
            TimerXIntervalValue = 0x04,
            TimerXCurrentValue = 0x08,
            AvsControl = 0x80,
            AvsCounter0 = 0x84,
            AvsCounter1 = 0x88,
            AvsDivisor = 0x8c,
            WatchdogControl = 0x90,
            WatchdogMode = 0x94,
            LowOscillatorControl = 0x100,
            RtcYearMonthDay = 0x104,
            RtcHourMinuteSecond = 0x108,
            AlarmDayHourMinuteSecond = 0x10c,
            AlarmWeekHourMinuteSecond = 0x110,
            AlarmEnable = 0x114,
            AlarmIrqEnable = 0x118,
            AlarmIrqStatus = 0x11c,
            AlarmConfig = 0x170
        }
    }
}
    