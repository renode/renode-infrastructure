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
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class STM32_UART :  IDoubleWordPeripheral, IUART
    {
        public STM32_UART(Machine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();
            charFifo = new Queue<byte>();
            Reset();
        }

        public GPIO IRQ { get; private set; }

        [field: Transient]
        public event Action<byte> CharReceived;

        public void WriteChar(byte value)
        {
            machine.ReportForeignEvent(value, WriteCharInner);
        }
    
        public void WriteDoubleWord(long address, uint value)
        {   
            lock(charFifo)
            {
                switch((Register)address)
                {
                case Register.Status:
                    transmissionComplete &= (value & (1u << 6)) != 0;
                    return;
                case Register.Data:
                    transmissionComplete = true;
                    var handler = CharReceived;
                    if(handler != null)
                    {
                        handler((byte)(value & 0xFF));
                    }
                    return;
                case Register.Control1:
                    controlRegister1 = value;
                    Update();
                    return;
                case Register.Control2:
                    controlRegister2 = value;
                    return;
                case Register.BaudRate:
                    baudRate = value;
                    return;
                default:
                    this.LogUnhandledWrite(address, value);
                    return;
                }
            }
        }
        
        public uint ReadDoubleWord(long offset)
        {
            lock(charFifo)
            {
                switch((Register)offset)
                {
                    case Register.Status:
                        return charFifo.Count > 0 ? (1u << 5 | 1u << 7 | (transmissionComplete ? 1u : 0u) << 6) : (1 << 7 | (transmissionComplete ? 1u : 0u) << 6);

                    case Register.Data:
                        var returnValue = 0u;
                        if(charFifo.Count > 0)
                        {
                            returnValue = charFifo.Dequeue();
                        }
                        Update();
                        return returnValue;
                    case Register.Control1:
                        return controlRegister1;
                    default:
                        this.LogUnhandledRead(offset);
                        return 0u;
                }
            }
        }
        
        public void Reset()
        {
            // TODO!
            transmissionComplete = true;
        }

        void WriteCharInner(byte value)
        {
            lock(charFifo)
            {
                charFifo.Enqueue(value);
                Update();
            }
        }

        private void Update()
        {
            IRQ.Set((controlRegister1 & (uint)ControlRegister1.TXEInterruptEnable) != 0 || charFifo.Count > 0);
        }

        private readonly Queue<byte> charFifo;
        private readonly Machine machine;

        private bool transmissionComplete;
        private uint controlRegister1;
        private uint controlRegister2;
        private uint baudRate;

        private enum Register : long
        {
            Status   = 0x00,
            Data     = 0x04,
            Control1 = 0x0C,
            Control2 = 0x10,
            BaudRate = 0x08
        }

        [Flags]
        private enum ControlRegister1 : uint
        {
            TXEInterruptEnable  = (1u << 7),
            ParitySelection     = (1u << 9),
            ParityControlEnable = (1u << 10),
            OversamplingMode    = (1u << 15)
        }

        [Flags]
        private enum ControlRegister2 : uint
        {
            StopBitsH = (1u << 13),
            StopBitsL = (1u << 12)
        }

        public Bits StopBits
        {
            get
            {
                var stops = (controlRegister2 & ((uint)ControlRegister2.StopBitsH | (uint)ControlRegister2.StopBitsL) >> 12);
                switch (stops)
                {
                case 0:
                    return Bits.One;
                case 1:
                    return Bits.Half;
                case 2:
                    return Bits.Two;
                case 3:
                    return Bits.OneAndAHalf;
                default:
                    return Bits.None;
                }
            }
        }

        public Parity ParityBit
        {
            get
            {
                if ((controlRegister1 & (uint)ControlRegister1.ParityControlEnable) == 0)
                {
                    return Parity.None;
                }
                else
                {
                    return (controlRegister1 & (uint)ControlRegister1.ParitySelection) == 0 ? Parity.Even : Parity.Odd;
                }
            }
        }

        public uint BaudRate
        {
            get
            {
                var over8 = ((controlRegister1 & (uint)ControlRegister1.OversamplingMode) != 0) ? 1 : 0;
                var mantisa = baudRate >> 4;
                var fraction = baudRate & 0xF;
                var divisor = (8 * (2 - over8) * (mantisa + fraction / 16));
                return divisor == 0 ? 0 : (uint)(UARTClockFrequency / divisor);
            }
        }

        private const uint UARTClockFrequency = 0;
    }
}

