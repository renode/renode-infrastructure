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
using System.Collections.Generic;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class CadenceUart : IDoubleWordPeripheral, IUART
    {
        public CadenceUart()
        {
            buffer = new Queue<byte>();
            Reset();
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }

        [field: Transient]
        public event Action<byte> CharReceived;
    
        public void WriteChar(byte value)
        {
            lock(buffer)
            {
                buffer.Enqueue(value);
                if((InterruptEnable & 0x01) != 0)
                {
                    InterruptStatus |= 0x01;
                    IRQ.Set();
                }
            }
        }
    
        public void WriteDoubleWord(long offset, uint value)
        {   
            switch ( (Offset)offset)
            {
            case Offset.Control:
                ControlRegister = (uint)(value & 0xFCu);
                return;
                
            case Offset.Mode:
                ModeRegister = (uint)(value & 0xFFFu);
                return;
                
            case Offset.InterruptEnable:
                InterruptEnable |= value;
                return;
                
            case Offset.InterruptDisable:
                InterruptEnable &= ~(value);
                return;
                
            case Offset.InterruptStatus:
                InterruptStatus &= ~(value);
                lock(buffer)
                {
                    if(buffer.Count != 0)
                    {   
                        InterruptStatus |= 0x01;
                    }
                }
                if(InterruptStatus == 0)
                {
                    IRQ.Unset();
                }
                return;
                
            case Offset.BaudRateGen:
                baudRateGen = value & 0xFFFF;
                return;
                
            case Offset.RecvTimeOut:
                return;
                
            case Offset.RecvFIFOTrigger:
                return;
                
            case Offset.TxRxFIFO:
                var handler = CharReceived;
                if(handler != null)
                {
                    handler((byte)(value & 0xFF));
                }
                return;
                
            case Offset.BaudRateDivider:
                if (value < 4)
                {
                    value = 0; // according to manual values 0-3 are ignored (i understand they are treated as zero).
                }
                baudRateDiv = value & 0xF;
                return;
                
            default:
                this.LogUnhandledWrite(offset, value);   
                return;
            }   
        }
        
        public uint ReadDoubleWord(long offset)
        {
            switch( (Offset)offset)
            {
            case Offset.Control:
                return ControlRegister;
                
            case Offset.Mode:
                return ModeRegister;
                
            case Offset.InterruptMask:
                return InterruptEnable;
                
            case Offset.InterruptStatus: //int status
                return InterruptStatus;
                
            case Offset.Status:
                    // status register
		        uint res = 0;
                lock(buffer)
                {
                if (buffer.Count == 0)
                    {
                    res |= 0x2; // UART_CSR_REMPTY
                    }
                }
		        res |= 0x8; // UART_CSR_TEMPTY
		        return res;
                
            case Offset.TxRxFIFO: // data register
	    	    lock(buffer) {
			        if (buffer.Count == 0) return 0;
			        var waitingChar = buffer.Dequeue();
                    return waitingChar;
                }
            default:
                this.LogUnhandledRead(offset);
                return 0x00;
                
            }
            
        }
        
        public void Reset()
        {
            buffer.Clear();
        }

    	private readonly Queue<byte> buffer;
      
        private uint InterruptEnable = 0x08;    
        private uint InterruptStatus = 0x14;
        private uint ControlRegister = 0x00;
        private uint ModeRegister = 0x04;
        private uint baudRateGen = 0x28B;
        private uint baudRateDiv = 0xF;

        [Flags]
        private enum Mode : uint
        {
            ClockSelect = 1,
            Parity   = (1 << 5) | (1 << 4) | (1 << 3),
            StopBits = (1 << 6) | (1 << 7)
        }

        private enum Offset:uint
        {
            Control = 0x00,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0C,
            InterruptMask = 0x10,
            InterruptStatus = 0x14,
            BaudRateGen = 0x18,
            RecvTimeOut = 0x1C,
            RecvFIFOTrigger = 0x20,
            Status = 0x2C,
            TxRxFIFO = 0x30,
            BaudRateDivider = 0x34,
        }

        public Bits StopBits
        {
            get
            {
                var bits = (ModeRegister & (uint)Mode.StopBits) >> 3;
                switch(bits)
                {
                case 0:
                    return Bits.One;
                case 1:
                    return Bits.OneAndAHalf;
                case 2:
                    return Bits.Two;
                default:
                    throw new ArgumentException("Wrong stop bits register value");
                }

            }
        }

        public Parity ParityBit
        {
            get
            {
                var parity = (ModeRegister & (uint)Mode.Parity) >> 6;
                switch(parity)
                {
                case 0:
                    return Parity.Even;
                case 1:
                    return Parity.Odd;
                case 2:
                    return Parity.Forced0;
                case 3:
                    return Parity.Forced1;
                default:
                    return Parity.None;
                }
            }
        }

        public uint BaudRate
        {
            get
            {
                var divisor = (((ModeRegister & (uint)Mode.ClockSelect) == 0 ? 1 : 8) * (baudRateGen == 0 ? 1 : baudRateGen) * (baudRateDiv + 1));
                return divisor == 0 ? 0 : (uint)(SystemClockFrequency / divisor);
            }
        }

        public const int SystemClockFrequency = 0;
    }
}

