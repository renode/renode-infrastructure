//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Utilities.RESD
{
    public interface IRESDSampleSource<T> : IPeripheral where T : RESDSample
    {
        /// <value>The RESD Sample holding the current data.</value>
        T Sample { get; }

        /// <summary>
        /// Event invoked on a <see>Sample</see> change. Intended for use with immediate actions,
        /// such as warning the user about out of range values.
        /// The recipient of RESD sample source is still expected to pull the value from
        /// <see>Sample</see> when it needs it as the IRESDSampleSource object is the single source
        /// of truth for the sample data.
        /// </summary>
        event Action<T> NewSample;
    }
}
