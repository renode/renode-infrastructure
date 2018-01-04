//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.Structure
{
    /// <summary>
    /// An object that allows registration of TPeripheral using TRegistrationPoint.
    /// NOTE: This exists along IPeripheralContainer because some objects handle more than
    /// one TRegistrationPoint for a given TPeripheral.
    /// </summary>
    public interface IPeripheralRegister<TPeripheral, TRegistrationPoint> : ICovariantPeripheralRegister<TPeripheral, TRegistrationPoint>
        where TPeripheral : IPeripheral where TRegistrationPoint : IRegistrationPoint
    {
        void Register(TPeripheral peripheral, TRegistrationPoint registrationPoint);
        void Unregister(TPeripheral peripheral);
    }

    // this interface is needed for `IRegisterController` which describes controller of 'any' register
    // that is encoded as IPeripheralRegister<IPerhipheral, IRegistrationPoint> (that's why we need out)
    public interface ICovariantPeripheralRegister<out TPeripheral, out TRegistrationPoint> : IEmulationElement
        where TPeripheral : IPeripheral where TRegistrationPoint : IRegistrationPoint
    {
    }
}

