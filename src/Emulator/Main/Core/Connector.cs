//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.Collections;
using System.Linq;
using Microsoft.CSharp.RuntimeBinder;

namespace Antmicro.Renode.Core
{
    public class Connector
    {
        public Connector()
        {
            connections = new WeakMultiTable<IConnectable, IEmulationElement>();
        }

        public void Connect(IEmulationElement connectee, IConnectable connector)
        {
            try
            {
                ((dynamic)connector).AttachTo((dynamic)connectee);
                connections.Add(connector, connectee);
            } 
            catch (RuntimeBinderException)
            {
                ThrowConnectionException(connectee.GetType(), connector.GetType());
            }
        }

        public void Disconnect(IEmulationElement connectee, IConnectable connector)
        {
            Disconnect(connector, connectee);
        }

        public void Disconnect(IConnectable connector, IEmulationElement connectee)
        {
            try
            {
                ((dynamic)connector).DetachFrom((dynamic)connectee);
                connections.RemovePair(connector, connectee);
            }
            catch(RuntimeBinderException)
            {
                ThrowConnectionException(connectee.GetType(), connector.GetType(), true);
            }
        }

        public void DisconnectFromAll(IEmulationElement element)
        {
            var interestingKeys = connections.GetAllForRight(element);
            foreach(var external in interestingKeys)
            {
                Disconnect(external, element);
            }
        }

        public IEnumerable<IConnectable<IEmulationElement>> GetConnectionsFor(IEmulationElement obj)
        {
            return connections.GetAllForRight(obj).Cast<IConnectable<IEmulationElement>>();
        }

        public IEnumerable<IEmulationElement> GetObjectsConnectedTo(IConnectable obj)
        {
            return connections.GetAllForLeft(obj).ToList();
        }

        private static void ThrowConnectionException(Type tone, Type ttwo, bool disconnection = false)
        {
            throw new RecoverableException(String.Format("Could not find a way to {2}connect {0} {3} {1}", tone.Name, ttwo.Name, disconnection ? "dis" : string.Empty, disconnection ? "from" : "to"));
        }

        private readonly WeakMultiTable<IConnectable, IEmulationElement> connections;
    }
}
