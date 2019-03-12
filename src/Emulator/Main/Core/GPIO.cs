//
// Copyright (c) 2010-2018 Antmicro
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
    public sealed class GPIO : IGPIO
    {
        public GPIO()
        {
            sync = new object();
            targets = new List<GPIOEndpoint>();
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

        private static GPIOAttribute GetAttribute(IGPIOReceiver per)
        {
            return (GPIOAttribute)per.GetType().GetCustomAttributes(true).FirstOrDefault(x => x is GPIOAttribute);
        }
        
        private static void Validate(IGPIOReceiver to, int toNumber)
        {
            var destPeriAttribute = GetAttribute(to);
            var destPeriInNum = destPeriAttribute != null ? destPeriAttribute.NumberOfInputs : 0;
            if(destPeriInNum != 0 && toNumber >= destPeriInNum)
            {
                throw new ConstructionException(string.Format(
                    "Cannot connect {0}th input of {1}; it has only {2} GPIO inputs.",
                    toNumber, to, destPeriInNum));
            }           
        }

        private bool state;
        private readonly object sync;
        private readonly IList<GPIOEndpoint> targets;
    }
}

