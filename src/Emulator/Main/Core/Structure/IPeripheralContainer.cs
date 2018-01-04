//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.Structure
{
    /// <summary>
    /// Interface for objects that allow registering peripherals and addressing/querying for them.
    /// </summary>
    public interface IPeripheralContainer<TPeripheral, TRegistrationPoint> :
        IPeripheralRegister<TPeripheral, TRegistrationPoint>
        where TPeripheral : IPeripheral where TRegistrationPoint : IRegistrationPoint
    {
        IEnumerable<TRegistrationPoint> GetRegistrationPoints(TPeripheral peripheral);
        IEnumerable<IRegistered<TPeripheral, TRegistrationPoint>> Children { get; }
    }
}
