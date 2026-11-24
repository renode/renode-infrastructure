//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Testing;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Tools.Network
{
    public static class CANHubExtensions
    {
        public static void CreateCANHub(this Emulation emulation, string name, bool loopback = false, bool useNetworkByteOrderForLogging = true)
        {
            emulation.ExternalsManager.AddExternal(new CANHub(loopback, useNetworkByteOrderForLogging), name);
        }
    }

    public sealed class CANHub : IExternal, IConnectable<ICAN>, INetworkLog<ICAN>, IAnalyzable
    {
        public CANHub(bool loopback = false, bool useNetworkByteOrderForLogging = true)
        {
            sync = new object();
            attached = new List<ICAN>();
            descriptors = new Dictionary<ICAN, InterfaceDescriptor>();
            this.loopback = loopback;
            UseNetworkByteOrderForLogging = useNetworkByteOrderForLogging;
        }

        public void AttachTo(ICAN iface)
        {
            lock(sync)
            {
                if(descriptors.ContainsKey(iface))
                {
                    throw new RecoverableException("Cannot attach to the provided CAN periperal as it is already registered in this hub.");
                }

                attached.Add(iface);
                var desc = new InterfaceDescriptor(iface, this);
                descriptors[iface] = desc;
                iface.FrameSent += desc.Transmit;
            }
        }

        public void DetachFrom(ICAN iface)
        {
            lock(sync)
            {
                if(descriptors.TryGetValue(iface, out var desc))
                {
                    iface.FrameSent -= desc.Transmit;
                    attached.Remove(iface);
                    descriptors.Remove(iface);
                }
            }
        }

        public void LoadDBC(string path)
        {
            DbcDecoder = new DBCDecoder(path);
        }

        public string GetSenderName(ICAN sender)
        {
            lock(sync)
            {
                return descriptors.GetValueOrDefault(sender)?.Name ?? sender.GetType().Name;
            }
        }

        public bool UseNetworkByteOrderForLogging { get; set; }

        public DBCDecoder DbcDecoder { get; private set; }

        public event Action<IExternal, ICAN, ICAN, byte[]> FrameTransmitted;

        public event Action<IExternal, ICAN, byte[]> FrameProcessed;

        public event Action<IExternal, ICAN, CANMessageFrame> FrameReceived;

        private void Transmit(ICAN sender, string name, CANMessageFrame message)
        {
            lock(sync)
            {
                this.Log(LogLevel.Debug, "Received from {0}: {1}", name, message);
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

                var vts = TimeDomainsManager.Instance.GetEffectiveVirtualTimeStamp();
                foreach(var iface in attached)
                {
                    if(iface == sender && !loopback)
                    {
                        continue;
                    }

                    var desc = descriptors[iface];

                    if(!desc.IsEmulated)
                    {
                        TransmitToNonEmulatedInterface(iface, message, vts);
                    }
                    else
                    {
                        desc.Machine.HandleTimeDomainEvent(iface.OnFrameReceived, message, vts,
                            frame is not null ? () => FrameTransmitted?.Invoke(this, sender, iface, frame) : (Action)null);
                    }
                }
            }
        }

        private void TransmitToNonEmulatedInterface(ICAN iface, CANMessageFrame message, TimeStamp virtualTimestamp)
        {
            if(EmulationManager.Instance.CurrentEmulation.IsStarted)
            {
                // CANTester does not belong to a machine so the event has to be handled from MasterTimeSource
                EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ExecuteInSyncedState(
                    (_) => iface.OnFrameReceived(message), virtualTimestamp
                );
            }
            else
            {
                iface.OnFrameReceived(message);
            }
        }

        private readonly List<ICAN> attached;
        private readonly Dictionary<ICAN, InterfaceDescriptor> descriptors;
        private readonly object sync;
        private readonly bool loopback;

        private class InterfaceDescriptor
        {
            public InterfaceDescriptor(ICAN iface, CANHub parent)
            {
                Interface = iface;
                this.parent = parent;
                switch(iface)
                {
                case CANTester:
                    Machine = null;
                    Name = nameof(CANTester);
                    break;
                case CANExternalControlBus:
                    Machine = null;
                    Name = nameof(CANExternalControlBus);
                    break;
                default:
                    Machine = iface.GetMachine();
                    Name = iface.GetName();
                    break;
                }
            }

            public void Transmit(CANMessageFrame message)
            {
                parent.Transmit(Interface, Name, message);
            }

            public ICAN Interface { get; }

            public IMachine Machine { get; }

            public string Name { get; }

            public bool IsEmulated => Machine is not null;

            private readonly CANHub parent;
        }
    }
}
