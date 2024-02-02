//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Network
{
    public class BasicNetwork<TData, TAddress> : IExternal, IConnectable<IBasicNetworkNode<TData, TAddress>>
    {
        public BasicNetwork(string name)
        {
            this.sync = new object();
            this.name = name;
            nodes = new List<IBasicNetworkNode<TData, TAddress>>();
        }

        public void AttachTo(IBasicNetworkNode<TData, TAddress> node)
        {
            lock(sync)
            {
                if(nodes.Contains(node))
                {
                    throw new RecoverableException($"Provided node is already registered in network '{name}'");
                }

                node.TrySendData += SendData;
                nodes.Add(node);
            }
        }

        public void DetachFrom(IBasicNetworkNode<TData, TAddress> node)
        {
            lock(sync)
            {
                if(nodes.Remove(node))
                {
                    node.TrySendData -= SendData;
                    return;
                }

                throw new RecoverableException($"Provided node is not registered in network '{name}' and cannot be detached from it");
            }
        }

        private bool SendData(TData data, TAddress source, TAddress destination)
        {
            var dataSent = false;
            foreach(var node in GetMatchingNodes(destination))
            {
                node.GetMachine().HandleTimeDomainEvent<object>((_) => node.ReceiveData(data, source, destination), null, TimeDomainsManager.Instance.VirtualTimeStamp);
                dataSent = true;
            }
            return dataSent;
        }

        private IEnumerable<IBasicNetworkNode<TData, TAddress>> GetMatchingNodes(TAddress address)
        {
            lock(sync)
            {
                foreach(var node in nodes)
                {
                    if(address.Equals(node.NodeAddress))
                    {
                        yield return node;
                    }
                }
            }
        }

        private readonly List<IBasicNetworkNode<TData, TAddress>> nodes;
        private readonly string name;
        private readonly object sync;
    }
}
