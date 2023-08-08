//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public interface IPeripheralsGroup
    {
        string Name { get; }
        IMachine Machine { get; }
        IEnumerable<IPeripheral> Peripherals { get; }

        void Unregister();
    }
}

