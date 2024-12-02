//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Peripherals.UART
{
    public abstract class UARTBase : NullRegistrationPointPeripheralContainer<IUART>, IUARTWithBufferState
    {
        protected UARTBase(IMachine machine) : base(machine)
        {
            queue = new Queue<byte>();
            innerLock = new object();
        }

        public virtual void WriteChar(byte value)
        {
            lock(innerLock)
            {
                if(!IsReceiveEnabled)
                {
                    this.Log(LogLevel.Debug, "UART or receive disabled; dropping the character written: '{0}'", (char)value);
                    return;
                }

                queue.Enqueue(value);
                BufferStateChanged?.Invoke(BufferState);
                CharWritten();
            }
        }

        public override void Reset()
        {
            ClearBuffer();
        }

        public override void Register(IUART uart, NullRegistrationPoint registrationPoint)
        {
            base.Register(uart, registrationPoint);
            ConnectEvents();
        }

        public override void Unregister(IUART uart)
        {
            base.Unregister(uart);

            this.CharReceived -= uart.WriteChar;
            uart.CharReceived -= this.WriteChar;
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public event Action<BufferState> BufferStateChanged;
        public BufferState BufferState => Count != 0 ? BufferState.Ready : BufferState.Empty;

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
                    BufferStateChanged?.Invoke(BufferState);
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
                BufferStateChanged?.Invoke(BufferState);
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
        private readonly Queue<byte> queue;

        public abstract Bits StopBits { get; }

        public abstract Parity ParityBit { get; }

        public abstract uint BaudRate { get; }

        protected virtual bool IsReceiveEnabled => true;

        [PostDeserialization]
        private void ConnectEvents()
        {
            if(RegisteredPeripheral != null)
            {
                this.CharReceived += RegisteredPeripheral.WriteChar;
                RegisteredPeripheral.CharReceived += this.WriteChar;
            }
        }
    }
}

