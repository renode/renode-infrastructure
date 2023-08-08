//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.Timers
{
    public sealed class SunxiHighSpeedTimer : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public SunxiHighSpeedTimer(IMachine machine, long frequency)
        {
            irqEnableRegister = new DoubleWordRegister(this);
            irqStatusRegister = new DoubleWordRegister(this);

            timers = new SunxiHighSpeedTimerUnit[4];
            interruptFlags = new IFlagRegisterField[4];
            enableFlags = new IFlagRegisterField[4];

            for(var i = 0; i < 4; ++i)
            {
                var j = i;
                timers[i] = new SunxiHighSpeedTimerUnit(machine, frequency);
                timers[i].LimitReached += () => OnTimerLimitReached(j);
                interruptFlags[i] = irqStatusRegister.DefineFlagField(i, FieldMode.WriteOneToClear, name: "Tx_IRQ_PEND");
                enableFlags[i] = irqEnableRegister.DefineFlagField(i, name: "Tx_INT_EN");
            }

            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < 4; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
        }

        public uint ReadDoubleWord(long offset)
        {
            SunxiHighSpeedTimerUnit timerUnit;
            var offsetPerTimer = (Registers)GetTimerRegisterByOffset(offset, out timerUnit);
            switch(offsetPerTimer)
            {
            case Registers.IRQEnable:
                return irqEnableRegister.Read();
            case Registers.IRQStatus:
                return irqStatusRegister.Read();
            case Registers.Control:
                return timerUnit.ControlRegister;
            case Registers.CurrentValueHigh:
                return timerUnit.ValueRegisterHigh;
            case Registers.CurrentValueLow:
                return timerUnit.ValueRegisterLow;
            case Registers.IntervalHigh:
                return timerUnit.IntervalRegisterHigh;
            case Registers.IntervalLow:
                return timerUnit.IntervalRegisterLow;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            SunxiHighSpeedTimerUnit timerUnit;
            var offsetPerTimer = (Registers)GetTimerRegisterByOffset(offset, out timerUnit);
            switch(offsetPerTimer)
            {
            case Registers.IRQEnable:
                irqEnableRegister.Write(offset, value);
                Update();
                break;
            case Registers.IRQStatus:
                irqStatusRegister.Write(offset, value);
                Update();
                break;
            case Registers.Control:
                timerUnit.ControlRegister = value;
                break;
            case Registers.CurrentValueHigh:
                timerUnit.ValueRegisterHigh = value;
                break;
            case Registers.CurrentValueLow:
                timerUnit.ValueRegisterLow = value;
                break;
            case Registers.IntervalHigh:
                timerUnit.IntervalRegisterHigh = value;
                break;
            case Registers.IntervalLow:
                timerUnit.IntervalRegisterLow = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            for(var i = 0; i < 4; ++i)
            {
                timers[i].Reset();
            }
            irqEnableRegister.Reset();
            irqStatusRegister.Reset();
        }

        public long Size
        {
            get
            {
                return 0x2000;
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private int GetTimerRegisterByOffset(long offset, out SunxiHighSpeedTimerUnit timer)
        {
            if(offset < 0x10)
            {
                timer = null;
                return (int)offset;
            }

            var offsetPerTimer = ((offset - 0x10) % 0x20) + 0x10;
            var timerNumber = (uint)(offset - 0x10) / 0x20;
            timer = timers[timerNumber];
            return (int)offsetPerTimer;
        }

        private void OnTimerLimitReached(int timerId)
        {
            this.Log(LogLevel.Noisy, "HSTimer {0} limit reached.", timerId);
            if(enableFlags[timerId].Value)
            {
                interruptFlags[timerId].Value = true;
                Update();
            }
        }

        private void Update()
        {
            for(var i = 0; i < 4; ++i)
            {
                if(enableFlags[i].Value && interruptFlags[i].Value)
                {
                    Connections[i].Set();
                }
                else
                {
                    Connections[i].Unset();
                }
            }
        }

        private readonly SunxiHighSpeedTimerUnit[] timers;
        private readonly IFlagRegisterField[] interruptFlags, enableFlags;
        private readonly DoubleWordRegister irqEnableRegister, irqStatusRegister;

        private enum Registers
        {
            IRQEnable = 0x00,
            IRQStatus = 0x04,
            Control = 0x10,
            IntervalLow = 0x14,
            IntervalHigh = 0x18,
            CurrentValueLow = 0x1c,
            CurrentValueHigh = 0x20
        }

        private sealed class SunxiHighSpeedTimerUnit : LimitTimer
        {
            public SunxiHighSpeedTimerUnit(IMachine machine, long frequency) : base(machine.ClockSource, frequency, direction: Direction.Descending, enabled: false)
            {
                controlRegister = new DoubleWordRegister(this);
                controlRegister.DefineFlagField(7, changeCallback: OnModeChange, name: "MODE");
                controlRegister.DefineValueField(4, 3, writeCallback: (prevVal, val) => OnPrescalerChange((uint)prevVal, (uint)val), name: "PRESC");
                controlRegister.DefineFlagField(1, writeCallback: OnReload, name: "RELOAD");
                controlRegister.DefineFlagField(0, writeCallback: OnEnableChange, name: "ENABLE");
                EventEnabled = true;
            }

            public override void Reset()
            {
                base.Reset();
                controlRegister.Reset();
            }

            public uint IntervalRegisterLow
            {
                get
                {
                    var currentLimt = Limit;
                    savedIntervalHigh = (uint)(currentLimt >> 32) & 0x00ffffff;
                    return (uint)currentLimt;
                }
                set
                {
                    intervalRegisterLow = value;
                }
            }

            public uint IntervalRegisterHigh
            {
                get
                {
                    return savedIntervalHigh;
                }
                set
                {
                    Limit = ((ulong)value << 32) | intervalRegisterLow;
                }
            }

            public uint ValueRegisterLow
            {
                get
                {
                    var currentValue = Value;
                    savedValueHigh = (uint)(currentValue >> 32) & 0x00ffffff;
                    return (uint)currentValue;
                }
                set
                {
                    valueRegisterLow = value;
                }
            }

            public uint ValueRegisterHigh
            {
                get
                {
                    return savedValueHigh;
                }
                set
                {
                    Value = ((ulong)value << 32) | valueRegisterLow;
                }
            }

            public uint ControlRegister
            {
                get
                {
                    return controlRegister.Read();
                }
                set
                {
                    controlRegister.Write((long)Registers.Control, value);
                }
            }

            // THIS IS A WORKAROUND FOR A BUG IN MONO
            // https://bugzilla.xamarin.com/show_bug.cgi?id=39444
            protected override void OnLimitReached()
            {
                base.OnLimitReached();
            }

            private void OnModeChange(bool oldValue, bool newValue)
            {
                Mode = newValue ? WorkMode.OneShot : WorkMode.Periodic;
            }

            private void OnPrescalerChange(ulong oldValue, ulong newValue)
            {
                if(newValue < 4)
                {
                    Divider = 1 << (int)newValue;
                }
                else
                {
                    this.Log(LogLevel.Warning, "Invalid prescaler value: {0}.", newValue);
                }
            }

            private void OnReload(bool oldValue, bool newValue)
            {
                if(newValue)
                {
                    Value = Limit;
                }
            }

            private void OnEnableChange(bool oldValue, bool newValue)
            {
                Enabled = newValue;
            }

            private uint savedIntervalHigh, savedValueHigh, intervalRegisterLow, valueRegisterLow;
            private readonly DoubleWordRegister controlRegister;
        }
    }
}
  
