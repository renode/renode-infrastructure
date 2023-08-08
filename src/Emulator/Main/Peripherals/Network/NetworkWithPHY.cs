//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract class NetworkWithPHY: IPeripheral, IPeripheralContainer<IPhysicalLayer, PHYRegistrationPoint>
    {
        protected NetworkWithPHY(IMachine machine)
        {
            phys = new Dictionary<uint, IPhysicalLayer>();
            this.machine = machine;
        }

        #region IRegister[IPHYInterface,PHYRegistrationPoint] implementation
        public void Register(IPhysicalLayer peripheral, PHYRegistrationPoint registrationPoint)
        {
            if (phys.ContainsKey(registrationPoint.Id))
            {
                throw new RecoverableException("Selected registration port is already taken.");
            }
            phys.Add(registrationPoint.Id, peripheral);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IPhysicalLayer peripheral)
        {
            var key = phys.First(x => x.Value == peripheral).Key;
            phys.Remove(key);
            machine.UnregisterAsAChildOf(this, peripheral);
        }
        #endregion

        #region IContainer[IPHYInterface,PHYRegistrationPoint] implementation
        public IEnumerable<PHYRegistrationPoint> GetRegistrationPoints(IPhysicalLayer peripheral)
        {
            return phys.Select(x => new PHYRegistrationPoint(x.Key));
        }

        public IEnumerable<IRegistered<IPhysicalLayer, PHYRegistrationPoint>> Children =>
            phys.Select(x => Registered.Create(x.Value, new PHYRegistrationPoint(x.Key)));
        #endregion

        protected bool TryGetPhy<T>(uint id, out IPhysicalLayer<T> phy)
        {
            if(phys.TryGetValue(id, out var _phy) && _phy is IPhysicalLayer<T>)
            {
                phy = (IPhysicalLayer<T>)_phy;
                return true;
            }
            phy = default(IPhysicalLayer<T>);
            return false;
        }

        protected bool TryGetPhy<T, V>(uint id, out IPhysicalLayer<T, V> phy)
        {
            if(phys.TryGetValue(id, out var _phy) && _phy is IPhysicalLayer<T, V>)
            {
                phy = (IPhysicalLayer<T, V>)_phy;
                return true;
            }
            phy = default(IPhysicalLayer<T, V>);
            return false;
        }

        protected Dictionary<uint, IPhysicalLayer> phys;
        protected IMachine machine;

        public abstract void Reset();
    }
}

