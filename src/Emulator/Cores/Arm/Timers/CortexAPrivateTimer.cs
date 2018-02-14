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
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.IRQControllers;

/*
0x00 Private Timer Load Register
0x04 Private Timer Counter Register
0x08 Private Timer Control Register
0x0C Private Timer Interrupt Status Register
0x20 Watchdog Load Register 
0x24 Watchdog Counter Register
0x28 Watchdog Control Register
0x2C Watchdog Interrupt Status Register
0x30 Watchdog Reset Status Register
0x34 Watchdog Disable Register

*/

namespace Antmicro.Renode.Peripherals.Timers
{
    public class CortexAPrivateTimer : LimitTimer, IDoubleWordPeripheral
    {
        public CortexAPrivateTimer(Machine machine) : base(machine.ClockSource, 667 * 1000000, direction: Direction.Descending, limit: 0xffffffff, enabled: false)
        {
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }
        
        protected override void OnLimitReached()
        {
            if(EventEnabled)
            {
                IRQ.Set();
                this.Log(LogLevel.Debug, "Timer Alarm !!!");
            }
        }
        
        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord(long offset)
        {
            if(offset > 0x0Cu)
            {
                //throw new NotImplementedException ("Private Watchdog not implemented. Please contact Antmicro fo further support");
            }
            switch(offset)
            {
            case 0x00://load
                return (uint)Limit; 
            case 0x04://counter value
                return (uint)Value;
            case 0x08://control reg
                uint controlReg = 0;
                controlReg |= (uint)((Divider - 1) << 8) | (EventEnabled ? 1u : 0u) | ((AutoUpdate) ? 1u << 1 : 0u) | ((Enabled) ? 1u : 0u);
                return controlReg;
            case 0x0C://interrupt status
                return RawInterrupt ? 1u : 0u;
            default:
                break;
                
            }
            return (uint)Value;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset > 0x0Cu)
            {
                // throw new NotImplementedException ("Private Watchdog not implemented. Please contact Antmicro fo further support");
            }
            
            switch(offset)
            {
            case 0x00://load
                Limit = value;
                break;
            case 0x04://counter value
                Value = value;
                break;
            case 0x08://control reg
                int prescaler;
                uint irqEnable;
                bool autoReload;
                
                prescaler = (byte)(value >> 8);
                irqEnable = (uint)(value & 1u << 2);
                autoReload = ((value & 1u << 1) != 0);
                Enabled = ((value & 1u << 0) != 0);
                EventEnabled = irqEnable != 0;
                AutoUpdate = autoReload;
                Divider = prescaler + 1;
                
                break;
            case 0x0C://interrupt status
                if((value & 0x01) != 0)
                {
                    ClearInterrupt();
                    IRQ.Unset();
                }
                break;
            default:
                break;
                
            }
        }
        #endregion
    }
}

