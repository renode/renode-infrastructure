//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Tools.Network
{
    public static class CANHubExtensions
    {
        public static void CreateCANHub(this Emulation emulation, string name, bool loopback = false, bool useNetworkByteOrderForLogging = true)
        {
            emulation.ExternalsManager.AddExternal(new CANHub(loopback, useNetworkByteOrderForLogging), name);
        }
    }

    public sealed class CANHub : IExternal, IHasOwnLife, IConnectable<ICAN>, INetworkLog<ICAN>
    {
        public CANHub(bool loopback = false, bool useNetworkByteOrderForLogging = true)
        {
            sync = new object();
            attached = new List<ICAN>();
            handlers = new Dictionary<ICAN, Action<CANMessageFrame>>();
            this.loopback = loopback;
            UseNetworkByteOrderForLogging = useNetworkByteOrderForLogging;
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

        public bool IsPaused => !started;

        public event Action<IExternal, ICAN, ICAN, byte[]> FrameTransmitted;
        public event Action<IExternal, ICAN, byte[]> FrameProcessed;
        public event Action<IExternal, ICAN, CANMessageFrame> FrameReceived;

        public bool UseNetworkByteOrderForLogging { get; set; }

        private void Transmit(ICAN sender, CANMessageFrame message)
        {
            lock(sync)
            {
                this.Log(LogLevel.Debug, "Received from {0}: {1}", sender.GetName(), message);
                FrameReceived?.Invoke(this, sender, message);

                byte[] frame = null;
                try
                {
                    frame = message.ToSocketCAN(UseNetworkByteOrderForLogging);
                }
                catch(RecoverableException e)
                {
                    this.Log(LogLevel.Warning, "Failed to create SocketCAN from {0}: {1}", message, e.Message);
                }
                if(frame != null)
                {
                    FrameProcessed?.Invoke(this, sender, frame);
                }

                if(!started)
                {
                    return;
                }
                if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
                {
                    vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
                }
                foreach(var iface in attached.Where(x => (x != sender || loopback)))
                {
                    iface.GetMachine().HandleTimeDomainEvent(iface.OnFrameReceived, message, vts,
                        frame != null ? () => FrameTransmitted?.Invoke(this, sender, iface, frame) : (Action)null);
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

