//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Backends.Terminals
{
    public abstract class BackendTerminal : IExternal, IConnectable<IUART>
    {
        public BackendTerminal()
        {
            buffer = new Queue();
        }

        public virtual event Action<byte> CharReceived;

        public abstract void WriteChar(byte value);

        public virtual void BufferStateChanged(BufferState state)
        {
            lock(innerLock)
            {
                if(state == BufferState.Full || pendingTimeDomainEvent)
                {
                    return;
                }
                pendingTimeDomainEvent = true;
            }
            HandleExternalTimeDomainEvent<object>(_ => WriteBufferToUART(), null);
        }

        public virtual void AttachTo(IUART uart)
        {
            this.uart = uart;
            this.machine = uart.GetMachine();

            var uartWithBuffer = uart as IUARTWithBufferState;
            if(uartWithBuffer != null)
            {
                CharReceived += EnqueueWriteToUART;
                uartWithBuffer.BufferStateChanged += BufferStateChanged;
            }
            else
            {
                CharReceived += WriteToUART;
            }

            uart.CharReceived += WriteChar;
        }

        public virtual void DetachFrom(IUART uart)
        {
            var uartWithBuffer = uart as IUARTWithBufferState;
            if(uartWithBuffer != null)
            {
                CharReceived -= EnqueueWriteToUART;
                uartWithBuffer.BufferStateChanged -= BufferStateChanged;
            }
            else
            {
                CharReceived -= WriteToUART;
            }

            uart.CharReceived -= WriteChar;

            this.uart = null;
            this.machine = null;
            buffer.Clear();
        }

        protected void CallCharReceived(byte value)
        {
            var charReceived = CharReceived;
            if(charReceived != null)
            {
                charReceived(value);
            }
        }

        private void EnqueueWriteToUART(byte value)
        {
            lock(innerLock)
            {
                buffer.Enqueue(value);
                if(!pendingTimeDomainEvent)
                {
                    pendingTimeDomainEvent = true;
                    HandleExternalTimeDomainEvent<object>(_ => WriteBufferToUART(), null);
                }
            }
        }

        private void WriteBufferToUART()
        {
            lock(innerLock)
            {
                var uartWithBuffer = uart as IUARTWithBufferState;
                while(buffer.Count > 0 && uartWithBuffer.BufferState != BufferState.Full)
                {
                    uart.WriteChar((byte)buffer.Dequeue());
                }
                pendingTimeDomainEvent = false;
            }
        }

        private void WriteToUART(byte value)
        {
            HandleExternalTimeDomainEvent(uart.WriteChar, value);
        }

        private void HandleExternalTimeDomainEvent<T>(Action<T> handler, T handlerValue)
        {
            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }
            machine.HandleTimeDomainEvent(handler, handlerValue, vts);
        }

        private readonly Queue buffer;
        private readonly object innerLock = new object();

        private IUART uart;
        private IMachine machine;
        private bool pendingTimeDomainEvent;
    }
}

