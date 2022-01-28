//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents time interval.
    /// Right now it has the resolution of 10^-6 second, but is intended for future extension.
    /// </summary>
    public struct TimeInterval : IComparable<TimeInterval>, IEquatable<TimeInterval>
    {
        // this method is required by a parsing mechanism in the monitor
        public static explicit operator TimeInterval(string s)
        {
            if(!TryParse(s, out var output))
            {
                throw new RecoverableException("Could not parse ${s} to time interval. Provide input in form 00:00:00.0000");
            }
            return output;
        }

        public static bool TryParse(string input, out TimeInterval output)
        {
            var m = Regex.Match(input, @"(((?<hours>[0-9]+):)?(?<minutes>[0-9]+):)?(?<seconds>[0-9]+)(?<microseconds>\.[0-9]+)?");
            if(!m.Success)
            {
                output = Empty;
                return false;
            }

            var hours = m.Groups["hours"].Success ? ulong.Parse(m.Groups["hours"].Value) : 0;
            var minutes = m.Groups["minutes"].Success ? ulong.Parse(m.Groups["minutes"].Value) : 0;
            var seconds = ulong.Parse(m.Groups["seconds"].Value);
            var microseconds = m.Groups["microseconds"].Success ? (ulong)(double.Parse($"0{m.Groups["microseconds"].Value}", CultureInfo.InvariantCulture) * 1000000) : 0;

            ulong ticks = 0;
            ticks += microseconds * TicksPerMicrosecond;
            ticks += seconds * TicksPerSecond;
            ticks += minutes * (60 * TicksPerSecond);
            ticks += hours * (3600 * TicksPerSecond);

            output = new TimeInterval(ticks);
            return true;
        }

        public static TimeInterval Min(TimeInterval t1, TimeInterval t2)
        {
            return (t1.ticks <= t2.ticks) ? t1 : t2;
        }

        public static TimeInterval FromMicroseconds(ulong v)
        {
            return FromTicks(v * TimeInterval.TicksPerMicrosecond);
        }

        public static TimeInterval FromMilliseconds(ulong v)
        {
            return FromTicks(v * TimeInterval.TicksPerMillisecond);
        }

        public static TimeInterval FromMilliseconds(float v)
        {
            return FromTicks((ulong)(v * TimeInterval.TicksPerMillisecond));
        }

        public static TimeInterval FromSeconds(ulong v)
        {
            return FromTicks(v * TimeInterval.TicksPerSecond);
        }

        public static TimeInterval FromSeconds(float v)
        {
            return FromTicks((ulong)(v * TimeInterval.TicksPerSecond));
        }

        public static TimeInterval FromMinutes(ulong v)
        {
            return FromSeconds(v * 60);
        }

        public static TimeInterval FromMinutes(float v)
        {
            return FromSeconds(v * 60);
        }

        public static TimeInterval FromTicks(ulong ticks)
        {
            return new TimeInterval(ticks);
        }

        public static TimeInterval FromTimeSpan(TimeSpan span)
        {
            // since the number of ticks per second in `TimeSpan` is 10^7 (which gives 10 us per tick) we must divide here by 10 to get the number of `us`.
            return new TimeInterval((ulong)span.Ticks / 10);
        }

        public static TimeInterval FromCPUCycles(ulong cycles, uint performanceInMips, out ulong cyclesResiduum)
        {
            checked
            {
                cyclesResiduum = cycles % performanceInMips;
                ulong useconds = cycles / performanceInMips;
                return TimeInterval.FromTicks(useconds * TicksPerMicrosecond);
            }
        }

        public static TimeInterval operator +(TimeInterval t1, TimeInterval t2)
        {
            return new TimeInterval(checked(t1.ticks + t2.ticks));
        }

        public static TimeInterval operator -(TimeInterval t1, TimeInterval t2)
        {
            return new TimeInterval(checked(t1.ticks - t2.ticks));
        }

        public static bool operator <(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks < t2.ticks;
        }

        public static bool operator >(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks > t2.ticks;
        }

        public static bool operator <=(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks <= t2.ticks;
        }

        public static bool operator >=(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks >= t2.ticks;
        }

        public static bool operator ==(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks == t2.ticks;
        }

        public static bool operator !=(TimeInterval t1, TimeInterval t2)
        {
            return t1.ticks != t2.ticks;
        }

        public static readonly TimeInterval Empty = FromTicks(0);
        public static readonly TimeInterval Maximal = FromTicks(ulong.MaxValue);

        public int CompareTo(TimeInterval other)
        {
            return ticks.CompareTo(other.ticks);
        }

        public TimeSpan ToTimeSpan()
        {
            return TimeSpan.FromTicks(checked((long)ticks * 10));
        }

        public override bool Equals(object obj)
        {
            return (obj is TimeInterval ts) && this.ticks == ts.ticks;
        }

        public bool Equals(TimeInterval ts)
        {
            return this.ticks == ts.ticks;
        }

        public override int GetHashCode()
        {
            return (int)ticks;
        }

        public override string ToString()
        {
            var microseconds = (ticks / TicksPerMicrosecond) % 1000000;
            var seconds = (long)(ticks / TicksPerSecond);
            var hours = Math.DivRem(seconds, 3600, out seconds);
            var minutes = Math.DivRem(seconds, 60, out seconds);
            return $"{hours:00}:{minutes:00}:{seconds:00}.{microseconds:000000}";
        }

        public TimeInterval WithTicksMin(ulong ticks)
        {
            return new TimeInterval(Math.Min(this.ticks, ticks));
        }

        public TimeInterval WithScaledTicks(double factor)
        {
            return new TimeInterval((ulong)(ticks * factor));
        }

        public ulong ToCPUCycles(uint performanceInMips, out ulong ticksCountResiduum)
        {
            var maxTicks = FromCPUCycles(ulong.MaxValue, performanceInMips, out var unused).Ticks;
            if(ticks >= maxTicks)
            {
                ticksCountResiduum = ticks - maxTicks;
                return ulong.MaxValue;
            }

            checked
            {
                var microSeconds = ticks / TicksPerMicrosecond;
                ticksCountResiduum = ticks % TicksPerMicrosecond;
                return microSeconds * performanceInMips;
            }
        }

        public ulong Ticks { get { return ticks; } }
        public double TotalSeconds { get { return ticks / (double)TicksPerSecond; } }
        public double TotalMilliseconds { get { return ticks / (double)TicksPerMillisecond; }}

        public ulong InMicroseconds { get { return ticks / TicksPerMicrosecond; } }

        public const ulong TicksPerSecond = TicksPerMicrosecond * 1000000;
        public const ulong TicksPerMillisecond = TicksPerMicrosecond * 1000;

        // WARNING: when changing the resolution of TimeInterval update methods: 'FromCPUCycles', 'TryParse', 'FromTimeSpan' and 'ToTimeSpan' accordingly
        public const ulong TicksPerMicrosecond = 1;

        private TimeInterval(ulong ticks)
        {
            this.ticks = ticks;
        }

        private ulong ticks;
    }
}
