//
// Copyright (c) 2010-2018 Antmicro
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
        public static void CreateUARTHub(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub(), name);
        }
    }

    public sealed class UARTHub : IExternal, IHasOwnLife, IConnectable<IUART>
    {
        public UARTHub()
        {
            uarts = new Dictionary<IUART, Action<byte>>();
            locker = new object();
        }

        public void AttachTo(IUART uart)
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

        public void DetachFrom(IUART uart)
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

        private void HandleCharReceived(byte obj, IUART sender)
        {
            if(!started)
            {
                return;
            }

            lock(locker)
            {
                foreach(var item in uarts.Where(x => x.Key != sender).Select(x => x.Key))
                {
                    item.GetMachine().HandleTimeDomainEvent(item.WriteChar, obj, TimeDomainsManager.Instance.VirtualTimeStamp);
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

        private bool started;
        private readonly Dictionary<IUART, Action<byte>> uarts;
        private readonly object locker;
    }
}

