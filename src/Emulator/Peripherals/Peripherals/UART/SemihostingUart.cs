//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public class SemihostingUart : IPeripheral, IUART
    {
        public SemihostingUart()
        {
            Reset();
        }

        public void Reset()
        {
            readFifo = new Queue<byte>(ReceiveFifoSize);    // typed chars are stored here
        }

        public void WriteChar(byte value) // char is typed
        {
            lock(UartLock)
            {
                readFifo.Enqueue(value);
            }
        }

        public byte SemihostingReadByte()
        {
            lock(UartLock)
            {
                return readFifo.Count > 0 ? readFifo.Dequeue() : (byte)0;
            }
        }

        public int SemihostingReadBytes(byte[] buffer, int offset, int count)
        {
            ReadWriteCheck(buffer, offset, count);

            lock(UartLock)
            {
                count = readFifo.Count < count ? readFifo.Count : count;
                for(var i = offset; i < count + offset; i++)
                {
                    buffer[i] = readFifo.Dequeue();
                }
            }
            return count;
        }

        public void SemihostingWriteByte(byte b)
        {
            lock(UartLock)
            {
                OnCharReceived(b);
            }
        }

        public void SemihostingWriteBytes(byte[] buffer, int offset, int count)
        {
            ReadWriteCheck(buffer, offset, count);

            lock(UartLock)
            {
                for(var i = offset; i < count + offset; i++)
                {
                    OnCharReceived(buffer[i]);
                }
            }
        }

        public void SemihostingWriteString(string s)
        {
            lock(UartLock)
            {
                for(var i = 0; i < s.Length; i++)
                {
                    OnCharReceived(Convert.ToByte(s[i]));
                }
            }
        }

        public Bits StopBits
        {
            get
            {
                return 0;
            }
        }

        public Parity ParityBit
        {
            get
            {
                return Parity.None;
            }
        }

        public uint BaudRate
        {
            get
            {
                return 9600;
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        private void OnCharReceived(byte b)
        {
            var handler = CharReceived;
            if(handler != null)
            {
                handler(b);
            }
        }

        private void ReadWriteCheck(byte[] buffer, int offset, int count)
        {
            try
            {
                if(buffer == null || checked(offset + count) > buffer.Length)
                {
                    throw new ArgumentException();
                }
            }
            catch(OverflowException e)
            {
                throw new ArgumentException(e.Message);
            }

            if(offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if(count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        private Queue<byte> readFifo;

        private readonly object UartLock = new object();
        private const int ReceiveFifoSize = 16;
    }
}