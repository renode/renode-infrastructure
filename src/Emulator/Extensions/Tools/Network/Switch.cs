//
// Copyright (c) 2010-2017 Antmicro
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

    public class Switch : SynchronizedExternalBase, IExternal, IHasOwnLife, IConnectable<IMACInterface>, INetworkLogSwitch
    {
        public void AttachTo(IMACInterface iface)
        {
            lock(innerLock)
            {
                var ifaceDescriptor = new InterfaceDescriptor
                {
                    Interface = iface,
                    Delegate = f => ForwardToReceiver(f, iface)
                };

                iface.FrameReady += ifaceDescriptor.Delegate;
                ifaces.Add(ifaceDescriptor);
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

        public event Action<IExternal, IMACInterface, IMACInterface, byte[]> FrameTransmitted;
        public event Action<IExternal, IMACInterface, byte[]> FrameProcessed;

        private void ForwardToReceiver(EthernetFrame frame, IMACInterface sender)
        {
            var frameTransmitted = FrameTransmitted;
            var frameProcessed = FrameProcessed;

            if(!frame.DestinationMAC.HasValue)
            {
                this.Log(LogLevel.Warning, "Destination MAC not set, the frame has unsupported format.");
                return;
            }

            FrameProcessed?.Invoke(this, sender, frame.Bytes.ToArray());

            ExecuteOnNearestSync(() =>
            {
                if(!started)
                {
                    return;
                }
                lock(innerLock)
                {
                    IMACInterface destIface;
                    var interestingIfaces = macMapping.TryGetValue(frame.DestinationMAC.Value, out destIface)
                        ? ifaces.Where(x => (x.PromiscuousMode && x.Interface != sender) || x.Interface == destIface)
                        : ifaces.Where(x => x.Interface != sender);

                    foreach(var iface in interestingIfaces)
                    {
                        iface.Interface.ReceiveFrame(frame);

                        FrameTransmitted?.Invoke(this, sender, iface.Interface, frame.Bytes.ToArray());
                    }
                }
            });

            // at the same we will potentially add current MAC address assigned to the source
            if(!frame.SourceMAC.HasValue)
            {
                this.Log(LogLevel.Warning, "Source MAC not set, cannot update switch cache.");
                return;
            }

            lock(innerLock)
            {
                macMapping[frame.SourceMAC.Value] = sender;
            }
        }

        private bool started;

        private readonly object innerLock = new object();
        private readonly HashSet<InterfaceDescriptor> ifaces = new HashSet<InterfaceDescriptor>();
        private readonly Dictionary<MACAddress, IMACInterface> macMapping = new Dictionary<MACAddress, IMACInterface>();

        private class InterfaceDescriptor
        {
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

