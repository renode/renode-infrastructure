//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public class PeripheralsChangedEventArgs
    {
        // This can't be a public constructor cause the arguments are the same as the protected
        // constructor and adding a fake argument to make it possible seems... fake.
        //
        // Constructing `PeripheralsChangedEventArgs` with `PeripheralChangeType.Addition` isn't
        // allowed. It makes `PeripheralsAddedEventArgs`, which includes `IRegistrationPoint`,
        // always used for addition.
        public static PeripheralsChangedEventArgs Create(IPeripheral peripheral, PeripheralChangeType operation)
        {
            DebugHelper.Assert(operation != PeripheralChangeType.Addition);
            return new PeripheralsChangedEventArgs(peripheral, operation);
        }

        public IPeripheral Peripheral { get; private set; }
        public PeripheralChangeType Operation { get; private set; }

        protected PeripheralsChangedEventArgs(IPeripheral peripheral, PeripheralChangeType operation)
        {
            Peripheral = peripheral;
            Operation = operation;
        }

        public enum PeripheralChangeType
        {
            Addition,
            Removal,
            Moved,
            CompleteRemoval,
            NameChanged
        }
    }
}
