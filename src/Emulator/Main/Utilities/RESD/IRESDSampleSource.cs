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

        /// <value>Is sample source read-only?</value>
        bool ReadOnly { get; }

        /// <value>Try to set new sample for sample source</value>
        /// <remark>This method should always fail when ReadOnly is true</remark>
        public bool TrySetSample(T newSample);
    }
}
