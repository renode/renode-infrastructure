//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Tools.Network
{
    public static class CANHubExtensions
    {
        public static void CreateCANHub(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new CANHub(), name);
        }
    }

    public sealed class CANHub : SynchronizedExternalBase, IExternal, IHasOwnLife, IConnectable<ICAN>
    {
        public CANHub()
        {
            sync = new object();
            attached = new List<ICAN>();
            handlers = new Dictionary<ICAN, Action<int, byte[]>>();
        }

        public void AttachTo(ICAN iface)
        {
            lock(sync)
            {
                attached.Add(iface);
                handlers.Add(iface, (id, data) => Transmit(iface, id, data));
                iface.FrameSent += handlers[iface];
            }
        }

        public void DetachFrom(ICAN iface)
        {
            lock(sync)
            {
                attached.Remove(iface);
                iface.FrameSent -= handlers[iface];
                handlers.Remove(iface);
            }
        }


        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            lock(sync)
            {
                started = false;
            }
        }

        public void Resume()
        {
            lock(sync)
            {
                started = true;
            }
        }

        private void Transmit(ICAN sender, int id, byte[] data)
        {
            ExecuteOnNearestSync(() =>
            {
                lock(sync)
                {
                    if(!started)
                    {
                        return;
                    }
                    foreach(var iface in attached)
                    {
                        if(iface == sender)
                        {
                            continue;
                        }
                        iface.OnFrameReceived(id, data);
                    }
                }
            });
        }

        private readonly List<ICAN> attached;
        private readonly Dictionary<ICAN, Action<int, byte[]>> handlers;
        private bool started;
        private object sync;
    }
}

