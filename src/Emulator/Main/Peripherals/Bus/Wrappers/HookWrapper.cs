//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public abstract class HookWrapper
    {
        protected HookWrapper(PeripheralAccessMethods pam, Type type, Range? subrange)
        {
            Pam = pam;
            Peripheral = pam.Peripheral;
            Name = type.Name;
            Subrange = subrange;
        }

        protected readonly string Name;
        protected readonly PeripheralAccessMethods Pam;
        protected readonly IBusPeripheral Peripheral;
        protected readonly Range? Subrange;
    }
}
