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
using System.Collections.Generic;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public class STM32W_UART :  IDoubleWordPeripheral, IUART
    {
        public STM32W_UART()
        {
            IRQ = new GPIO();
            charFifo = new Queue<byte>();
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public GPIO IRQ { get; private set; }

        public void WriteChar(byte value)
        {
            lock(charFifo)
            {
                charFifo.Enqueue(value);
                Update();
            }
        }

        [ConnectionRegion("irq")]
        public void WriteDoubleWordIRQ(long address, uint value)
        {

        }

        public void WriteDoubleWord(long address, uint value)
        {	
            if(address == 0x3C)
            {
                var handler = CharReceived;
                if(handler != null)
                {
                    handler((byte)(value & 0xFF));
                }
                return;
                // data register
            }

            switch((Register)address)
            {
            case Register.Control:
                controlRegister = value;
                break;
            case Register.BaudRate1:
                baudRate1 = value;
                break;
            case Register.BaudRate2:
                baudRate2 = value;
                break;
            }
        }

        [ConnectionRegion("irq")]
        public uint ReadDoubleWordIRQ(long offset)
        {
            if(offset == 0)
            {
                uint res = (1 << 1);
                if(charFifo.Count > 0)
                    res |= 1;
                return res;
            }
            return 0;
        }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
            case 0x3c:
                var returnValue = charFifo.Dequeue();
                Update();
                if(returnValue == 0x0D)
                    return 0x0A;
                return returnValue;
            default:
                return 0;
            }
        }

        public void Reset()
        {
            // TODO!
        }

        private void Update()
        {
            IRQ.Set(/*txInterruptEnabled ||*/ charFifo.Count > 0);
        }

        private uint controlRegister;
        private uint baudRate1;
        private uint baudRate2;
        private readonly Queue<byte> charFifo;

        [Flags]
        private enum Control : uint
        {
            Stop            = 1 << 2,
            ParityEnabled   = 1 << 3,
            ParitySelection = 1 << 4
        }

        private enum Register : long
        {
            // TODO: I don't know if the offset should be 0xC85C or Ox5C - check it!
            Control    = 0xC85C, // SC1_UARTCR
            BaudRate1  = 0xC868, // SC1_UARTBRR1
            BaudRate2  = 0xC86C  // SC1_UARTBRR2
        }

        public Bits StopBits
        {
            get
            {
                return (controlRegister & (uint)Control.Stop) == 0 ? Bits.One : Bits.Two;
            }
        }

        public Parity ParityBit
        {
            get
            {
                if ((controlRegister & (uint)Control.ParityEnabled) == 0)
                {
                    return Parity.None;
                }
                else
                {
                    return (controlRegister & (uint)Control.ParitySelection) == 0 ? Parity.Even : Parity.Odd;
                }
            }
        }

        public uint BaudRate
        {
            get
            {
                var divisor = ((2 * (baudRate1 & 0xFFFF) + (baudRate2 & 0x1)));
                return divisor == 0 ? 0 : UARTClockFrequency / divisor;
            }
        }

        private const uint UARTClockFrequency = 24000000; // 24Mhz
    }
}

