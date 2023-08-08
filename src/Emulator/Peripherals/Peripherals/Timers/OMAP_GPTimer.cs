//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class OMAP_GPTimer : LimitTimer, IDoubleWordPeripheral
    {
        public OMAP_GPTimer(IMachine machine) : base (machine.ClockSource, (38400000), direction: Direction.Ascending, limit: (0xFFFFFFFF), enabled: true)
        { // TODO: hack - 10 times slower, because of Stopwatch limitation
            AutoUpdate = true;
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }

        protected override void OnLimitReached()
        {
            this.NoisyLog("Alarm!!!");
            if(it_ena > 0)
            {
                this.NoisyLog("generate interrupt");
                IRQ.Set(true);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
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
                return (uint)this.Value;
            } // TCRR
            return 0;
        }
     
        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == 0x10)
            {
                config = value & 0x33d;
            }
            if(offset == 0x1c)
            {
                it_ena = value & 0x7;
            }
            if(offset == 0x24)
            {
                if((value & (1 << 5)) > 0)
                {
                    Divider = (int)Math.Pow(2, (((value >> 2) & 7) + 1));
                }
                else
                {
                    Divider = (int)Math.Pow(2, 0);
                }
                Enabled = ((value & 1) > 0);
            }
            if(offset == 0x2C)
            {
                this.Limit = 0xFFFFFFFF - value;
                load_val = value;
            }
            if(offset == 0x18)
            {
                IRQ.Set(false);
            }
            if(offset == 0x28)
            {
                this.NoisyLog("timer is at {0} of {1}", this.Value, this.Limit);
            }
        }

        uint config = 0;
        uint load_val = 0;
        uint it_ena = 0;
    }
}

