//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    [Convertible]
    public sealed class GPIO : IGPIOWithHooks
    {
        public GPIO()
        {
            sync = new object();
            targets = new List<GPIOEndpoint>();
            stateChangedHook = delegate {};
        }

        public void Set(bool value)
        {
            // yup, we're locking on self in order not to create an additional field (and object, more importantly)
            lock(sync)
            {
                if(state == value)
                {
                    return;
                }
                state = value;
                for(var i = 0; i < targets.Count; ++i)
                {
                    targets[i].Receiver.OnGPIO(targets[i].Number, state);
                }
                stateChangedHook(value);
            }
        }

        public void Toggle()
        {
            lock(sync)
            {
                Set(!IsSet);
            }
        }

        public void Connect(IGPIOReceiver destination, int destinationNumber)
        {
            if(destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            Validate(destination, destinationNumber);
            lock(sync)
            {
                if(!targets.Any(x => x.Receiver == destination && x.Number == destinationNumber))
                {
                    targets.Add(new GPIOEndpoint(destination, destinationNumber));
                    destination.OnGPIO(destinationNumber, state);
                }
            }
        }

        public void Disconnect()
        {
            lock(sync)
            {
                targets.Clear();
            }
        }

        public void Disconnect(GPIOEndpoint endpoint)
        {
            lock(sync)
            {
                targets.Remove(endpoint);
            }
        }

        public override string ToString()
        {
            return IsSet ? "GPIO: set" : "GPIO: unset";
        }

        public bool IsSet
        {
            get
            {
                lock(sync)
                {
                    return state;
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock(sync)
                {
                    return targets.Count > 0;
                }
            }
        }

        public IList<GPIOEndpoint> Endpoints
        {
            get
            {
                lock(sync)
                {
                    return targets;
                }
            }
        }

        public void AddStateChangedHook(Action<bool> hook)
        {
            stateChangedHook += hook;
        }

        public void RemoveStateChangedHook(Action<bool> hook)
        {
            stateChangedHook -= hook;
        }

        public void RemoveAllStateChangedHooks()
        {
            stateChangedHook = delegate {};
        }

        private static void Validate(IGPIOReceiver to, int toNumber)
        {
            var destPeriInNum = to.GetPeripheralInputCount();
            if(destPeriInNum != 0 && toNumber >= destPeriInNum)
            {
                throw new ConstructionException(string.Format(
                    "Cannot connect {0}th input of {1}; it has only {2} GPIO inputs.",
                    toNumber, to, destPeriInNum));
            }           
        }

        private bool state;
        private Action<bool> stateChangedHook;
        private readonly object sync;
        private readonly IList<GPIOEndpoint> targets;
    }
}

