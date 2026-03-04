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
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public abstract class UARTBaseWithFrameInfo : UARTBase, IUARTWithFrameInfo
    {
        public virtual void WriteChar(byte value, UARTFrame frame)
        {
            WriteCharInner(value, frame);
        }

        protected UARTBaseWithFrameInfo(IMachine machine) : base(machine)
        {
        }

        protected bool TryGetCharacterWithFrame(out byte character, out UARTFrame frame, bool peek = false)
        {
            return TryGetCharacterInner(out character, out frame, peek);
        }
    }

    public abstract class UARTBase : NullRegistrationPointPeripheralContainer<IUART>, IUART
    {
        public virtual void WriteChar(byte value)
        {
            WriteCharInner(value, null);
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

        public abstract Bits StopBits { get; }

        public abstract Parity ParityBit { get; }

        public abstract uint BaudRate { get; }

        [field: Transient]
        public event Action<byte> CharReceived;

        protected UARTBase(IMachine machine) : base(machine)
        {
            queue = new Queue<Tuple<Byte, UARTFrame>>();
            innerLock = new object();
        }

        /// <remark>
        /// when upgrading to C# 8.0, change the type of `frame` to UARTFrame?
        /// </remark>
        protected virtual void WriteCharInner(byte value, UARTFrame frame)
        {
            lock(innerLock)
            {
                if(!IsReceiveEnabled)
                {
                    this.Log(LogLevel.Debug, "UART or receive disabled; dropping the character written: '{0}'", (char)value);
                    return;
                }

                queue.Enqueue(Tuple.Create(value, frame));
                CharWritten();
            }
        }

        protected bool TryGetCharacter(out byte character, bool peek = false)
        {
            return TryGetCharacterInner(out character, out var _, peek);
        }

        protected bool TryGetCharacterInner(out byte character, out UARTFrame frame, bool peek = false)
        {
            lock(innerLock)
            {
                if(queue.Count == 0)
                {
                    character = default(byte);
                    frame = null;
                    return false;
                }
                if(peek)
                {
                    // Unpacking assignments are not avalible on mono
                    var value = queue.Peek();
                    character = value.Item1;
                    frame = value.Item2;
                }
                else
                {
                    var value = queue.Dequeue();
                    character = value.Item1;
                    frame = value.Item2;
                }
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

        protected abstract void CharWritten();

        protected abstract void QueueEmptied();

        protected virtual bool IsReceiveEnabled => true;

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

        [PostDeserialization]
        private void ConnectEvents()
        {
            if(RegisteredPeripheral != null)
            {
                this.CharReceived += RegisteredPeripheral.WriteChar;
                RegisteredPeripheral.CharReceived += this.WriteChar;
            }
        }

        private readonly Queue<Tuple<byte, UARTFrame>> queue;
    }
}
