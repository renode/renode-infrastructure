//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public class PeripheralsChangedEventArgs 
    {
        public PeripheralsChangedEventArgs(IPeripheral peripheral, PeripheralChangeType operation)
        {
            Peripheral = peripheral;
            Operation = operation;
        }

        public IPeripheral Peripheral { get; private set; }
        public PeripheralChangeType Operation { get; private set; }

        public enum PeripheralChangeType
        {
            Addition,
            Removal,
            CompleteRemoval,
            NameChanged
        }
    }
}

