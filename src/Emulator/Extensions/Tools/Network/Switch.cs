//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Network;
using System.Linq;
using Antmicro.Renode.Network;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Tools.Network
{
    public static class SwitchExtensions
    {
        public static void CreateSwitch(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new Switch(), name);
        }
    }

    public class Switch : IExternal, IHasOwnLife, IConnectable<IMACInterface>, INetworkLogSwitch
    {
        public void AttachTo(IMACInterface iface)
        {
            AttachTo(iface, null);
        }

        public void AttachTo(IMACInterface iface, IMachine machine)
        {
            lock(innerLock)
            {
                if(ifaces.Any(x => x.Interface == iface))
                {
                    throw new RecoverableException("Cannot attach to the provided MAC interface as it is already registered in this switch.");
                }

                var ifaceDescriptor = new InterfaceDescriptor
                {
                    Interface = iface,
                    Delegate = f => ForwardToReceiver(f, iface)
                };

                //  this is to handle TAPInterfaces that are not peripherals
                if(iface is IPeripheral peripheralInterface)
                {
                    ifaceDescriptor.Machine = machine ?? peripheralInterface.GetMachine();
                }
                iface.FrameReady += ifaceDescriptor.Delegate;
                ifaces.Add(ifaceDescriptor);
                this.Log(LogLevel.Info, "Interface {0} attached", iface.MAC);
            }
        }

        public void DetachFrom(IMACInterface iface)
        {
            lock(innerLock)
            {
                var descriptor = ifaces.SingleOrDefault(x => x.Interface == iface);
                if(descriptor == null)
                {
                    this.Log(LogLevel.Warning, "Detaching mac interface that is currently not attached: {0}", iface.MAC);
                    return;
                }

                ifaces.Remove(descriptor);
                iface.FrameReady -= descriptor.Delegate;
                foreach(var m in macMapping.Where(x => x.Value == iface).ToArray())
                {
                    macMapping.Remove(m.Key);
                }
                this.Log(LogLevel.Info, "Interface {0} detached", iface.MAC);
            }
        }

        public void EnablePromiscuousMode(IMACInterface iface)
        {
            lock(innerLock)
            {
                var descriptor = ifaces.SingleOrDefault(x => x.Interface == iface);
                if(descriptor == null)
                {
                    throw new RecoverableException("The interface is not registered, you must connect it in order to change promiscuous mode settings");
                }
                descriptor.PromiscuousMode = true;
                this.Log(LogLevel.Info, "Promiscuous mode enabled for interace {0}", iface.MAC);
            }
        }

        public void DisablePromiscuousMode(IMACInterface iface)
        {
            lock(innerLock)
            {
                var descriptor = ifaces.SingleOrDefault(x => x.Interface == iface);
                if(descriptor == null)
                {
                    throw new RecoverableException("The interface is not registered, you must connect it in order to change promiscuous mode settings");
                }
                if(!descriptor.PromiscuousMode)
                {
                    throw new RecoverableException("The interface is not in promiscuous mode");
                }
                descriptor.PromiscuousMode = false;
                this.Log(LogLevel.Info, "Promiscuous mode disabled for interace {0}", iface.MAC);
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

        public bool IsPaused => !started;

        public event Action<IExternal, IMACInterface, IMACInterface, byte[]> FrameTransmitted;
        public event Action<IExternal, IMACInterface, byte[]> FrameProcessed;

        private void ForwardToReceiver(EthernetFrame frame, IMACInterface sender)
        {
            this.Log(LogLevel.Noisy, "Received frame from interface {0}", sender.MAC);

            FrameProcessed?.Invoke(this, sender, frame.Bytes);

            if(!started)
            {
                return;
            }
            lock(innerLock)
            {
                var interestingIfaces = macMapping.TryGetValue(frame.DestinationMAC, out var destIface)
                    ? ifaces.Where(x => (x.PromiscuousMode && x.Interface != sender) || x.Interface == destIface)
                    : ifaces.Where(x => x.Interface != sender);

                if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
                {
                    // it happens when sending from tap interface
                    vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
                }

                foreach(var iface in interestingIfaces)
                {
                    this.Log(LogLevel.Noisy, "Forwarding frame to interface {0}", iface.Interface.MAC);

                    if(iface.Machine == null)
                    {
                        iface.Interface.ReceiveFrame(frame.Clone());
                        continue;
                    }

                    iface.Machine.HandleTimeDomainEvent(iface.Interface.ReceiveFrame, frame.Clone(), vts, () =>
                    {
                        FrameTransmitted?.Invoke(this, sender, iface.Interface, frame.Bytes);
                    });
                }
            }

            // at the same we will potentially add current MAC address assigned to the source
            lock(innerLock)
            {
                macMapping[frame.SourceMAC] = sender;
            }
        }

        private bool started = true;

        private readonly object innerLock = new object();
        private readonly HashSet<InterfaceDescriptor> ifaces = new HashSet<InterfaceDescriptor>();
        private readonly Dictionary<MACAddress, IMACInterface> macMapping = new Dictionary<MACAddress, IMACInterface>();

        private class InterfaceDescriptor
        {
            public IMachine Machine;
            public IMACInterface Interface;
            public bool PromiscuousMode;
            public Action<EthernetFrame> Delegate;

            public override int GetHashCode()
            {
                return Interface.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var objAsInterfaceDescriptor = obj as InterfaceDescriptor;
                return objAsInterfaceDescriptor != null && Interface.Equals(objAsInterfaceDescriptor.Interface);
            }
        }
    }
}

