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
    /// Represents a time stamp information consisting of time interval and the domain.
    /// </summary>
    /// <remarks>
    /// Time intervals from different time domains are not comparable.
    /// </remarks>
    public struct TimeStamp
    {
        public TimeStamp(TimeInterval interval, ITimeDomain domain)
        {
            TimeElapsed = interval;
            Domain = domain;
        }

        public override string ToString()
        {
            return $"[Domain = {Domain}, TimeElapsed = {TimeElapsed}]";
        }

        public TimeInterval TimeElapsed { get; private set; }
        public ITimeDomain Domain { get; private set; }
    }
}
