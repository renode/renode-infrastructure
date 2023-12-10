//
// Copyright (c) 2010-2023 Antmicro
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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class AxiUartLite : IDoubleWordPeripheral, IUART, IKnownSize
    {
        public AxiUartLite()
        {
            readFifo = new Queue<uint>();
            IRQ = new GPIO();
        }

        public void WriteChar(byte value)
        {
            readFifo.Enqueue(value);
        }

        public void Reset()
        {
            readFifo.Clear();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Register)offset)
            {
            case Register.RxFIFO:
                if(readFifo.Count == 0)
                {
                    this.Log(LogLevel.Warning, "Trying to read from empty fifo.");
                    return 0;
                }
                return readFifo.Dequeue();
            case Register.Status:
                // Tx FIFO Empty | Rx FIFO Valid Data
                return (1u << 2) | (readFifo.Count == 0 ? 0 : 1u);
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.TxFIFO:
                CharReceived?.Invoke((byte)value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;
        
        public GPIO IRQ { get; }

        public long Size { get { return 0x10; } }
        public Bits StopBits { get { return Bits.One; } }
        public Parity ParityBit { get { return Parity.None; } }
        public uint BaudRate { get { return 0; } }

        private readonly Queue<uint> readFifo;

        private enum Register
        {
            RxFIFO = 0x0,
            TxFIFO = 0x4,
            Status = 0x8,
            Control = 0xC
        }
    }
}
