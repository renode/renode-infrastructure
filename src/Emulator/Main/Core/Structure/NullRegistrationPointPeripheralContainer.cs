//
// Copyright (c) 2010-2014 Antmicro
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

        protected NullRegistrationPointPeripheralContainer(IMachine machine)
        {
            Machine = machine;
            container = new NullRegistrationPointContainerHelper<TPeripheral>(machine, this);
        }

        public virtual void Register(TPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            container.Register(peripheral, registrationPoint);
        }

        public virtual void Unregister(TPeripheral peripheral)
        {
            container.Unregister(peripheral);
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(TPeripheral peripheral)
        {
            return container.GetRegistrationPoints(peripheral);
        }

        public IEnumerable<IRegistered<TPeripheral, NullRegistrationPoint>> Children => container.Children;

        protected TPeripheral RegisteredPeripheral => container.RegisteredPeripheral;

        protected readonly IMachine Machine;

        private readonly NullRegistrationPointContainerHelper<TPeripheral> container;
    }

    public class NullRegistrationPointContainerHelper<TPeripheral> : IPeripheralContainer<TPeripheral, NullRegistrationPoint>
        where TPeripheral : class, IPeripheral
    {
        public NullRegistrationPointContainerHelper(IMachine machine, IPeripheral owner)
        {
            this.machine = machine;
            this.owner = owner;
        }

        public void Register(TPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            if(RegisteredPeripheral != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            machine.RegisterAsAChildOf(owner, peripheral, registrationPoint);
            RegisteredPeripheral = peripheral;
        }

        public void Unregister(TPeripheral peripheral)
        {
            if(RegisteredPeripheral == null || RegisteredPeripheral != peripheral)
            {
                throw new RegistrationException("The specified peripheral was never registered.");
            }
            machine.UnregisterAsAChildOf(owner, peripheral);
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

        public TPeripheral RegisteredPeripheral { get; private set; }

        private readonly IMachine machine;
        private readonly IPeripheral owner;
    }
}
