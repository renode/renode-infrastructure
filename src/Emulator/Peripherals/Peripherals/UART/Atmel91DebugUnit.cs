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
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public class Atmel91DebugUnit : IDoubleWordPeripheral, IUART, IKnownSize
    {
        public Atmel91DebugUnit()
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
                    this.Log(LogLevel.Noisy, "GPIO IRQ set to {0} due to new character in a buffer", value);
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

            case Offset.BaudRateGen:
                BaudRateGenerator = value & 0xFFFF;
                return;
                
            case Offset.TransmitHoldingRegister:
                var handler = CharReceived;
                if(handler != null)
                {
                    handler((byte)(value & 0xFF));
                }
                if (BitHelper.IsBitSet(InterruptEnable, 4))
                {
                    BitHelper.SetBit(ref InterruptStatus, 4, true);
                    IRQ.Set();
                }
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

            case Offset.Status:
                    // status register
		        uint res = 0;
                lock(buffer)
                {
                if (buffer.Count != 0)
                    {
                    res |= 0x1; // RXRDY
                    }
		    res |= (1<<9); // TXEMPTY
    		        res |= (1<<11); // TXBUFE
		    res |= (1<<1); // TXRDY
                    if (buffer.Count == 0)
                    {
                        InterruptStatus = 0;
                        IRQ.Unset();
                    }
                }
		        return res;
                
            case Offset.ReceiveHoldingRegister: // data register
	    	    lock(buffer) {
			        if (buffer.Count == 0) {
                        return 0;
                    }
			        var waitingChar = buffer.Dequeue();
                    return waitingChar;
                }
            case Offset.PDCTransferStatusRegister:  
                return 0u;
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
        private uint BaudRateGenerator = 0x0;
     
        private enum Offset:uint
        {
            Control = 0x00,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0C,
            InterruptMask = 0x10,
            Status = 0x14,
            ReceiveHoldingRegister = 0x18,
            TransmitHoldingRegister = 0x1C,
            BaudRateGen = 0x20,
            PDCTransferStatusRegister = 0x124,
        }
        
        public long Size => 0x200;

        public Bits StopBits { get { return Bits.One; } }

        public Parity ParityBit
        {
            get
            {
                var parity = ((ModeRegister >> 9) & 7u);
                switch (parity)
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
                return BaudRateGenerator == 0 ? 0 : (MasterClockFrequency / (BaudRateGenerator == 1 ? 1 : 16 * BaudRateGenerator));
            }
        }

        public const int MasterClockFrequency = 0;
    }
}

