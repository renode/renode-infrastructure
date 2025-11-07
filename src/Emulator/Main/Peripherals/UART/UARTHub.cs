//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;

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
        public UARTHub(bool loopback) : base(loopback) { }
    }

    public class UARTHubBase<I> : IExternal, IHasOwnLife, IConnectable<I>
        where I : class, IUART
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

                Action<byte> d = null;
                if(uart is IDelayableUART duart)
                {
                    d = x =>
                    {
                        var now = TimeDomainsManager.Instance.VirtualTimeStamp;
                        var when = new TimeStamp(now.TimeElapsed + duart.CharacterTransmissionDelay, now.Domain);
                        HandleCharReceived(x, when, uart);
                    };
                }
                else
                {
                    d = x => HandleCharReceived(x, TimeDomainsManager.Instance.VirtualTimeStamp, uart);
                }
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

        protected bool started;
        protected readonly bool shouldLoopback;
        protected readonly Dictionary<I, Action<byte>> uarts;
        protected readonly object locker;

        private void HandleCharReceived(byte obj, TimeStamp when, I sender)
        {
            if(!started)
            {
                return;
            }

            lock(locker)
            {
                foreach(var recipient in uarts.Where(x => shouldLoopback || x.Key != sender).Select(x => x.Key))
                {
                    recipient.GetMachine().HandleTimeDomainEvent(recipient.WriteChar, obj, when);
                }
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
    }
}