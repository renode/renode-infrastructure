//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure;
using System;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.UART
{
    public class EFM32_UART : UARTBase, IDoubleWordPeripheral
    {
        public EFM32_UART(IMachine machine) : base(machine)
        {
            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();
        }

        public GPIO TransmitIRQ{get; private set;}
        public GPIO ReceiveIRQ{get; private set;}

        private uint frameFormatRegister;
        private uint clockControlRegister;
        private bool txInterruptEnabled = false;
        public void WriteDoubleWord(long address, uint value)
        {
            switch((Registers)address)
            {
            case Registers.FrameFormat:
                frameFormatRegister = value;
                break;
            case Registers.Command:
                break;
            case Registers.TxBufferData:
                TransmitCharacter((byte)value);
                // HACK:
                if(txInterruptEnabled)
                    TransmitIRQ.Blink();
                break;
            case Registers.InterruptEnable:
                txInterruptEnabled = (value & 0x3) > 0;
                break;
            case Registers.ClockControl:
                clockControlRegister = value & 0x1FFFE0;
                break;
            default:
                this.LogUnhandledWrite(address, value);
                break;
            }          
        }
        
        public uint ReadDoubleWord(long offset)
        {
            this.NoisyLog("Read {0}", (Registers)offset);
            switch((Registers)offset)
            {
            case Registers.Status:
                uint res = 0x40;
                if(Count > 0)
                {
                    res |= 0x80;
                }
                this.NoisyLog("Returned {0:X}.", res);
                return res;
            case Registers.RxBufferData:
            case Registers.RxBufferDataExtended:
                this.NoisyLog("Read data");
                byte character;
                if(!TryGetCharacter(out character))
                {
                    this.NoisyLog("Failed");
                    return 0;
                }
                this.NoisyLog("Succeeded");
                return character;
            case Registers.TxBufferData:
                return 0;
            case Registers.InterruptFlag:                    
                return 4;
            default:
                this.LogUnhandledRead(offset);
                return 0x00;
            }
        }

        protected override void CharWritten()
        {
            this.NoisyLog("Char written. Count is {0}.", Count);
            ReceiveIRQ.Set();
        }

        protected override void QueueEmptied()
        {
            this.NoisyLog("Queue empty.");
            ReceiveIRQ.Unset();
        }

        [Flags]
        private enum FrameFormat
        {
            StopBitsH = 1 << 13,
            StopBitsL = 1 << 12,
            ParityH   = 1 << 9,
            ParityL   = 1 << 8
        }

        private enum Registers
        {
            FrameFormat          = 0x004, // USARTn_FRAME
            Command              = 0x00C, // USARTn_CMD
            Status               = 0x010, // USARTn_STATUS
            RxBufferDataExtended = 0x018, // USARTn_RXDATAX
            RxBufferData         = 0x01C, // USARTn_RXDATA
            TxBufferData         = 0x034, // USARTn_TXDATA
            InterruptFlag        = 0x040, // USARTn_IF
            InterruptEnable      = 0x04C, // USARTn_IEN
            ClockControl         = 0x014  // USARTn_CLKDIV
        }

        public override Parity ParityBit
        {
            get
            {
                var parity = (frameFormatRegister & (uint)(FrameFormat.ParityH | FrameFormat.ParityL)) >> 8;
                switch (parity)
                {
                case 0:
                    return Parity.None;
                case 2:
                    return Parity.Even;
                case 3:
                    return Parity.Odd;
                default:
                    throw new ArgumentException("Wrong parity bits register value");
                }
            }
        }

        public override Bits StopBits
        {
            get 
            { 
                var bits = (frameFormatRegister & (uint)(FrameFormat.StopBitsH | FrameFormat.StopBitsL)) >> 12;
                switch (bits)
                {
                case 0:
                    return Bits.Half;
                case 1:
                    return Bits.One;
                case 2:
                    return Bits.OneAndAHalf;
                default:
                    return Bits.Two;
                }
            }
        }

        public override uint BaudRate
        {
            get
            {
                // divisor cannot be 0, so there is no need to check it
                return UARTClockFrequency / (2 * (1 + clockControlRegister/256));
            }
        }

        private const uint UARTClockFrequency = 0;
    }
}

