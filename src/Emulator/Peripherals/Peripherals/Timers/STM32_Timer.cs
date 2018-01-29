//
// Copyright (c) 2010-2017 Antmicro
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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32_Timer : LimitTimer, IDoubleWordPeripheral
    {
        public STM32_Timer(Machine machine) : base(machine.ClockSource, 10000000, direction: Direction.Ascending, limit: 0x100000, enabled: false) {
            AutoUpdate = true;
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }

        protected override void OnLimitReached()
        {
            this.NoisyLog("Alarm!!!");
            if(it_ena > 0) {
                this.NoisyLog("generate interrupt");
                IRQ.Set(false); // TODO: Hack to remove hang-ups
                IRQ.Set(true);
            } else {
                IRQ.Set(false);
	    }
        }

        public uint ReadDoubleWord(long offset)
        {
            /*
	    if(offset == 0x10)
            {
                return config;
            } // TIOCP_CFG
            if(offset == 0x14)
            {
                return 1;
            } //(uint)((this.Value*10) & 0xFFFFFFFF);
            if(offset == 0x2c)
            {
                return load_val;
            }
            if(offset == 0x28)
            {
                return (uint)this.Value*div;
            } // TCRR
	    */
            return 0;
        }
     
        public void WriteDoubleWord(long offset, uint value)
        {
		if (offset == 0x00) {
			uint prescaler = (value & 0xF000000) >> 24;
			prescaler = (uint)Math.Pow(2.0, prescaler);
			this.NoisyLog("CTRL, prescaler = {0}",prescaler);
		}
		if ((offset == 0x4) && ((value & 0x1)==0x1)) {
			this.Enabled = true;
			this.NoisyLog("Timer started");
		}
		if ((offset == 0x4) && ((value & 0x2)==0x2)) {
                IRQ.Set(false);
			this.Enabled = false;
			this.NoisyLog("Timer stopped");
		}
		if (offset == 0x0C) {
			if ((value & 0x2) == 0x2) {
				// underflow irq
				it_ena = 1;
				this.NoisyLog("IRQ ENABLED");
			} else {
				it_ena = 0;
                    IRQ.Set(false);
			}
		}
        }

        const uint div = 100;
        uint it_ena = 0;
    }
}

