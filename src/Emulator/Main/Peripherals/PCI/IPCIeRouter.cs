//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.PCI
{
    public interface IPCIeRouter
    {
        void RegisterBar(Range range, IPCIePeripheral peripheral, uint bar);
    }
}