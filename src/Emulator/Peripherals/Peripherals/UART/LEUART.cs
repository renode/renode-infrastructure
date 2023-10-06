//
// Copyright (c) 2010-2023 Antmicro
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
    public class LEUART : IDoubleWordPeripheral, IUART
    {
        public LEUART()
        {
            queueLock = new object();
            IRQ = new GPIO();
            Reset();
        }

        public GPIO IRQ { get; private set; }

        public uint ReadDoubleWord(long offset)
        {
            switch((Register)offset)
            {
            case Register.Status:
                this.NoisyLog("Status register read, returned {0}.", currentStatus);
                return (uint)currentStatus;
            case Register.InterruptFlag:
                this.NoisyLog("Interrupt flag register read, returned {0}.", interruptFlag);
                return (uint)interruptFlag;
            case Register.InterruptEnable:
                return interruptEnable;
            case Register.ReceiveBufferData:
                return HandleReadCharacter();
            case Register.Freeze:
                return 0;
            case Register.SyncBusy:
                return 0;
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.Control:
                controlRegister = value;
                break;
            case Register.TransmitBufferData:
                TransmitCharacter((byte)value);
                UpdateInterrupts();
                break;
            case Register.InterruptClear:
                ClearInterrupt();
                break;
            case Register.InterruptEnable:
                interruptEnable = value;
                UpdateInterrupts();
                break;
            case Register.ClockControl:
                clockControl = value & 0x7FF8;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            currentStatus = Status.TransmitBufferEmpty;
            // Assume that TX buffer is always empty
            interruptFlag = InterruptFlag.TxCompleteInterrutpt | InterruptFlag.TxBufferLevelInterrupt;
            waitingChars = new Queue<byte>();
        }

        public void WriteChar(byte data)
        {
            this.NoisyLog("Char 0x{0:X} written.", data);
            interruptFlag |= InterruptFlag.RxDataAvailable;
            lock(queueLock)
            {
                waitingChars.Enqueue(data);
                currentStatus |= Status.RxDataAvailable;
            }
            UpdateInterrupts();
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        private uint HandleReadCharacter()
        {
            lock(queueLock)
            {
                if(waitingChars.Count == 0)
                {
                    return 0;
                }
                var waitingChar = waitingChars.Dequeue();
                if(waitingChars.Count == 0)
                {
                    currentStatus &= ~Status.RxDataAvailable;
                }
                UpdateInterrupts();
                this.NoisyLog("ReceiveBufferData read, returned {0}", waitingChar);
                return waitingChar;
            }
        }

        private void TransmitCharacter(byte data)
        {
            var charReceived = CharReceived;
            if(charReceived != null)
            {
                charReceived(data);
            }
        }

        private void ClearInterrupt()
        {
            IRQ.Set(false);
            interruptFlag &= ~InterruptFlag.RxDataAvailable;
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(
                ((interruptEnable & (uint)InterruptEnable.RxDataAvailable) != 0 && waitingChars.Count > 0) ||
                (interruptEnable & (uint)InterruptEnable.TxBufferLevelInterrupt) != 0 ||
                (interruptEnable & (uint)InterruptEnable.TxCompleteInterrutpt) != 0
            );
        }

        private uint controlRegister;
        private uint clockControl;
        private uint interruptEnable;

        private Status currentStatus;
        private InterruptFlag interruptFlag;
        private Queue<byte> waitingChars;
        private readonly object queueLock;

        [Flags]
        private enum InterruptFlag
        {
            RxDataAvailable = 1 << 2,
            TxBufferLevelInterrupt = 1 << 1,
            TxCompleteInterrutpt = 1 << 0
        }

        [Flags]
        private enum InterruptEnable
        {
            RxDataAvailable = 1 << 2,
            TxBufferLevelInterrupt = 1 << 1,
            TxCompleteInterrutpt = 1 << 0
        }

        [Flags]
        private enum Status
        {
            TransmitBufferEmpty = 1 << 4,
            RxDataAvailable = 1 << 5
        }

        [Flags]
        private enum Control
        {
            ParityL =  1 << 2,
            ParityH =  1 << 3,
            StopBits = 1 << 4
        }

        private enum Register
        {
            Control            = 0x000,
            Status             = 0x008,
            ReceiveBufferData  = 0x01C,
            TransmitBufferData = 0x028,
            InterruptFlag      = 0x02C,
            InterruptClear     = 0x034,
            InterruptEnable    = 0x038,
            Freeze             = 0x040,
            SyncBusy           = 0x044,
            ClockControl       = 0x00C
        }

        public Bits StopBits
        {
            get
            {
                return (controlRegister & (uint)Control.StopBits) != 0 ? Bits.Two : Bits.One;
            }
        }

        public Parity ParityBit
        {
            get
            {
                var bits = ((controlRegister & (uint)(Control.ParityH | Control.ParityL)) >> 2);
                switch(bits)
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

        public uint BaudRate
        {
            get
            {
                // divisor cannot be 0, so there is no need to check it
                return UARTClockFrequency / (1 + clockControl/256);
            }
        }

        private const uint UARTClockFrequency = 0;
    }
}

