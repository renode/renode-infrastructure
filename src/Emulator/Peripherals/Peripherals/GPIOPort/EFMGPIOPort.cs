//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    static class EFMGPIOPort_Constants
    {
        public const int NB_PORT       = 6;     // Port A,B,C,D,E,F
        public const int PINS_PER_PORT = 16;    // Pin 0 to 15
        public const int PORT_LENGTH   = 9 * 4; // Port is 9 registers long
    }
 
    public class EFMGPIOPort : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public EFMGPIOPort(Machine machine) : 
            base(machine, EFMGPIOPort_Constants.NB_PORT * EFMGPIOPort_Constants.PINS_PER_PORT)
        {

        }

        public long Size
        {
            get
            {
                return 0x140;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            this.Log(LogLevel.Noisy, "Read: offset=" + offset);

            var portNumber = (int)(offset / EFMGPIOPort_Constants.PORT_LENGTH);
            if(portNumber <= EFMGPIOPort_Constants.NB_PORT)
            {
                offset %= EFMGPIOPort_Constants.PORT_LENGTH;
                switch((Register)offset)
                {
                case Register.GPIO_Px_DOUT:
                    // fall through, no break
                case Register.GPIO_Px_DIN:
                    var ret = (uint) 0;
                    var portStart = portNumber * EFMGPIOPort_Constants.PINS_PER_PORT;
                    for (var i = 0; i < EFMGPIOPort_Constants.PINS_PER_PORT; i++)
                    {
                        ret += Convert.ToUInt32(Connections[portStart + i].IsSet) << i;
                    }
                    return ret;
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
            else
            {
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Write: offset=" + offset + ", value=" + value);

            var portNumber = (int)(offset / EFMGPIOPort_Constants.PORT_LENGTH);
            if(portNumber <= EFMGPIOPort_Constants.NB_PORT)
            {
                offset %= EFMGPIOPort_Constants.PORT_LENGTH;
                switch((Register)offset)
                {
                case Register.GPIO_Px_DOUTSET:
                    DoPinOperation(portNumber, Operation.Set, value);
                    break;
                case Register.GPIO_Px_DOUTCLR:
                    DoPinOperation(portNumber, Operation.Clear, value);
                    break;
                case Register.GPIO_Px_DOUTTGL:
                    DoPinOperation(portNumber, Operation.Toggle, value);
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            Connections[number].Set(value);
        }

        private void DoPinOperation(int portNumber, Operation operation, uint value)
        {
            for(var i = 0; i < EFMGPIOPort_Constants.NB_PORT; i++)
            {
                var pinNumber = portNumber * EFMGPIOPort_Constants.NB_PORT + i;
                if((value & 1) != 0)
                {
                    switch(operation)
                    {
                    case Operation.Set:
                        Connections[pinNumber].Set();
                        break;
                    case Operation.Clear:
                        Connections[pinNumber].Unset();
                        break;
                    case Operation.Toggle:
                        Connections[pinNumber].Toggle();
                        break;
                    }
                }
                value >>= 1;
            }
        }

        private enum Operation
        {
            Set,
            Clear,
            Toggle
        }

        private enum Offset : uint
        {
            Set = 0x10,
            Clear = 0x14,
            Toggle = 0x18
        }

        [RegisterMapper.RegistersDescription]
        private enum Register
        {
            /* Port registers */
            GPIO_Px_CTRL = 0x00,        // Cf. drive mode (strength)
            GPIO_Px_MODEL = 0x04,       // Mode (input/output), LSB part
            GPIO_Px_MODEH = 0x08,       // Mode (input/output), MSB part
            GPIO_Px_DOUT = 0x0C,        // Out data (i.e. GPIO state)
            GPIO_Px_DOUTSET = 0x10,     // Set GPIO
            GPIO_Px_DOUTCLR = 0x14,     // Clear GPIO
            GPIO_Px_DOUTTGL = 0x18,     // Toggle GPIO
            GPIO_Px_DIN = 0x1C,         // Input state
            GPIO_Px_PINLOCKN = 0x20,    // Unlocked pins
            /* Global registers */
            GPIP_EXTIPSELL = 0x100,     // Interrupt port select, LSB part
            GPIP_EXTIPSELH = 0x104,     // Interrupt port select, MSB part
            GPIP_EXTIRISE = 0x108,      // Interrupt rising edge trigger
            GPIP_EXTIFALL = 0x10c,      // Interrupt falling edge trigger
            GPIO_IEN = 0x110,           // Interrupt enable
            GPIO_IF = 0x114,            // Interrupt flag
            GPIO_IFS = 0x118,           // Interrupt set
            GPIO_IFC = 0x11c,           // Interrupt clear
            GPIO_ROUTE = 0x120,         // GPIO routing
            GPIO_INSENSE = 0x124,       // Input sense
            GPIO_LOCK = 0x128           // Configuration lock
        }
    }
}

