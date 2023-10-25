//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.CAN;

namespace Antmicro.Renode.Tools.Network
{
    public static class CANHubExtensions
    {
        public static void CreateCANHub(this Emulation emulation, string name, bool loopback = false)
        {
            emulation.ExternalsManager.AddExternal(new CANHub(loopback), name);
        }
    }

    public sealed class CANHub : IExternal, IHasOwnLife, IConnectable<ICAN>
    {
        public CANHub(bool loopback = false)
        {
            sync = new object();
            attached = new List<ICAN>();
            handlers = new Dictionary<ICAN, Action<CANMessageFrame>>();
            this.loopback = loopback;
        }

        public void AttachTo(ICAN iface)
        {
            lock(sync)
            {
                if(attached.Contains(iface))
                {
                    throw new RecoverableException("Cannot attach to the provided CAN periperal as it is already registered in this hub.");
                }
                attached.Add(iface);
                handlers.Add(iface, message => Transmit(iface, message));
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

        private void Transmit(ICAN sender, CANMessageFrame message)
        {
            lock(sync)
            {
                if(!started)
                {
                    return;
                }
                foreach(var iface in attached.Where(x => (x != sender || loopback)))
                {
                    iface.GetMachine().HandleTimeDomainEvent(iface.OnFrameReceived, message, TimeDomainsManager.Instance.VirtualTimeStamp);
                }
            }
        }

        private readonly List<ICAN> attached;
        private readonly Dictionary<ICAN, Action<CANMessageFrame>> handlers;
        private bool started;
        private readonly object sync;
        private readonly bool loopback;
    }
}

