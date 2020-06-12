//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class PL011 : IDoubleWordPeripheral, IUART, IKnownSize
    {
        public PL011(int size = 0x1000)
        {
            this.size = size;
            IRQ = new GPIO();
            Reset();
            idHelper = new PrimeCellIDHelper(size, new byte[] { 0x11, 0x10, 0x14, 0x00, 0x0D, 0xF0, 0x05, 0xB1 }, this);
        }

        public long Size
        {
            get
            {
                return size;
            }
        }

        public void WriteChar(byte value)
        {
            lock(UartLock)
            {
                readFifo.Enqueue(value);
                flags &= ~(uint)Flags.ReceiveFifoEmpty;
                if(readFifo.Count >= receiveFifoSize)
                {
                    flags |= (uint)Flags.ReceiveFifoFull;
                }
                if(readFifo.Count >= readFifoTriggerLevel)
                {
                    rawInterruptStatus |= (uint)RawInterruptStatus.ReceiveInterruptStatus;
                    CallInterrupt();
                }
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public GPIO IRQ { get; private set; }

        public Bits StopBits { get { return (lineControl & (uint)LineControl.TwoStopBitsSelect) == 0 ? Bits.One : Bits.Two; } }

        public Parity ParityBit
        {
            get
            {
                var pen = lineControl & (uint)LineControl.ParityEnable;
                if (pen == 0)
                {
                    return Parity.None;
                }
                else
                {
                    var eps = lineControl & (uint)LineControl.EvenParitySelect;
                    var sps = lineControl & (uint)LineControl.StickParitySelect;

                    if (eps == 0)
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

        public uint BaudRate
        {
            get
            {
                var divisor = (16 * ((integerBaudRate & 0xFFFF) + ((fractionalBaudRate & 0x1F) / 64)));
                return (divisor > 0) ? (UARTClockFrequency / divisor) : 0;
            }
        }

        public void Reset()
        {

            lock(UartLock)
            {
                control = flags = integerBaudRate = fractionalBaudRate = irdaLowPowerCounter = interruptMask = 0;
                flags |= (uint)Flags.TransmitFifoEmpty;
                flags |= (uint)Flags.ReceiveFifoEmpty;
                control |= (uint)Control.TransmitEnable;
                control |= (uint)Control.ReceiveEnable;
                readFifo = new Queue<uint>(receiveFifoSize);    // typed chars are stored here
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(UartLock)
            {
                uint retVal;
                switch((Register)offset)
                {
                case Register.Data:
                    if(readFifo.Count == 0)
                    {
                        flags |= (uint)Flags.ReceiveFifoEmpty;
                        retVal = 0;
                    }
                    else
                    {
                        retVal = readFifo.Dequeue();
                        flags &= ~(uint)Flags.ReceiveFifoFull;
                        if(readFifo.Count == 0)
                        {
                            flags |= (uint)Flags.ReceiveFifoEmpty;
                        }
                        else
                        {
                            flags &= ~(uint)Flags.ReceiveFifoEmpty;
                        }
                    }

                    if(readFifo.Count < readFifoTriggerLevel)
                        rawInterruptStatus &= ~(uint)RawInterruptStatus.ReceiveInterruptStatus;

                    CallInterrupt();
                    return retVal;
                case Register.ReceiveStatus:
                    return 0;
                case Register.Flag:
                    return flags;
                case Register.IrDALowPowerCounter:
                    return irdaLowPowerCounter;
                case Register.IntegerBaudRate:
                    return integerBaudRate;
                case Register.FractionalBaudRate:
                    return fractionalBaudRate;
                case Register.LineControl:
                    return lineControl;
                case Register.Control:
                    return control;
                case Register.InterruptFIFOLevel:
                    return interruptFIFOLevel;
                case Register.InterruptMask:
                    return interruptMask;
                case Register.RawInterruptStatus:
                    return rawInterruptStatus;
                case Register.MaskedInterruptStatus:
                    return MaskedInterruptStatus;
                case Register.DMAControl:
                    return dmaControl;
                case Register.InterruptClear:
                    return 0;
                case Register.UARTPeriphID0:
                    return 0x11;
                case Register.UARTPeriphID1:
                    return 0x10;
                case Register.UARTPeriphID2:
                    return 0x14;
                case Register.UARTPeriphID3:
                    return 0x00;
                case Register.UARTPCellID0:
                    return 0x0D;
                case Register.UARTPCellID1:
                    return 0xF0;
                case Register.UARTPCellID2:
                    return 0x05;
                case Register.UARTPCellID3:
                    return 0xB1;
                default:
                    return idHelper.Read(offset);
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(UartLock)
            {
                switch((Register)offset)
                {
                case Register.Data:
                    if(!UartEnabled || !TransmitEnabled)
                    {
                        break;
                    }
                    if (LoopbackTesting)
                    {
                        rawInterruptStatus |= (uint)RawInterruptStatus.TransmitInterruptStatus;
                        CallInterrupt();
                        break;
                    }
                    OnCharReceived((byte)value);
                    rawInterruptStatus |= (uint)RawInterruptStatus.TransmitInterruptStatus;
                    CallInterrupt();

                    break;
                case Register.ReceiveStatus:
                    break;
                case Register.Flag:
                    break;
                case Register.IrDALowPowerCounter:
                    irdaLowPowerCounter = value;
                    break;
                case Register.IntegerBaudRate:
                    integerBaudRate = value;
                    break;
                case Register.FractionalBaudRate:
                    fractionalBaudRate = value;
                    break;
                case Register.LineControl:
                    lineControl = value;
                    break;
                case Register.Control:
                    control = value;
                    break;
                case Register.InterruptFIFOLevel:
                    interruptFIFOLevel = value;
                    break;
                case Register.InterruptMask:
                    interruptMask = value;
                    CallInterrupt();
                    break;
                case Register.InterruptClear:
                    rawInterruptStatus &= ~value;
                    CallInterrupt();
                    break;
                case Register.DMAControl:
                    dmaControl = value;
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
        }

        private void OnCharReceived(byte b)
        {
            var handler = CharReceived;
            if(handler != null)
            {
                handler(b);
            }

        }

        private void CallInterrupt()
        {
            IRQ.Set(MaskedInterruptStatus != 0);
        }

        private uint MaskedInterruptStatus
        {
            get { return rawInterruptStatus & interruptMask; }
        }

        private bool ReadFifoEmpty
        {
            get { return  (flags & (uint)1u << 4) != 0; }
        }

        private bool ReadFifoFull
        {
            get { return  (flags & (uint)1u << 6) != 0; }
        }

        private bool WriteFifoEmpty
        {
            get { return  (flags & (uint)1u << 7) != 0; }
        }

        private bool WriteFifoFull
        {
            get { return  (flags & (uint)1u << 5) != 0; }
        }

        private bool ReceiveInterrupt
        {
            get { return (rawInterruptStatus & (uint)1u << 4) != 0; }
        }

        private bool TransmitInterrupt
        {
            get { return (rawInterruptStatus & (uint)1u << 5) != 0; }
        }

        private bool TransmitEnabled
        {
            get { return (control & (uint)1u << 8) != 0; }
        }

        private bool ReceiveEnabled
        {
            get { return (control & (uint)1u << 9) != 0; }
        }

        private bool UartEnabled
        {
            get { return (control & (uint)1u) != 0; }
        }

        private bool LoopbackTesting
        {
            get { return (control & (uint)1u << 7) != 0; }
        }

        private object UartLock = new object();
        private const uint UARTClockFrequency = 24000000;
        private Queue<uint> readFifo;
        private const int transmitFifoSize = 16;
        private const int receiveFifoSize = 16;
        private const int readFifoTriggerLevel = 1;
        private uint flags;
        private uint lineControl;
        private uint control;
        private uint dmaControl;
        private uint interruptMask;
        private uint rawInterruptStatus;
        private uint irdaLowPowerCounter;
        private uint integerBaudRate;
        private uint fractionalBaudRate;
        private uint interruptFIFOLevel;

        private readonly int size;
        private readonly PrimeCellIDHelper idHelper;

        private const uint UartEnable = 0x0001;
        private const uint LoopbackEnable = 0x0080;
        private const uint TxEnable = 0x0100;

        private enum Register
        {
            Data                            = 0x000,
            ReceiveStatus                   = 0x004, //aka ErrorClear
            Flag                            = 0x018,
            IrDALowPowerCounter             = 0x020,
            IntegerBaudRate                 = 0x024,
            FractionalBaudRate              = 0x028,
            LineControl                     = 0x02c,
            Control                         = 0x030,
            InterruptFIFOLevel              = 0x034,
            InterruptMask                   = 0x038,
            RawInterruptStatus              = 0x03c,
            MaskedInterruptStatus           = 0x040,
            InterruptClear                  = 0x044,
            DMAControl                      = 0x048,

            UARTPeriphID0                   = 0xFE0,
            UARTPeriphID1                   = 0xFE4,
            UARTPeriphID2                   = 0xFE8,
            UARTPeriphID3                   = 0xFEC,
            UARTPCellID0                    = 0xFF0,
            UARTPCellID1                    = 0xFF4,
            UARTPCellID2                    = 0xFF8,
            UARTPCellID3                    = 0xFFC
        }

        [Flags]
        private enum LineControl : uint
        {
            TwoStopBitsSelect = 1u << 3,
            ParityEnable = 1u << 1,
            EvenParitySelect = 1u << 2,
            StickParitySelect = 1u << 7
        }
        [Flags]
        private enum Flags : uint
        {
            TransmitFifoEmpty = 1u << 7,
            ReceiveFifoEmpty = 1u << 4,
            ReceiveFifoFull = 1u << 6
        }
        [Flags]
        private enum Control : uint
        {
            TransmitEnable = 1u << 8,
            ReceiveEnable = 1u << 9
        }
        [Flags]
        private enum RawInterruptStatus : uint
        {
            ReceiveInterruptStatus = 1u << 4,
            TransmitInterruptStatus = 1u << 5
        }

    }
}

