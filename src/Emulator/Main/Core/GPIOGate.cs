//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Core
{
    public class GPIOGate
    {
        public GPIOGate(GPIO destinationIrq)
        {
            destination = destinationIrq;
            nodes = new List<IrqNode>();
        }

        public IGPIO GetGPIO()
        {
            var result = new IrqNode(destination, HandleChangeState);
            nodes.Add(result);
            return result;
        }

        private void HandleChangeState()
        {
            for(var i = 0; i < nodes.Count; ++i)
            {
                if(nodes[i].LocalState)
                {
                    destination.Set(true);
                    return;
                }
            }
            destination.Set(false);
        }

        private readonly GPIO destination;

        private readonly List<IrqNode> nodes;

        private class IrqNode : IGPIO
        {
            public IrqNode(IGPIO destination, Action changeCallback)
            {
                this.destination = destination;
                this.changeCallback = changeCallback;
            }

            public void Connect(IGPIOReceiver destination, int destinationNumber)
            {
                throw new NotImplementedException();
            }

            public void Disconnect()
            {
                throw new NotImplementedException();
            }

            public void Disconnect(GPIOEndpoint endpoint)
            {
                throw new NotImplementedException();
            }

            public void Set(bool value)
            {
                LocalState = value;
                changeCallback();
            }

            public void Toggle()
            {
                Set(!LocalState);
            }

            public bool LocalState { get; private set; }

            public bool IsSet => destination.IsSet;

            public bool IsConnected => destination.IsConnected;

            public IList<GPIOEndpoint> Endpoints => destination.Endpoints;

            private readonly IGPIO destination;
            private readonly Action changeCallback;
        }
    }
}
