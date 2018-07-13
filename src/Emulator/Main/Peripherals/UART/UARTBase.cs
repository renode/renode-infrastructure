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
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public abstract class UARTBase : IUART
    {
        protected UARTBase(Machine machine)
        {
            queue = new Queue<byte>();
            innerLock = new object();
            Machine = machine;
        }

        public virtual void WriteChar(byte value)
        {
            lock(innerLock)
            {
                queue.Enqueue(value);
                CharWritten();
            }
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
            lock(innerLock)
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
            lock(innerLock)
            {
                queue.Clear();
                QueueEmptied();
            }
        }

        protected int Count
        {
            get
            {
                lock(innerLock)
                {
                    return queue.Count;
                }
            }
        }

        protected readonly object innerLock;
        protected readonly Machine Machine;
        private readonly Queue<byte> queue;

        public abstract Bits StopBits { get; }

        public abstract Parity ParityBit { get; }

        public abstract uint BaudRate { get; }
    }
}

