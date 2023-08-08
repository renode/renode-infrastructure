//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

/*
0x000 TIMERn_CTRL RW Control Register
0x004 TIMERn_CMD W1 Command Register
0x008 TIMERn_STATUS R Status Register
0x00C TIMERn_IEN RW Interrupt Enable Register
0x010 TIMERn_IF R Interrupt Flag Register
0x014 TIMERn_IFS W1 Interrupt Flag Set Register
0x018 TIMERn_IFC W1 Interrupt Flag Clear Register
0x01C TIMERn_TOP RWH Counter Top Value Register
0x020 TIMERn_TOPB RW Counter Top Value Buffer Register
0x024 TIMERn_CNT RWH Counter Value Register
0x028 TIMERn_ROUTE RW I/O Routing Register
0x030 TIMERn_CC0_CTRL RW CC Channel Control Register
*/
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Efm32Timer : LimitTimer, IDoubleWordPeripheral
    {
        public Efm32Timer(IMachine machine) : base(machine.ClockSource, 48000000, direction: Direction.Ascending, limit: 0x100000, enabled: false)
        {
            AutoUpdate = true;
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }

        protected override void OnLimitReached()
        {
            IRQ.Set(true);
        }

        public uint ReadDoubleWord(long offset)
        {   
            if(offset == 0x24)
            {
                return (uint)(Value);
            }

            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == 0x00)
            {
                int prescaler = (int)(value & 0xF000000) >> 24;
                prescaler = 1 << prescaler;
                Divider = prescaler;
                //TODO: Hack. Should support UPDOWN on 2 and quadrature decoder on 3
                if((value & 0x3) == 0)
                {
                    Direction = Direction.Ascending;
                } else
                {
                    Direction = Direction.Descending;
                }
                this.NoisyLog("CTRL, prescaler = {0}", prescaler);
            }
            if(offset == 0x4)
            {
                if((value & 0x1) == 0x1)
                {
                    if(!Enabled)
                    {
                        Enabled = true;
                    }
                    this.NoisyLog("Timer started");
                } 
                else if((value & 0x2) == 0x2)
                {
                    IRQ.Set(false);
                    //this.Disable(); // TODO: timer stopping temporary disabled due to excessive thread usage
                    this.NoisyLog("Timer stopped");
                }
            }
            if(offset == 0x0C)
            {
                if((value & 0x2) == 0x2)
                {
                    // underflow irq
                    EventEnabled = true;
                    
                    this.NoisyLog("IRQ ENABLED");
                } else
                {
                    EventEnabled = false;
                    IRQ.Set(false);
                }
            }
            if(offset == 0x18)
            {
                if((value & 0x3) > 0)
                {
                    ClearInterrupt();
                    IRQ.Set(false);
                }

            }
            if(offset == 0x24)
            {
                Value = value;
            }
        }

    }
}

