//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

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

    public static class TimeSinkExtensions
    {
        public static IDisposable ObtainSinkActiveState(this ITimeSink @this)
        {
            var result = new DisposableWrapper();
            @this.TimeHandle.SinkSideActive = true;
            result.RegisterDisposeAction(() => @this.TimeHandle.SinkSideActive = false);
            return result;
        }

        public static IDisposable ObtainSinkInactiveState(this ITimeSink @this)
        {
            var result = new DisposableWrapper();
            @this.TimeHandle.SinkSideActive = false;
            result.RegisterDisposeAction(() => @this.TimeHandle.SinkSideActive = true);
            return result;
        }
    }
}
