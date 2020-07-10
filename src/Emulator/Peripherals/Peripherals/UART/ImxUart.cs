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
    public class ImxUart : IBytePeripheral, IUART, IKnownSize
    {
        public ImxUart()
        {
            IRQ = new GPIO();
            charFifo = new Queue<byte>();
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public GPIO IRQ { get; private set; }

        [field: Transient]
        public event Action<byte> CharReceived;
    
        public void WriteChar(byte value)
        {
            lock(charFifo)
            {
                charFifo.Enqueue(value);
                UpdateIRQ();
            }
        }
    
        public byte ReadByte(long address) 
        {
            switch((Register)address)
            {
            case Register.FifoParameters:
                return fifoParametersRegister;
            case Register.FifoWatermark:
                return fifoWatermarkRegister;
            case Register.Modem:
                return modemRegister;
            case Register.BaudRateH:
                return baudRateH;
            case Register.Control1:
                return control1Register;
            case Register.BaudRateL:
                return baudRateL;
            case Register.Control4:
                return control4Register;
            case Register.Status1:
                return status1Register;
            case Register.FifoStatus:
                return (byte) ((charFifo.Count > 0) ? (1<<7) : ((1<<7) | (1<<6)));
            case Register.Control2:
                return control2Register;
            case Register.Control3:
                return 0;
            case Register.Data:
                lock(charFifo)
                {
                    byte returnValue = (byte)0;
                    if(charFifo.Count > 0)
                    {
                        returnValue = charFifo.Dequeue();
                    }
                    UpdateIRQ();
                    return returnValue;
                }
            case Register.Control5:
                return control5Register;
            default:
                this.LogUnhandledRead(address);
                return 0;
            }
        }

        public void WriteByte(long address, byte value) 
        {
            switch((Register)address)
            {
            case Register.FifoParameters:
                fifoParametersRegister = value;
                break;
            case Register.FifoWatermark:
                fifoWatermarkRegister = value;
                break;
            case Register.Modem:
                modemRegister = value;
                break;
            case Register.Control1:
                control1Register = value;
                break;
            case Register.Control2:
                control2Register = value;
                //bool rx_enabled = ((value & (1u << 2)) != 0);
                //bool tx_enabled = ((value & (1u << 3)) != 0);
                enableIRQ = /*(rx_enabled || tx_enabled) &&*/ (((value & (1u << 7)) != 0) || ((value & (1u << 5)) != 0)); // TX_IRQ_DMA | RX_IRQ_DMA
                UpdateIRQ();
                break;
            case Register.Control4:
                control4Register = value;
                break;
            case Register.Control5:
                control5Register = value;
                break;
            case Register.Data:
                var handler = CharReceived;
                if(handler != null)
                {
                    handler(value);
                }
                break;
            case Register.BaudRateH:
                baudRateHBuffer = value;
                break;
            case Register.BaudRateL:
                baudRateH = baudRateHBuffer;
                baudRateL = value;
                break;
            case Register.InterruptEnable:
                break;
            default:
                this.LogUnhandledWrite(address, value);
                break;
            }
            UpdateIRQ();
        }

        private void UpdateIRQ()
        {   
            TDRE    = true;
            TC      = true;
            RDRF    = charFifo.Count > 0;
            IDLE    = !RDRF;

            var irqState = ((TDRE && TIE && !TDMAS) ||
                (TC && TCIE) ||
                (IDLE && ILIE) ||
                (RDRF && RIE && !RDMAS));
            IRQ.Set(enableIRQ && irqState);
        }
        
        public void Reset()
        {
            // TODO!
        }

        private bool enableIRQ;

        private byte status1Register = (byte)((1u << 7) | (1u << 6));

        private byte control1Register;
        private byte control2Register;
        private byte control4Register;
        private byte control5Register;

        private byte fifoWatermarkRegister;
        private byte fifoParametersRegister;

        private readonly Queue<byte> charFifo;

        private byte baudRateHBuffer;
        private byte baudRateH;
        private byte baudRateL;
        private byte modemRegister;

        [Flags]
        private enum Control1 : byte
        {
            ParityEnable = 1 << 1,
            ParityType   = 1
        }

        private enum Register : long
        {
            BaudRateH       = 0x00,
            BaudRateL       = 0x01,
            Control1        = 0x02,
            Control2        = 0x03,
            Status1         = 0x04,
            Control3        = 0x06,
            Data            = 0x07,
            Control4        = 0x0A,
            Control5        = 0x0B,
            Modem           = 0x0D,
            FifoParameters  = 0x10,
            FifoStatus      = 0x12,
            FifoWatermark   = 0x13,
            InterruptEnable = 0x19
        }
        #region Bits

        private bool TDMAS { get { return (control5Register & (1u << 7)) != 0; } }
        private bool RDMAS { get { return (control5Register & (1u << 5)) != 0; } }
        private bool TIE   { get { return (control2Register & (1u << 7)) != 0; } }
        private bool RIE   { get { return (control2Register & (1u << 5)) != 0; } }
        private bool TCIE  { get { return (control2Register & (1u << 6)) != 0; } }
        private bool ILIE  { get { return (control2Register & (1u << 4)) != 0; } }

        private bool TDRE
        {
            get { return (status1Register & (1u << 7)) != 0; }
            set
            {
                if (value)
                {
                    status1Register |= (byte)(1u << 7);
                }
            }
        }
        private bool TC
        {
            get { return (status1Register & (1u << 6)) != 0; }
            set
            {
                if (value)
                {
                    status1Register |= (byte)(1u << 6);
                }
            }
        }
        private bool IDLE
        {
            get { return (status1Register & (1u << 4)) != 0; }
            set 
            {
                if (value)
                {
                    status1Register |= (byte)(1u << 4);
                }
                else
                {
                    status1Register &= (byte.MaxValue - (byte)(1u << 4));
                }
            }
        }
        private bool RDRF
        {
            get { return (status1Register & (1u << 5)) != 0; }
            set 
            {
                if (value)
                {
                    status1Register |= (byte)(1u << 5);
                }
                else
                {
                    status1Register &= (byte.MaxValue - (byte)(1u << 5));
                }
            }
        }

        #endregion

        public Bits StopBits
        {
            get
            {
                return Bits.One;
            }
        }
        public Parity ParityBit
        {
            get
            {
                if ((control1Register & (byte)Control1.ParityEnable) == 0)
                {
                    return Parity.None;
                }
                else
                {
                    return ((control1Register & (byte)Control1.ParityType) == 0) ? Parity.Even : Parity.Odd;
                }
            }
        }

        public uint BaudRate
        {
            get
            {
                var divisor = (16 * (((baudRateH & 0x1F) << 8) + baudRateL) + ((control4Register & 0x1F) / 32));
                return divisor == 0 ? 0 : (uint)(SystemClockFrequency / divisor);
            }
        }

        private uint SystemClockFrequency = 0;
    }
}

