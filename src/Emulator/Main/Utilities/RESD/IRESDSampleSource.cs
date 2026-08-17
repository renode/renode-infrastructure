//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Utilities.RESD
{
    public interface IRESDSampleSource<T> : IPeripheral where T : RESDSample
    {
        /// <value>The RESD Sample holding the current data.</value>
        T Sample { get; }
    }
}
