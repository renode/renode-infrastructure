//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents an object that is aware of time flow.
    /// </summary>
    public interface ITimeSink
    {
        /// <summary>
        /// Gets or sets handle used to synchronize time.
        /// </summary>
        TimeHandle TimeHandle { get; set; }
    }
}
