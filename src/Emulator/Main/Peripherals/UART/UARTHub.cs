//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using System.Linq;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Migrant.Hooks;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class UARTHubExtensions
    {
        public static void CreateUARTHub(this Emulation emulation, string name, bool loopback = false)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub(loopback), name);
        }
    }

    public sealed class UARTHub : UARTHubBase<IUART>
    {
        public UARTHub(bool loopback) : base(loopback) {}
    }

    public class UARTHubBase<I> : IExternal, IHasOwnLife, IConnectable<I>
        where I: class, IUART
    {
        public UARTHubBase(bool loopback)
        {
            uarts = new Dictionary<I, Action<byte>>();
            locker = new object();
            shouldLoopback = loopback;
        }

        public virtual void AttachTo(I uart)
        {
            lock(locker)
            {
                if(uarts.ContainsKey(uart))
                {
                    throw new RecoverableException("Cannot attach to the provided UART as it is already registered in this hub.");
                }

                var d = (Action<byte>)(x => HandleCharReceived(x, uart));
                uarts.Add(uart, d);
                uart.CharReceived += d;
            }
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            started = false;
        }

        public void Resume()
        {
            started = true;
        }

        public virtual void DetachFrom(I uart)
        {
            lock(locker)
            {
                if(!uarts.ContainsKey(uart))
                {
                    throw new RecoverableException("Cannot detach from the provided UART as it is not registered in this hub.");
                }

                uart.CharReceived -= uarts[uart];
                uarts.Remove(uart);
            }
        }

        public bool IsPaused => !started;

        struct WriteCharContext {
            public I i;
            public byte b;
        };

        private void HandleCharReceived(byte obj, I sender)
        {
            if(!started)
            {
                return;
            }

            lock(locker)
            {
                foreach(var item in uarts.Where(x => shouldLoopback || x.Key != sender).Select(x => x.Key))
                {
                    WriteCharContext context = new WriteCharContext() { i = item, b = obj };
                    item.GetMachine().HandleTimeDomainEvent(WriteCharCallback, context, TimeDomainsManager.Instance.VirtualTimeStamp);
                }
            }
        }

        //
        // For any UART device that is correctly modeling its RX buffer state
        // and is derived from UARTHub, this callback will queue any byte that
        // would trigger a RX overflow, giving the device a mechanism to dequeue
        // and process the byte later when its buffer state becomes "Ready"
        // again.
        //
        // For all other devices, the byte is sent immediately.
        //
        private void WriteCharCallback(WriteCharContext context) {
            bool isUARTBase = (context.i as UARTBase) != null;
            bool hasBufferState = (context.i as IUARTWithBufferState) != null;
            bool bufferIsFull = hasBufferState
                ? ((context.i as IUARTWithBufferState).BufferState == BufferState.Full)
                : false;
            if (bufferIsFull && isUARTBase)
            {
                (context.i as UARTBase).QueueOverflowByte(context.b);
            }
            else
            {
                context.i.WriteChar(context.b);
            }
        }

        [PostDeserialization]
        private void ReattachUARTsAfterDeserialization()
        {
            lock(locker)
            {
                foreach(var uart in uarts)
                {
                    uart.Key.CharReceived += uart.Value;
                }
            }
        }

        protected bool started;
        protected readonly bool shouldLoopback;
        protected readonly Dictionary<I, Action<byte>> uarts;
        protected readonly object locker;
    }
}

