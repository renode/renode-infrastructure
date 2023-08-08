//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PeriodicInterruptTimer : IDoubleWordPeripheral
    {

        public PeriodicInterruptTimer(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();
            Reset();
        }

        public GPIO IRQ { get; private set; }

        public uint ReadDoubleWord(long offset)
        {
            if (offset < 0x100) {
                return 0;
            }
            var returnValue = 0u;
            var timer_no = (int) ((offset - 0x100) / 0x10);
            var noffset = (long) (offset - 0x100 - 0x10*timer_no);
            if (timer_no < 8) {
                returnValue = ReadTimer(timer_no, noffset);
            } else {
                this.LogUnhandledRead(offset);
            }
            return returnValue;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (offset < 0x100) {
                return;
            }
            var timer_no = (int) ((offset - 0x100) / 0x10);
            var noffset = (long) (offset - 0x100 - 0x10*timer_no);
            if (timer_no < 8) {
                WriteTimer(timer_no, noffset, value);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public void Reset()
        {
            timers = new InnerTimer[8];
            for(var i = 0; i < timers.Length; i++)
            {
                var j = i;
                timers[i].CoreTimer = new LimitTimer(machine.ClockSource, TimerFrequency, this, i.ToString()) { AutoUpdate = true };
                timers[i].Control = InnerTimer.ControlRegister.InterruptEnable;
                timers[i].CoreTimer.Limit = 0xFFFFFFFF;
                timers[i].CoreTimer.LimitReached += () => UpdateInterrupt(j, true);
            }
        }

        private uint ReadTimer(int timerNo, long offset)
        {
            var value = 0u;
            switch((Offset)offset)
            {
            case Offset.Load:
                value = (uint)timers[timerNo].CoreTimer.Limit;
                break;
            case Offset.Value:
                value = (uint)timers[timerNo].CoreTimer.Value;
                break;
            case Offset.Control:
                value = (uint)timers[timerNo].Control;
                break;
            case Offset.Flag:
                value = timers[timerNo].CoreTimer.RawInterrupt ? 1u : 0u;
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled read from 0x{0:X} (timer {1}).", offset, timerNo);
                value = 0;
                break;
            }
            return value;
        }

        private void WriteTimer(int timerNo, long offset, uint value)
        {
            switch((Offset)offset)
            {
            case Offset.Load:
                timers[timerNo].CoreTimer.Limit = value;
                break;
            case Offset.Value:
                // linux writes here
                break;
            case Offset.Control:
                timers[timerNo].Control = (InnerTimer.ControlRegister)value;
                break;
            case Offset.Flag:
                timers[timerNo].CoreTimer.ClearInterrupt();
                UpdateInterrupt(timerNo, false);
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled write to 0x{0:X}, value 0x{1:X} (timer {2}).", offset, value, timerNo);
                break;
            }
        }

        private void UpdateInterrupt(int timerNo, bool value)
        {
            // this method's code is rather good despite looking strange
            if(value)
            {
                IRQ.Set(true);
                return;
            }
            IRQ.Set(timers[timerNo].CoreTimer.Interrupt);
        }

        private InnerTimer[] timers;
        private readonly IMachine machine;

        private struct InnerTimer
        {      
            public LimitTimer CoreTimer;

            public ControlRegister Control
            {
                get
                {
                    lock(CoreTimer)
                    {
                        return control;
                    }
                }
                set
                {
                    lock(CoreTimer)
                    {
                        if((value & ControlRegister.Enable) != (control & ControlRegister.Enable))
                        {
                            CoreTimer.Enabled = (value & ControlRegister.Enable) != 0;
                        }
                        CoreTimer.EventEnabled = (value & ControlRegister.InterruptEnable) != 0;
                        control = value;
                    }
                }
            }

            private ControlRegister control;

            [Flags]
            public enum ControlRegister
            {
                ChainMode       = (1 << 2),
                InterruptEnable = (1 << 1), 
                Enable          = (1 << 0) 
            }
        }

        private enum Offset : uint
        {
            Load                =   0x000,
            Value               =   0x004,
            Control             =   0x008,
            Flag                =   0x00C,
        }

        private const int TimerFrequency = 66000000;
    }
}
