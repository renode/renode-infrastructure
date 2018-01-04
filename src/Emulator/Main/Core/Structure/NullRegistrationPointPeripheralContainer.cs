//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Core.Structure
{
    public abstract class NullRegistrationPointPeripheralContainer<TPeripheral> :
        IPeripheralContainer<TPeripheral, NullRegistrationPoint>,
        IPeripheral
        where TPeripheral : class, IPeripheral
    {
        public abstract void Reset();

        protected NullRegistrationPointPeripheralContainer(Machine machine)
        {
            Machine = machine;
        }

        public virtual void Register(TPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            if(RegisteredPeripheral != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            RegisteredPeripheral = peripheral;
        }

        public virtual void Unregister(TPeripheral peripheral)
        {
            if(RegisteredPeripheral == null || RegisteredPeripheral != peripheral)
            {
                throw new RegistrationException("The specified peripheral was never registered.");
            }
            Machine.UnregisterAsAChildOf(this, peripheral);
            RegisteredPeripheral = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(TPeripheral peripheral)
        {
            return RegisteredPeripheral != null ?
                new [] { NullRegistrationPoint.Instance } :
                Enumerable.Empty<NullRegistrationPoint>();
        }

        public IEnumerable<IRegistered<TPeripheral, NullRegistrationPoint>> Children
        {
            get
            {
                return RegisteredPeripheral != null ?
                    new [] { Registered.Create(RegisteredPeripheral, NullRegistrationPoint.Instance) } :
                    Enumerable.Empty<IRegistered<TPeripheral, NullRegistrationPoint>>();
            }
        }

        protected readonly Machine Machine;
        protected TPeripheral RegisteredPeripheral;
    }
}
