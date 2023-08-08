//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class TexasInstrumentsTimer : IDoubleWordPeripheral, IKnownSize
    {
        public TexasInstrumentsTimer(IMachine machine)
        {
            IRQ12 = new GPIO();
            IRQ34 = new GPIO();

            timer12 = new LimitTimer(machine.ClockSource, 24000000, this, nameof(timer12), direction: Direction.Ascending, eventEnabled: true); // clocked from AUXCLK (default 24 MHz)
            timer34 = new LimitTimer(machine.ClockSource, 24000000, this, nameof(timer34), direction: Direction.Ascending, eventEnabled: true);
            timer12.LimitReached += () => OnTimerLimitReached(timer12);
            timer34.LimitReached += () => OnTimerLimitReached(timer34);

            timerControlRegister = new DoubleWordRegister(this);
            timerGlobalControlRegister = new DoubleWordRegister(this, 3);
            timerInterruptControlAndStatusRegister = new DoubleWordRegister(this, 0x10001); // the driver expects interrupts to be enabled; inconsistent with timer user's guixde

            timerControlRegister.DefineEnumField<OperationMode>(6, 2, changeCallback: (oldValue, newValue) => OnEnableModeChanged(oldValue, newValue, timer12));
            timerControlRegister.DefineEnumField<OperationMode>(22, 2, changeCallback: (oldValue, newValue) => OnEnableModeChanged(oldValue, newValue, timer34));
            resetOnRead12 = timerControlRegister.DefineFlagField(10);
            resetOnRead34 = timerControlRegister.DefineFlagField(26);

            timerGlobalControlRegister.DefineFlagField(0, changeCallback: (oldValue, newValue) =>
            {
                if(!newValue)
                {
                    timer12.Reset();
                }
            });
            timerGlobalControlRegister.DefineFlagField(1, changeCallback: (oldValue, newValue) =>
            {
                if(!newValue)
                {
                    timer34.Reset();
                }
            });

            timerGlobalControlRegister.DefineEnumField<TimerMode>(2, 2, changeCallback: OnTimerModeChanged);

            timerGlobalControlRegister.DefineValueField(8, 4, changeCallback: (oldValue, newValue) => timer34.Divider = (int)newValue);
            interruptEnable12 = timerInterruptControlAndStatusRegister.DefineFlagField(0);
            interruptEnable34 = timerInterruptControlAndStatusRegister.DefineFlagField(16);
            interruptOccurred12 = timerInterruptControlAndStatusRegister.DefineFlagField(3);
            interruptOccurred34 = timerInterruptControlAndStatusRegister.DefineFlagField(19);

            Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.TimerCounter12:
                CounterRegister12 = value;
                Update();
                break;
            case Registers.TimerCounter34:
                CounterRegister34 = value;
                Update();
                break;
            case Registers.TimerControl:
                timerControlRegister.Write(offset, value);
                Update();
                break;
            case Registers.TimerGlobalControl:
                timerGlobalControlRegister.Write(offset, value);
                break;
            case Registers.TimerPeriod12:
                timerPeriod12 = value;
                UpdateTimerLimits();
                Update();
                break;
            case Registers.TimerPeriod34:
                timerPeriod34 = value;
                UpdateTimerLimits();
                Update();
                break;
            case Registers.TimerInterruptControlAndStatus:
                timerInterruptControlAndStatusRegister.Write(offset, value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.PeripheralIdentification12:
                return peripheralIdentification;
            case Registers.TimerCounter12:
                if(resetOnRead12.Value && timerMode == TimerMode.Unchained32) // only available in 32 bit unchained mode
                {
                    var value = CounterRegister12;
                    CounterRegister12 = 0;
                    return value;
                }
                return CounterRegister12;
            case Registers.TimerCounter34:
                if(resetOnRead34.Value && timerMode == TimerMode.Unchained32) // only available in 32 bit unchained mode
                {
                    var value = CounterRegister34;
                    CounterRegister34 = 0;
                    return value;
                }
                return CounterRegister34;
            case Registers.TimerControl:
                return timerControlRegister.Read();
            case Registers.TimerGlobalControl:
                return timerGlobalControlRegister.Read();
            case Registers.TimerPeriod12:
                return timerPeriod12;
            case Registers.TimerPeriod34:
                return timerPeriod34;
            case Registers.TimerInterruptControlAndStatus:
                return timerInterruptControlAndStatusRegister.Read();
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void OnTimerLimitReached(LimitTimer timer)
        {
            if(timer == timer12 && interruptEnable12.Value)
            {
                interruptOccurred12.Value = true;
                Update();
            }
            else if(timer == timer34 && interruptEnable34.Value)
            {
                interruptOccurred34.Value = true;
                Update();
            }
        }

        public void Reset()
        {
            timer12.Reset();
            timer34.Reset();
            timerControlRegister.Reset();
            timerGlobalControlRegister.Reset();
            timerInterruptControlAndStatusRegister.Reset();
        }

        public long Size
        {
            get
            {
                return 0x200;
            }
        }

        public GPIO IRQ12
        {
            get;
            private set;
        }

        public GPIO IRQ34
        {
            get;
            private set;
        }

        private void UpdateTimerLimits()
        {
            if(timerMode == TimerMode.GeneralPurpose64)
            {
                timer12.Limit = (((ulong)timerPeriod12) << 32) | timerPeriod34;
            }
            else
            {
                timer12.Limit = timerPeriod12;
                timer34.Limit = timerPeriod34;
            }
        }

        private void Update()
        {
            if(interruptOccurred12.Value)
            {
                IRQ12.Blink();
                interruptOccurred12.Value = false;
            }
            else if(interruptOccurred34.Value)
            {
                IRQ34.Blink();
                interruptOccurred34.Value = false;
            }
        }

        private void OnEnableModeChanged(OperationMode oldValue, OperationMode newValue, LimitTimer timer)
        {
            switch(newValue)
            {
            case OperationMode.Disabled:
                timer.Enabled = false;
                break;
            case OperationMode.Continuous:
            case OperationMode.ContinuousReload:
                timer.Mode = WorkMode.Periodic;
                timer.Enabled = true;
                timer.EventEnabled = true;
                break;
            case OperationMode.Once:
                timer.Mode = WorkMode.OneShot;
                timer.Enabled = true;
                timer.EventEnabled = true;
                break;
            }
        }

        private void OnTimerModeChanged(TimerMode oldValue, TimerMode newValue)
        {
            timerMode = newValue;
            switch(newValue)
            {
            case TimerMode.GeneralPurpose64:
                timer34.Enabled = false;
                break;
            case TimerMode.Watchdog64:
            case TimerMode.Chained32:
                this.Log(LogLevel.Warning, "Unsupported TMS320 timer mode set: {0}", newValue);
                break;
            }
            UpdateTimerLimits();
        }

        private uint CounterRegister12
        {
            get
            {
                if(timerMode == TimerMode.GeneralPurpose64)
                {
                    temporaryCounterRegister34 = (uint)(timer12.Value >> 32);
                    return (uint)(timer12.Value);
                }
                else
                {
                    return (uint)timer12.Value;

                }
            }
            set
            {
                if(timerMode == TimerMode.GeneralPurpose64)
                {
                    timer12.Value &= ~0xffffffffUL;
                    timer12.Value |= value;
                }
                else
                {
                    timer12.Value = value;
                }
            }
        }

        private uint CounterRegister34
        {
            get
            {
                if(timerMode == TimerMode.GeneralPurpose64)
                {
                    return temporaryCounterRegister34;
                }
                else
                {
                    return (uint)timer34.Value;
                }
            }
            set
            {
                if(timerMode == TimerMode.GeneralPurpose64)
                {
                    timer12.Value &= 0xffffffffL;
                    timer12.Value |= ((ulong)value << 32);
                }
                else
                {
                    timer34.Value = value;
                }
            }
        }

        private uint temporaryCounterRegister34, timerPeriod12, timerPeriod34;
        private readonly DoubleWordRegister timerControlRegister, timerGlobalControlRegister, timerInterruptControlAndStatusRegister;
        private readonly IFlagRegisterField resetOnRead12, resetOnRead34, interruptEnable12, interruptEnable34, interruptOccurred12, interruptOccurred34;
        private TimerMode timerMode;
        private readonly LimitTimer timer12, timer34;
        private const uint peripheralIdentification = 0x010701;

        private enum Registers
        {
            PeripheralIdentification12 = 0x00,
            EmulationManagement = 0x04,
            TimerCounter12 = 0x10,
            TimerCounter34 = 0x14,
            TimerPeriod12 = 0x18,
            TimerPeriod34 = 0x1c,
            TimerControl = 0x20,
            TimerGlobalControl = 0x24,
            WatchdogTimerControl = 0x28,
            TimerReload12 = 0x34,
            TimerReload34 = 0x38,
            TimerCapture12 = 0x3c,
            TimerCapture34 = 0x40,
            TimerInterruptControlAndStatus = 0x44,
        }

        private enum TimerMode
        {
            GeneralPurpose64 = 0x0,
            Unchained32 = 0x1,
            Watchdog64 = 0x2,
            Chained32 = 0x3
        }

        private enum OperationMode
        {
            Disabled = 0x0,
            Once = 0x1,
            Continuous = 0x2,
            ContinuousReload = 0x3
        }
    }
}

