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
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class VybridGPIOPort : BaseGPIOPort, IBusPeripheral
    {
        public VybridGPIOPort(Machine machine) : base(machine, 32)
        {
            IRQ = new GPIO();
        }

        [ConnectionRegion("gpio")]
        public uint ReadDoubleWordFromGPIO(long offset)
        {
            switch(offset)
	    {
            case 0x10:
                return GetSetGPIOs();
		default:
			this.LogUnhandledRead(offset);
			break;
	    }
            return 0;
        }

        [ConnectionRegion("gpio")]
        public void WriteDoubleWordToGPIO(long offset, uint value)
        {
            switch(offset)
            {
	    case 0x0: // gpio output
	        gpio_v = value;
                foreach(var cn in Connections)
                {
                    if((gpio_v & 1u << cn.Key) != 0)
                    {
                        cn.Value.Set();
                    }
                    else
                    {
                        cn.Value.Unset();
                    }
                }
		break;		
            case 0x4: // gpio set
                this.Log(LogLevel.Debug, "Setting {0:X}", value);
                gpio_v |= value;
                SetConnectionsStateUsingBits(gpio_v);
                break;
            case 0x8: // gpio clr
                this.Log(LogLevel.Debug, "Clearing {0:X}", value);
                gpio_v &= ~value;
                SetConnectionsStateUsingBits(gpio_v);
                break;
            default:
                this.LogUnhandledWrite(offset,value);
                break;
            }
            return;
        }

        [ConnectionRegion("port")]
        public uint ReadDoubleWordFromPORT(long offset)
        {
            if(offset >= (long)Registers.PinControlRegisterStart && offset <= (long)Registers.PinControlRegisterEnd)
            {
                //nothing yet
            }
            switch((Registers)offset)
            {
            case Registers.InterruptStatusFlag:
                return GetSetGPIOs();
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        [ConnectionRegion("port")]
        public void WriteDoubleWordToPORT(long offset, uint value)
        {
            if(offset >= (long)Registers.PinControlRegisterStart && offset <= (long)Registers.PinControlRegisterEnd)
            {
                //nothing yet
            }
            switch((Registers)offset)
            {
            case Registers.InterruptStatusFlag:
                ClearGPIOs(value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            //no base call, we want to set state as a latch
            if(value)
            {
                State[number] = true;
                Update();
            }
        }

        public GPIO IRQ
        {
            get;
            private set;
        }

        private void ClearGPIOs(uint which)
        {
            foreach(var setBit in BitHelper.GetSetBits(which))
            {
                State[setBit] = false;
            }
            Update();
        }

        private uint GetSetGPIOs()
        {
            var interruptStatusFlag = 0u;
            for(var i = 0; i < State.Length; ++i)
            {
                if(State[i])
                {
                    interruptStatusFlag |= (1u << i);
                }
            }
            return interruptStatusFlag;
        }


        private void Update()
        {
            if(State.Any(x => x))
            {
                IRQ.Set();
            }
            else
            {
                IRQ.Unset();
            }
        }

        private enum Registers
        {
            PinControlRegisterStart = 0x00,
            PinControlRegisterEnd = 0xC7,
            InterruptStatusFlag = 0xA0,
            DigitalFilterEnable = 0xC0,
            DigitalFilterClock = 0xC4,
            DigitalFilterWidth = 0xC8,
        }

        private uint gpio_v = 0x0;
    }
}

