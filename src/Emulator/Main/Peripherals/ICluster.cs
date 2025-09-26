//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals
{
    public interface ICluster<out T> : IPeripheral, IEnumerable<T>
    {
        IEnumerable<ICluster<T>> Clusters { get; }

        IEnumerable<T> Clustered { get; }
    }
}