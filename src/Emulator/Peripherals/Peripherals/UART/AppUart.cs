//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.UART
{
    public class AppUart : IDoubleWordPeripheral, IUART
    {
        public AppUart()
        {
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            uint val;
            switch((Register)offset)
            {
            case Register.RecvCtrl:
                val = recvControl;
                break;
            case Register.TransCtrl:
                val = transControl;
                break;
            case Register.Control:
                val = control;
                break;
            case Register.LineCtrl:
                val = lineControl;
                break;
            case Register.InterruptReg:
                val = 0;//state.Interrupt;
                break;
            case Register.StatusReg:
                val = 0x08000000;//state.Status;
                break;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
            return val;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.RecvCtrl:
                recvControl = value;
                break;
            case Register.RecvCtrlSet:
                recvControl |= value;
                break;
            case Register.RecvCtrlClr:
                recvControl &= ~value;
                break;
            case Register.Control:
                control = value;
                break;
            case Register.ControlSet:
                control |= value;
                break;
            case Register.ControlClr:
                control &= ~value;
                break;
            case Register.LineCtrl:
                lineControl = value;
                break;
            case Register.LineCtrlSet:
                lineControl |= value;
                break;
            case Register.LineCtrlClr:
                lineControl &= ~value;
                break;
            case Register.InterruptReg:
                interrupt = value;
                break;
            case Register.InterruptRegSet:
                interrupt |= value;
                break;
            case Register.InterruptRegClr:
                interrupt &= ~value;
                break;
            case Register.StatusReg:
                status = value;
                break;
            case Register.StatusRegSet:
                status |= value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            recvControl = 1 << 31 & 1 << 30 & 3 << 16;
            transControl = 0; //TODO: verify if buggy
            control = 2 << 20 & 2 << 16 & 1 << 9 & 1 << 8;
            Status = 1 << 31 & 1 << 30 & 1 << 27 & 1 << 24 & 0xF << 20;
        }

        public event Action<byte> CharReceived
        {
            add {}
            remove {}
        }

        public void WriteChar(byte value)
        {
            throw new NotImplementedException();
        }

        private uint recvControl;
        private uint control;
        private uint lineControl;
        private uint interrupt;
        private uint status;
        private uint transControl;

        private uint Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value & (0xF << 20 & 1 << 18 & 1 << 17 & 1 << 16);
            }
        }

        private enum Register
        {
            RecvCtrl = 0x00,
            RecvCtrlSet = 0x04,
            RecvCtrlClr = 0x08,
            RecvCtrlTog = 0x0C,
            TransCtrl = 0x10,
            TransCtrlSet = 0x14,
            TransCtrlClr = 0x18,
            TransCtrlTog = 0x1C,
            Control = 0x20,
            ControlSet = 0x24,
            ControlClr = 0x28,
            ControlTog = 0x2C,
            LineCtrl = 0x30,
            LineCtrlSet = 0x34,
            LineCtrlClr = 0x38,
            LineCtrlTog = 0x3C,
            InterruptReg = 0x50,
            InterruptRegSet = 0x54,
            InterruptRegClr = 0x58,
            InterruptRegTohg = 0x5C,
            StatusReg = 0x70,
            StatusRegSet = 0x74,
            StatusRegClr = 0x78,
            StatusRegTog = 0x7C
        }

        public uint BaudRate
        {
            get
            {
                var divFrac = ((lineControl >> 8) & 0x3F);
                var divInt = (lineControl >> 16);
                var divisor = ((divInt << 6) + divFrac);
                return divisor == 0 ? 0 : (UARTClockFrequency * 32) / divisor;
            }
        }

        public Bits StopBits { get { return (lineControl & 8u) == 0 ? Bits.One : Bits.Two; } }

        public Parity ParityBit
        {
            get
            {
                var pen = lineControl & (1u << 1);
                if(pen == 0)
                {
                    return Parity.None;
                }
                else
                {
                    var eps = lineControl & (1u << 2);
                    var sps = lineControl & (1u << 7);

                    if(eps == 0)
                    {
                        return sps == 0 ? Parity.Odd : Parity.Forced1;
                    }
                    else
                    {
                        return sps == 0 ? Parity.Even : Parity.Forced0;
                    }
                }
            }
        }

        private const uint UARTClockFrequency = 24000000;
        // 24 Mhz
    }
}

