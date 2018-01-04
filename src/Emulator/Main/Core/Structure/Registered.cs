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
    public class Registered<TPeripheral, TRegistrationPoint> : IRegistered<TPeripheral, TRegistrationPoint>
        where TPeripheral : IPeripheral where TRegistrationPoint : IRegistrationPoint
    {
        public Registered(TPeripheral peripheral, TRegistrationPoint registrationPoint)
        {
            Peripheral = peripheral;
            RegistrationPoint = registrationPoint;
        }

        public TPeripheral Peripheral { get; private set; }
        public TRegistrationPoint RegistrationPoint { get; private set; }
    }

    public static class Registered
    {
        public static Registered<TPeripheral, TRegistrationPoint> Create<TPeripheral, TRegistrationPoint>
            (TPeripheral peripheral, TRegistrationPoint registrationPoint)
            where TPeripheral : IPeripheral  where TRegistrationPoint : IRegistrationPoint
        {
            return new Registered<TPeripheral, TRegistrationPoint>(peripheral, registrationPoint);
        }
    }
}
