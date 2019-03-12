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
    public class GaislerGPTimer : IDoubleWordPeripheral, IGaislerAPB, INumberedGPIOOutput
    {
        public GaislerGPTimer(int timersNumber, Machine machine, int frequency = DefaultTimerFrequency)
        {
            this.numberOfTimers = timersNumber;
            if(timersNumber > MaximumNumberTimersImplemented)
            {
                this.Log(LogLevel.Warning, "Registration of unsupported  number of Timers, defaulting to maximum {0:X]", MaximumNumberTimersImplemented);
                this.numberOfTimers = MaximumNumberTimersImplemented;
            }
            timers = new InnerTimer[numberOfTimers];
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfTimers; i++)
            {
                var j = i;
                timers[i] = new InnerTimer(machine, frequency, this);
                timers[i].CoreTimer.LimitReached += () => OnTimerAlarm(j);
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            Reset();
        }

        public void Reset ()
        {
            registers = new DeviceRegisters();
            registers.Configuration.TimersNumber = (uint)numberOfTimers;

            for(var i = 0; i < numberOfTimers; i++)
            {
                timers[i].Reset();
                timers[i].CoreTimer.Divider = (int)registers.Configuration.TimersNumber + 1;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= (uint)registerOffset.Timers)
            {
                var timerNumber = (uint)((offset & 0xf0) >> 4) - 1; 
                if ( timerNumber >= registers.Configuration.TimersNumber )
                {
                    this.Log(LogLevel.Warning,"Read from non existing Timer");
                    return 0;
                }
                var timerOffset = (uint)(offset & 0x0f);
                var timer = timers[timerNumber];
                switch((timerRegisterOffset)timerOffset)
                {
                case timerRegisterOffset.Control:
                    return timer.Control.GetValue();
                case timerRegisterOffset.CounterValue:
                    return (uint)timer.CoreTimer.Value;
                case timerRegisterOffset.ReloadValue:
                    return timer.ReloadValue;
                default:
                    this.Log(LogLevel.Warning, "Unhandled read from 0x{0:X} (timer {1}, offset 0x{2:X}).", offset, timerNumber, timerOffset);
                    return 0;

                }
            }
            else
            {
                switch( (registerOffset) offset )
                {
                case registerOffset.Configuration:
                    return registers.Configuration.GetValue();
                case registerOffset.ScalerReloadValue:
                    return registers.ScalerReloadValue;
                case registerOffset.ScalerValue:
                    return registers.ScalerValue;
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            if ( offset >= (uint)registerOffset.Timers )
            {
                var timerNumber = (uint)( ((offset & 0xf0) >> 4) ) - 1;
                if ( timerNumber >= registers.Configuration.TimersNumber )
                {
                    this.Log(LogLevel.Warning,"Write to non existing Timer");
                    return;
                }
                var timerOffset = (uint)(offset & 0xf0);
                var timer = timers[timerNumber];
                switch ( (timerRegisterOffset)(offset - timerOffset) )
                {
                case timerRegisterOffset.CounterValue:
                    return;
                case timerRegisterOffset.ReloadValue:
                    timer.ReloadValue = value;
                    timer.CoreTimer.Limit = timer.ReloadValue;
                    return;
                case timerRegisterOffset.Control:
                    timer.Control.SetValue(value);
                    
                    if(timer.Control.Enable)//enable timer
                    {
                        if(!timer.CoreTimer.Enabled)
                        {
                            timer.CoreTimer.Enabled = true;
                        }
                    }
                    else//disable timer
                    {
                        timer.CoreTimer.Enabled = false;
                    }
                    
                    if(timer.Control.Restart)//set mode to periodical
                    {
                        timer.CoreTimer.Mode = WorkMode.Periodic;
                    }
                    else//set mode to one shot
                    {
                        timer.CoreTimer.Mode = WorkMode.OneShot;
                    }
                    if( timer.Control.Load )//load value
                    {
                        timer.CoreTimer.Value = timer.ReloadValue;
                        timer.Control.Load = false;
                    }
                    return;
                default:
                    this.Log(LogLevel.Warning,"Unhandled write to {0}, value {1:X} (timer {2}, offset 0x{3:X}).", offset, value, timerNumber, offset - timerNumber);
                    return;
                }
                
            }
            else
            {
                switch ( (registerOffset)offset )
                {
                case registerOffset.ScalerValue:
                    registers.ScalerValue = value;
                    return;
                case registerOffset.ScalerReloadValue:
                    registers.ScalerReloadValue = value;
                    foreach(var curTimer in timers)
                    {
                        curTimer.CoreTimer.Divider = (int)registers.ScalerReloadValue + 1;
                    }
                    return;
                case registerOffset.Configuration:
                    registers.Configuration.DisableFreeze = (value & (1u<<9)) != 0;
                    return;
                default:
                    this.LogUnhandledWrite(offset, value);
                    return;
                }
            }
        }


        private void checkInterrupt()
        {
            var interrupt = false;
            foreach( InnerTimer tmr in timers )
            {
                if(tmr.Control.Interrupt)
                {
                    interrupt = true;
                }
            }
            if(!interrupt)
            {
                Connections[0].Unset();
            }
        }
        
        private void OnTimerAlarm(int timerNo)
        {
            var timer = timers[timerNo];
            lock(timer.CoreTimer)
            {
                if(timer.Control.InterruptEnable)
                {
                    timer.Control.Interrupt = true;
                    if(registers.Configuration.SeparateInterrupts) //if each timer has its own interrupt
                    {
                        Connections[timerNo].Set();
                    }
                    else //if all timers use global interrupt
                    {
                        Connections[0].Set();
                        Connections[0].Unset();
                    }
                }
            }
        }
        
        private InnerTimer[] timers;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
        private readonly uint vendorID = 0x01;  // Aeroflex Gaisler
        private readonly uint deviceID = 0x011; // GRLIB GPTimer
        private readonly GaislerAPBPlugAndPlayRecord.SpaceType spaceType = GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;
        private DeviceRegisters registers;
        private const int DefaultTimerFrequency = 1000000;
        private const uint InitialLimit = 0xFFFF;
        private const int MaximumNumberTimersImplemented = 7;

        private enum registerOffset : uint
        {
            ScalerValue = 0x00,
            ScalerReloadValue = 0x04,
            Configuration = 0x08,
            Timers = 0x10
        }
        
        private enum timerRegisterOffset : uint
        {
            CounterValue = 0x00,
            ReloadValue = 0x04,
            Control = 0x08
        }
        
        private class DeviceRegisters
        {
            public uint ScalerValue;
            public uint ScalerReloadValue = 7;
            public ConfigurationRegister Configuration = new ConfigurationRegister();
            public class ConfigurationRegister
            {
                public bool DisableFreeze = true;
                public readonly bool SeparateInterrupts = false;
                public readonly uint InterruptNumber = 8;
                public uint TimersNumber;
                public uint GetValue()
                {
                    var retVal = (DisableFreeze ? 1u<<9 : 0) | (SeparateInterrupts ? 1u<<8 : 0) | ( (InterruptNumber & 0x1f)<<3 ) | (TimersNumber & 0x07);
                    return retVal;
                }
            }
        }
  
        private class InnerTimer
        {
            public void Reset()
            {
                ReloadValue = 0;
                Control = new ControlRegister();
                CoreTimer.Reset();
            }
            
            public InnerTimer(Machine machine, int frequency, GaislerGPTimer parent)
            {
                CoreTimer = new LimitTimer(machine.ClockSource, frequency, parent, nameof(CoreTimer), InitialLimit, Direction.Descending, eventEnabled: true);
            }

            public LimitTimer CoreTimer;
            public uint ReloadValue;
            public ControlRegister Control;

            public class ControlRegister
            {
                public bool Enable;
                public bool Restart;
                public bool Load;
                public bool InterruptEnable;
                public bool Interrupt;
                public bool Chain;

                public void SetValue(uint value)
                {
                    Enable = (value & 1u<<0) != 0;
                    Restart = (value & 1u<<1) != 0;
                    Load = (value & 1u<<2) != 0;
                    InterruptEnable = (value & 1u<<3) != 0;
                    Interrupt = ( (value & 1u<<4) != 0 ) ? false : Interrupt;
                    Chain = (value & 1u<<5) != 0;
                }
                public uint GetValue()
                {
                    var retVal = (Enable ? 1u<<0 : 0) | (Restart ? 1u<<1 : 0) | (InterruptEnable ? 1u<<3 : 0) | (Interrupt ? 1u<<4 : 0);
                    retVal |= (Chain ? 1u<<5 : 0);
                    return retVal;
                }
            }           

        }

        public uint GetVendorID ()
        {
            return vendorID;
        }

        public uint GetDeviceID ()
        {
            return deviceID;
        }

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType ()
        {
            return spaceType;
        }
        
        public uint GetInterruptNumber()
        {
            var irqEndpoints = Connections[0].Endpoints;
            if(irqEndpoints.Count > 0)
            {
                return (uint)irqEndpoints[0].Number;
            }
            return 0;
        }

        private readonly int numberOfTimers;
    }
}
