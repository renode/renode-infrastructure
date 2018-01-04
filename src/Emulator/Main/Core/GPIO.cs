//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Core
{
    [Convertible]
    public sealed class GPIO : IGPIO
    {
        public GPIO()
        {
            sync = new object();
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
                if(target != null)
                {
                    target.OnGPIO(targetNumber, state);
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
                target = destination;
                targetNumber = destinationNumber;
                target.OnGPIO(destinationNumber, state);
            }
        }

        public void Disconnect()
        {
            lock(sync)
            {
                target = null;
                targetNumber = default(int);
            }
        }

        public bool IsConnected
        {
            get
            {
                lock(sync)
                {
                    return target != null;
                }
            }
        }

        public GPIOEndpoint Endpoint
        {
            get
            {
                lock(sync)
                {
                    return target == null ? null : new GPIOEndpoint(target, targetNumber);
                }
            }
        }

        public override string ToString()
        {
            return IsSet ? "GPIO: set":"GPIO: unset";
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

        private IGPIOReceiver target;
        private int targetNumber;
        private bool state;
        private readonly object sync;
    }
}

