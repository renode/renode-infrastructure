//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public abstract class HookWrapper
    {
        protected HookWrapper(IBusPeripheral peripheral, Type type, Range? subrange)
        {
            Peripheral = peripheral;
            Name = type.Name;
            Subrange = subrange;
        }

        protected readonly string Name;
        protected readonly IBusPeripheral Peripheral;
        protected readonly Range? Subrange;
    }
}

