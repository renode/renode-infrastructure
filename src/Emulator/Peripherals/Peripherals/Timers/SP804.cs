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
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SP804 : IDoubleWordPeripheral, IKnownSize
    {

        public SP804(Machine machine, int size = 0x1000)
        {
            IRQ = new GPIO();
            this.machine = machine;
            this.size = size;
            idHelper = new PrimeCellIDHelper(size, new byte[] { 0x04, 0x18, 0x14, 0x00, 0x0D, 0xF0, 0x05, 0xB1 }, this);
            Reset();
        }

        public long Size
        {
            get
            {
                return size;
            }
        }

        public GPIO IRQ { get; private set; }

        public uint ReadDoubleWord(long offset)
        {
            var returnValue = 0u;
            if(offset < FirstTimerEnd)
            {
                returnValue = ReadTimer(0, offset);
            }
            else if(offset < SecondTimerEnd)
            {
                returnValue = ReadTimer(1, offset - FirstTimerEnd);
            }
            else
            {
                return idHelper.Read(offset);
            }
             
            return returnValue;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < FirstTimerEnd)
            {
                WriteTimer(0, offset, value);
            }
            else
            if(offset < SecondTimerEnd)
            {
                WriteTimer(1, offset - FirstTimerEnd, value);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public void Reset()
        {
            timers = new InnerTimer[2];
            for(var i = 0; i < timers.Length; i++)
            {
                var j = i;
                timers[i].CoreTimer = new LimitTimer(machine.ClockSource, TimerFrequency, this, i.ToString());
                timers[i].CoreTimer.AutoUpdate = true;
                timers[i].CoreTimer.LimitReached += () => UpdateInterrupt(j, true);
                timers[i].Control = InnerTimer.ControlRegister.InterruptEnable;
            }
            timers[0].CoreTimer.Limit = 0xFFFFFFFF;
            timers[1].CoreTimer.Limit = 0xFFFF;
        }

        private uint ReadTimer(int timerNo, long offset)
        {
            var value = 0u;
            switch((Offset)offset)
            {
            case Offset.Load:
                goto case Offset.BackgroundLoad;
            case Offset.BackgroundLoad:
                value = (uint)timers[timerNo].CoreTimer.Limit;
                break;
            case Offset.Value:
                value = (uint)timers[timerNo].CoreTimer.Value;
                break;
            case Offset.Control:
                value = (uint)timers[timerNo].Control;
                break;
            case Offset.RawInterruptState:
                value = timers[timerNo].CoreTimer.RawInterrupt ? 1u : 0u;
                break;
            case Offset.MaskedInterruptState:
                value = timers[timerNo].CoreTimer.Interrupt ? 1u : 0u;
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
            case Offset.InterruptClear:
                timers[timerNo].CoreTimer.ClearInterrupt();
                UpdateInterrupt(timerNo, false);
                break;
            case Offset.BackgroundLoad:
                timers[timerNo].CoreTimer.Limit = value;
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
            IRQ.Set(timers[1 - timerNo].CoreTimer.Interrupt);
        }

        private InnerTimer[] timers;
        private readonly Machine machine;
        private readonly int size;
        private readonly PrimeCellIDHelper idHelper;

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
                            CoreTimer.Enabled = ((value & ControlRegister.Enable) != 0);
                        }
                        CoreTimer.EventEnabled = (value & (ControlRegister.InterruptEnable)) != 0;
                        control = value;
                        value = value & (ControlRegister.DivideBy16 | ControlRegister.DivideBy256);
                        switch(value)
                        {
                        case ControlRegister.DivideBy16:
                            CoreTimer.Divider = 16;
                            break;
                        case ControlRegister.DivideBy256:
                            CoreTimer.Divider = 256;
                            break;
                        default:
                            CoreTimer.Divider = 1;
                            break;
                        }
                    }
                }
            }

            private ControlRegister control;

            [Flags]
            public enum ControlRegister
            {
                Enable          = (1 << 7),
                PeriodicMode    = (1 << 6), // UNUSED!!!!
                InterruptEnable = (1 << 5),
                DivideBy256     = (1 << 3),
                DivideBy16      = (1 << 2),
                Counter32Bit    = (1 << 1), // UNUSED!!!!
                OneShot         = (1 << 0) // UNUSED!!!!
            }
        }

        private enum Offset : uint
        {
            Load                =   0x000,
            Value               =   0x004,
            Control             =   0x008,
            InterruptClear      =   0x00C,
            RawInterruptState   =   0x010,
            MaskedInterruptState=   0x014,
            BackgroundLoad      =   0x018
        }

        private const uint FirstTimerEnd = 0x20;
        private const uint SecondTimerEnd = 0x40;
        private const int TimerFrequency = 1000000;
    }
}
