//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public class GaislerUART: IDoubleWordPeripheral, IUART, IGaislerAPB
    {
        public GaislerUART()
        {
            buffer = new Queue<byte>();
            IRQ = new GPIO();
            registers = new register();
            Reset();
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public void WriteChar(byte value)
        {
            lock(buffer)
            {
                buffer.Enqueue(value);
                registers.Status &= ~(uint)(0x3fu << 26);
                registers.Status |= (uint)(((buffer.Count) & 0x3f) << 26) | 0x01u;
                //data ready and data count
                if((registers.Control & 1u << 2) != 0)
                {
                    IRQ.Set();
                    IRQ.Unset();
                }
            }
        }

        #region IPeripheral implementation
        public void Reset ()
        {
            lock(buffer)
            {
                this.IRQ.Unset();
                buffer.Clear();
                registers.Status = statusRegisterResetValue;
                registers.Control &= controlRegisterResetMask;
                registers.Scaler = 0;
            }
        }
        #endregion


        public GPIO IRQ { get; private set; }


        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            lock(buffer)
            {
                switch((Offset)offset)
                {
                case Offset.Control:
                    return registers.Control;
                case Offset.Data:
                    var readByte = (byte)0;   
                    if(buffer.Count != 0)
                    {
                        readByte = (buffer.Dequeue());
                        registers.Status &= ~(uint)(0x3fu << 26);
                        registers.Status |= (uint)((((buffer.Count) & 0x3f) << 26)); //data ready and data count    
                        if(buffer.Count == 0)
                        {
                            registers.Status &= ~(0x01u);
                        }
                        else
                        {
                            IRQ.Set();
                            IRQ.Unset();
                        }
                    }
                    
                    return (uint)readByte;
                    
                case Offset.FifoDebug:
                    return 0;
                case Offset.Scaler:
                    return registers.Scaler;
                case Offset.Status:
                    return registers.Status;
                default:
                    this.LogUnhandledRead(offset);
                    return 0u;
                }
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        { 
            lock(buffer)
            {
                this.IRQ.Unset();
                switch((Offset)offset)
                {
                case Offset.Control:
                    registers.Control = (uint)(value & 0x7fff);

                    if((value & (1u << 8)) != 0) // EC (External Clock) bit
                    {
                        throw new ArgumentException("UART external clocking not supported");
                    }
                    return;
                case Offset.Data:
                    var handler = CharReceived;
                    if(handler != null)
                    {
                        handler((byte)value);
                        if((registers.Control & 1u << 3) != 0)
                        {
                            IRQ.Set();
                            IRQ.Unset();
                        }
                    }
                    return;
                case Offset.FifoDebug:
                    return;
                case Offset.Scaler:
                    registers.Scaler = value;
                    return;
                case Offset.Status:
                    this.IRQ.Unset();
                    return;
                default:
                    this.LogUnhandledWrite(offset, value);
                    return;
                }
            }
        }
        #endregion

        private Queue<byte> buffer;
        private register registers;
        
        private class register
        {
            public uint Status;
            public uint Control;
            public uint Scaler;
        }
        
        private enum Offset 
        {
            Data = 0x00,
            Status = 0x04,
            Control = 0x08,
            Scaler = 0x0C,
            FifoDebug = 0x10
        }
        
        private readonly uint vendorID = 0x01;  // Aeroflex Gaisler
        private readonly uint deviceID = 0x00c; // GRLIB APBUART
        private readonly uint statusRegisterResetValue = 0x06;
        private readonly uint controlRegisterResetMask = 0x80007ebc;
        private readonly GaislerAPBPlugAndPlayRecord.SpaceType spaceType = GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;
        
        #region IGaislerAPB implementation
        public uint GetVendorID ()
        {
            return vendorID;
        }

        public uint GetDeviceID ()
        {
            return deviceID;
        }

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType ()
        {
            return spaceType;
        }
        
        public uint GetInterruptNumber()
        {
            var irqEndpoints = IRQ.Endpoints;
            if(irqEndpoints.Count > 0)
            {
                return (uint)irqEndpoints[0].Number;
            }
            return 0;
        }
        #endregion

        public Bits StopBits { get { return Bits.One; } }

        public Parity ParityBit
        {
            get
            {
                var parity = (registers.Control >> 4) & (3u);
                switch (parity)
                {
                case 2:
                    return Parity.Even;
                case 3:
                    return Parity.Odd;
                default:
                    return Parity.None;
                }
            }
        }

        public uint BaudRate
        {
            get 
            {
                var divisor = ((registers.Scaler & 0x7fff) * 8);
                return divisor == 0 ? 0 : SystemClockFrequency / divisor;
            }
        }

        private const int SystemClockFrequency = 0;
    }
}
