//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public class PeripheralsAddedEventArgs : PeripheralsChangedEventArgs
    {
        public static PeripheralsAddedEventArgs Create(IPeripheral peripheral, IRegistrationPoint registrationPoint)
        {
            return new PeripheralsAddedEventArgs(peripheral, registrationPoint);
        }

        public IRegistrationPoint RegistrationPoint { get; private set; }

        protected PeripheralsAddedEventArgs(IPeripheral peripheral, IRegistrationPoint registrationPoint)
            : base(peripheral, PeripheralChangeType.Addition)
        {
            DebugHelper.Assert(registrationPoint != null);
            RegistrationPoint = registrationPoint;
        }
    }
}
