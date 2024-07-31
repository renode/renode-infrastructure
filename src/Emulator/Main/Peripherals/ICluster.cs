//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals
{
    public interface ICluster<out T> : IPeripheral
    {
        IEnumerable<ICluster<T>> Clusters { get; }
        IEnumerable<T> Clustered { get; }
    }
}
