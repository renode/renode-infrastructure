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
    /// Represents an object generating time flow.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>
        /// Gets or sets a virtual time unit of synchronization of this time source.
        /// </summary>
        /// <remark>
        /// All time handles obtained from this object are guaranteed not to be de-synchronized by more than the current <see cref="Quantum">.
        /// Setting small values increases execution precision but can degrade performance.
        /// The value of this property can be safely changed in any moment, but the new value won't be in use before the next synchronization phase.
        /// </remark>
        TimeInterval Quantum { get; set; }

        /// <summary>
        /// Gets time domain of this time source.
        /// </summary>
        ITimeDomain Domain { get; }

        /// <summary>
        /// Gets moment of the nearest synchronization point.
        /// </summary>
        TimeInterval NearestSyncPoint { get; }

        /// <summary>
        /// Gets the amount of virtual time elapsed from the perspective of this time source.
        /// </summary>
        TimeInterval ElapsedVirtualTime { get; }

        /// <summary>
        /// Registers a new time sink in this source.
        /// </summary>
        /// <remarks>
        /// All sinks registered in the same source are synchronized and guaranteed not to be de-synchronized by more than the current <see cref="Quantum">.
        /// </remarks>
        void RegisterSink(ITimeSink sink);

        /// <summary>
        /// Used by a time sink to inform that it became active again.
        /// </summary>
        void ReportHandleActive();

        /// <summary>
        /// Used by a time sink to inform that it has processed some part of the granted time.
        /// </summary>
        /// <remark>
        /// Calling this method does not mean that the granted time is fully processed. It is just used to update Elapsed Virtual Time more often than Quantum in order to handle timers well.
        /// </remark>
        void ReportTimeProgress();
    }
}
