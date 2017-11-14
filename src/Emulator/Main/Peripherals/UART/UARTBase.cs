//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public abstract class UARTBase : IUART
    {
        protected UARTBase(Machine machine)
        {
            queue = new Queue<byte>();
            Machine = machine;
        }

        public void WriteChar(byte value)
        {
            Machine.ReportForeignEvent(value, WriteCharInner);
        }

        public virtual void Reset()
        {
            ClearBuffer();
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        protected abstract void CharWritten();
        protected abstract void QueueEmptied();

        protected bool TryGetCharacter(out byte character)
        {
            lock(queue)
            {
                if(queue.Count == 0)
                {
                    character = default(byte);
                    return false;
                }
                character = queue.Dequeue();
                if(queue.Count == 0)
                {
                    QueueEmptied();
                }
                return true;
            }
        }

        protected void TransmitCharacter(byte character)
        {
            CharReceived?.Invoke(character);
        }

        protected void ClearBuffer()
        {
            lock(queue)
            {
                queue.Clear();
                QueueEmptied();
            }
        }

        protected int Count
        {
            get
            {
                lock(queue)
                {
                    return queue.Count;
                }
            }
        }

        private void WriteCharInner(byte value)
        {
            lock(queue)
            {
                queue.Enqueue(value);
                CharWritten();
            }
        }

        protected readonly Machine Machine;
        private readonly Queue<byte> queue;

        public abstract Bits StopBits { get; }

        public abstract Parity ParityBit { get; }

        public abstract uint BaudRate { get; }
    }
}

