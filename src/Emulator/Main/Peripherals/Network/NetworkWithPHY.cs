//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract class NetworkWithPHY : IPeripheral, IPeripheralContainer<IPhysicalLayer, PHYRegistrationPoint>
    {
        #region IRegister[IPHYInterface,PHYRegistrationPoint] implementation
        public void Register(IPhysicalLayer peripheral, PHYRegistrationPoint registrationPoint)
        {
            if(phys.ContainsKey(registrationPoint.Id))
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

        public abstract void Reset();

        public IEnumerable<IRegistered<IPhysicalLayer, PHYRegistrationPoint>> Children =>
            phys.Select(x => Registered.Create(x.Value, new PHYRegistrationPoint(x.Key)));

        protected NetworkWithPHY(IMachine machine)
        {
            phys = new Dictionary<uint, IPhysicalLayer>();
            this.machine = machine;
        }
        #endregion

        protected bool TryGetPhy<T>(uint id, out IPhysicalLayer<T> outPhy)
        {
            if(phys.TryGetValue(id, out var phy) && phy is IPhysicalLayer<T>)
            {
                outPhy = (IPhysicalLayer<T>)phy;
                return true;
            }
            outPhy = default(IPhysicalLayer<T>);
            return false;
        }

        protected bool TryGetPhy<T, V>(uint id, out IPhysicalLayer<T, V> outPhy)
        {
            if(phys.TryGetValue(id, out var phy) && phy is IPhysicalLayer<T, V>)
            {
                outPhy = (IPhysicalLayer<T, V>)phy;
                return true;
            }
            outPhy = default(IPhysicalLayer<T, V>);
            return false;
        }

        protected Dictionary<uint, IPhysicalLayer> phys;
        protected IMachine machine;
    }
}