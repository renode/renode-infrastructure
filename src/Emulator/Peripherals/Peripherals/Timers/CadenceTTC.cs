//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class CadenceTTC : IDoubleWordPeripheral, INumberedGPIOOutput, ITimer
    {
        public CadenceTTC(Machine machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                innerConnections[i] = new GPIO();
            }
            timers = new InnerTimer[NumberOfTimers];
            for(var i = 0; i < timers.Length; i++)
            {
                // this line must stay to avoid access to modified closure
                var j = i;
                timers[i].CoreTimer = new LimitTimer(machine.ClockSource, TimerFrequency, this, i.ToString(), InitialLimit, Direction.Ascending);
                timers[i].CoreTimer.LimitReached += () => OnTimerAlarm(j);
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
        }

        public void SetFrequency(uint timerNo, uint frequency)
        {
            if(timerNo > (timers.Length - 1))
            {
                this.Log(LogLevel.Warning, "Trying to set freqency for unexisting Timer");
                return;
            }
            timers[timerNo].CoreTimer.Frequency = frequency;
            this.Log(LogLevel.Info, "Frequency of timer {0} set to {1}", timerNo, frequency);
        }

        public uint GetCounterValue(uint timerNo)
        {
            var timerValue = timers[timerNo].CoreTimer.Value;
            return (uint)timerValue;
        }

        public uint ReadDoubleWord(long offset)
        {
            uint timerNo = (uint)((offset % 12) / 4);
            long off = offset - timerNo * 4;
            return ReadDoubleWordTimer(timerNo, off);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            uint timerNo = (uint)((offset % 12) / 4);
            long off = offset - timerNo * 4;
            WriteDoubleWordTimer(timerNo, off, value);
        }

        public uint ReadDoubleWordTimer(uint timerNo, long offset)
        {
            //InnerTimer timer = timers[timerNo];
            switch((Offset)offset)
            {
            case Offset.Clock_Control:
                return timers[timerNo].ClockControl;
            case Offset.Counter_Control:
                return timers[timerNo].CounterControl;
            case Offset.Counter_Value:
                var cval = GetCounterValue(timerNo);
                //if(timerNo == 1)
                  //  this.Log(LogType.Info, "T1 val read");
                return cval;
            case Offset.Interval_Counter:
                return timers[timerNo].IntervalCounter; 
            case Offset.Match_1_Counter:
                return timers[timerNo].Match1Counter;
            case Offset.Match_2_Counter:
                return timers[timerNo].Match2Counter;
            case Offset.Match_3_Counter:
                return timers[timerNo].Match3Counter;
            case Offset.Interrupt_Register: 
                uint val = timers[timerNo].InterruptRegister;
                timers[timerNo].InterruptRegister = 0; // read and clr
                Connections[(int)timerNo].Unset();
                return val;
            case Offset.Interrupt_Enable:
                return timers[timerNo].CoreTimer.EventEnabled ? 0u : 1;
            case Offset.Event_Control_Timer:
                return timers[timerNo].EventControlTimer;
            case Offset.Event_Register:
                return timers[timerNo].EventRegister;
            default: 
                this.Log(LogLevel.Warning, "Unhandled read from 0x{1:X} (timer {0}).", timerNo, offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWordTimer(uint timerNo, long offset, uint value)
        {
            //InnerTimer timer = timers[timerNo];
            uint original_value = value;
            value = value & 0xFFFF;
            switch((Offset)offset)
            {
            case Offset.Clock_Control:
                timers[timerNo].ClockControl = value & 0x3F;
                if((timers[timerNo].ClockControl & 1) != 0)
                {
                    timers[timerNo].CoreTimer.Divider = (int)(Math.Pow(2,((value >> 1) & 0xF)+1));
                }    
                else
                {
                    timers[timerNo].CoreTimer.Divider = 1;
                }
                break;
            case Offset.Counter_Control:
                if((value & 0x10) > 0)
                {
                    timers[timerNo].CoreTimer.Value = 0; //reset count value 
                    timers[timerNo].CoreTimer.Enabled = true;  //enable counting
                    
                }
                if(((timers[timerNo].CounterControl ^ value) & 0x1) > 0)
                { // start / stop
                    timers[timerNo].CoreTimer.Enabled = ((value & 0x1) == 0);
                }
                if((value & 0x2) != 0)
                {//set interval
                    timers[timerNo].Mode = 1;
                    if(timers[timerNo].IntervalCounter != 0)
                        timers[timerNo].CoreTimer.Limit = timers[timerNo].IntervalCounter;
                    else
                        timers[timerNo].CoreTimer.Limit = 1;
                } else
                {
                    timers[timerNo].Mode = 0;
                    timers[timerNo].CoreTimer.Limit = 0xFFFF;
                }
                if( ((value & 0x4) ^ (timers[timerNo].CounterControl & 0x4))!=0)
                {
                    if((value & 0x4) > 0)
                    {//set count dir
                        this.Log(LogLevel.Warning,"Timer{0} setting descending count direction", timerNo);
                        timers[timerNo].CoreTimer.Direction = Direction.Descending;
                    } else
                    {
                        this.Log(LogLevel.Warning,"Timer{0} setting ascending count direction", timerNo);
                        timers[timerNo].CoreTimer.Direction = Direction.Ascending;
                    }
                }
                    
                timers[timerNo].CounterControl = (uint)(value & 0x3f & ~0x10);
                break;
            case Offset.Interval_Counter:
                if(value == 0)
                {
                    value ++;
                }
                timers[timerNo].IntervalCounter = value;
                if(timers[timerNo].Mode == 1)
                {
                    timers[timerNo].CoreTimer.Limit = (timers[timerNo].IntervalCounter & 0xFFFF);
                }
                break;
            case Offset.Match_1_Counter:
                timers[timerNo].Match1Counter = value;
                break;
            case Offset.Match_2_Counter:
                timers[timerNo].Match2Counter = value;
                break;
            case Offset.Match_3_Counter:
                timers[timerNo].Match3Counter = value;
                break;
            case Offset.Interrupt_Register://this reg is read only
                break;
            case Offset.Interrupt_Enable:
                timers[timerNo].CoreTimer.EventEnabled = (value & 0x3F) > 0;
                break;
            case Offset.Event_Control_Timer:
                timers[timerNo].EventControlTimer = value & 0x07;
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled write to 0x{1:X}, value 0x{2:X} (timer {0}).", timerNo, offset, original_value);
                break;
            }
        }

        public void Reset()
        {
            for(var i = 0; i < timers.Length; i++)
            {
                // could deadlock if in lock
                timers[i].CoreTimer.Enabled = false;
                lock(timers[i].CoreTimer)
                {
                    timers[i].CoreTimer.Limit = InitialLimit;
                }
                timers[i].ClockControl = 0x0;
                timers[i].CounterControl = 0x0;
                timers[i].IntervalCounter = 0x0;
                timers[i].Match1Counter = 0x0;
                timers[i].Match2Counter = 0x0;
                timers[i].Match3Counter = 0x0;
                timers[i].InterruptRegister = 0x0;
                timers[i].CoreTimer.EventEnabled = false;
                timers[i].EventControlTimer = 0x0;
                timers[i].EventRegister = 0x0;
            }
        }

        private void OnTimerAlarm(int timerNo)
        {
            //this.Log(LogType.Warning, "Timer{0}  ALarm inten_reg = {1:X}",timerNo, timers[timerNo].InterruptEnable);
            lock(timers[timerNo].CoreTimer)
            {
                //TODO: manage rest of possilble ints
                
                //XXX: hack
                //if(timerNo == 1)
                    if(timers[timerNo].Mode == 0)
                    {
                        timers[timerNo].InterruptRegister = 1u << 5;
                        Connections[timerNo].Set();
                    }
                    else if(timers[timerNo].Mode == 1)
                    {
                        timers[timerNo].InterruptRegister = 0x01; 
                        Connections[timerNo].Set();
                    }
                    //this.Log(LogType.Warning, "Interrupt !!! ");
             }
        }

        private InnerTimer[] timers;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private struct InnerTimer
        {
            public LimitTimer CoreTimer;
            public uint ClockControl;
            public uint CounterControl;
            public uint IntervalCounter;
            public uint Match1Counter;
            public uint Match2Counter;
            public uint Match3Counter;
            public uint InterruptRegister;
            public uint EventControlTimer;
            public uint EventRegister;
            public uint Mode;
        }

        private enum Offset
        {
            Clock_Control = 0x00,
            Counter_Control = 0x0C,
            Counter_Value = 0x18,
            Interval_Counter = 0x24,
            Match_1_Counter = 0x30,
            Match_2_Counter = 0x3C,
            Match_3_Counter = 0x48,
            Interrupt_Register = 0x54,
            Interrupt_Enable = 0x60,
            Event_Control_Timer = 0x6C,
            Event_Register = 0x78,
        }

        private const int NumberOfTimers = 3;
        private const uint InitialLimit = 0xFFFF;
        private const int TimerFrequency = 111111111;//400000000;

    }
}

