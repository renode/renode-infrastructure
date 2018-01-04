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
using System.Collections.Concurrent;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class UARTHubExtensions 
    {
        public static void CreateUARTHub(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub(), name);
        }
    }

    public sealed class UARTHub : SynchronizedExternalBase, IExternal, IHasOwnLife, IConnectable<IUART>
    {
        public void AttachTo(IUART uart)
        {
            uarts.Add(uart);
            uart.CharReceived += x => HandleCharReceived(x, uart);
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

        private void HandleCharReceived (byte obj, IUART sender)
        {
            if(!started)
            {
                return;
            }
            ExecuteOnNearestSync(() =>
            {
                foreach(var item in uarts.Where(x=> x!= sender))
                {
                    item.WriteChar(obj);
                }
            });
        }

        public void DetachFrom(IUART uart)
        {
            throw new NotImplementedException();
        }

        private bool started;
        private ConcurrentBag<IUART> uarts = new ConcurrentBag<IUART>();
    }
}

