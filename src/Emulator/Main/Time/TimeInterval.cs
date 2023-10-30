//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents time interval.
    /// Right now it has the resolution of 10^-9 second, but is intended for future extension.
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
            var m = Regex.Match(input, @"(((?<hours>[0-9]+):)?(?<minutes>[0-9]+):)?(?<seconds>[0-9]+)(?<decimals>\.[0-9]+)?");
            if(!m.Success)
            {
                output = Empty;
                return false;
            }

            var hours = m.Groups["hours"].Success ? ulong.Parse(m.Groups["hours"].Value) : 0;
            var minutes = m.Groups["minutes"].Success ? ulong.Parse(m.Groups["minutes"].Value) : 0;
            var seconds = ulong.Parse(m.Groups["seconds"].Value);
            // For convenience we parse "decimals" as fraction of a second, and so we multiply this by the number of ticks in a second
            var decimals = m.Groups["decimals"].Success ? (ulong)(double.Parse($"0{m.Groups["decimals"].Value}", CultureInfo.InvariantCulture) * TicksPerSecond) : 0;

            ulong ticks = 0;
            ticks += decimals;
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

        public static TimeInterval FromNanoseconds(ulong v)
        {
            return FromTicks(v * TicksPerNanosecond);
        }

        public static TimeInterval FromMicroseconds(ulong v)
        {
            return FromTicks(v * TicksPerMicrosecond);
        }

        public static TimeInterval FromMilliseconds(ulong v)
        {
            return FromTicks(v * TicksPerMillisecond);
        }

        public static TimeInterval FromMilliseconds(float v)
        {
            return FromTicks((ulong)(v * TicksPerMillisecond));
        }

        public static TimeInterval FromSeconds(ulong v)
        {
            return FromTicks(v * TicksPerSecond);
        }

        public static TimeInterval FromSeconds(float v)
        {
            return FromTicks((ulong)(v * TicksPerSecond));
        }

        public static TimeInterval FromSeconds(double v)
        {
            return FromTicks((ulong)(v * TicksPerSecond));
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
            // since the number of ticks per second in `TimeSpan` is 10^7 (which gives 100 ns per tick) we must multiply here by 100 to get the number of `ns`.
            return FromNanoseconds(((ulong)span.Ticks) * 100);
        }

        public static TimeInterval FromTimeSpan(TimeSpan span, uint nsResiduum)
        {
            // since the number of ticks per second in `TimeSpan` is 10^7 (which gives 100 ns per tick) we must multiply here by 100 to get the number of `ns`.
            return FromNanoseconds(((ulong)span.Ticks) * 100 + nsResiduum);
        }

        public static TimeInterval FromCPUCycles(ulong cycles, uint performanceInMips, out ulong cyclesResiduum)
        {
            checked
            {
                var residuumModulus = performanceInMips / Misc.GCD(performanceInMips, TicksPerMicrosecond);
                cyclesResiduum = cycles % residuumModulus;
                cycles -= cyclesResiduum;

                ulong ticks = cycles * TicksPerMicrosecond / performanceInMips;
                return FromTicks(ticks);
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
            return TimeSpan.FromTicks((long)ticks / 100);
        }

        public TimeSpan ToTimeSpan(out uint nsResiduum)
        {
            nsResiduum = (uint)(ticks % 100);
            return TimeSpan.FromTicks((long)ticks / 100);
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
            var decimals = ticks % TicksPerSecond;
            var seconds = (long)(ticks / TicksPerSecond);
            var hours = Math.DivRem(seconds, 3600, out seconds);
            var minutes = Math.DivRem(seconds, 60, out seconds);
            return $"{hours:00}:{minutes:00}:{seconds:00}.{decimals:000000000}";
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
            var maxTicks = FromCPUCycles(ulong.MaxValue / TicksPerMicrosecond, performanceInMips, out var unused).Ticks;
            if(ticks >= maxTicks)
            {
                ticksCountResiduum = ticks - maxTicks;
                return ulong.MaxValue;
            }

            checked
            {
                var nanoSeconds = ticks / TicksPerNanosecond;
                ticksCountResiduum = ticks % TicksPerNanosecond;
                return nanoSeconds * performanceInMips / TicksPerMicrosecond;
            }
        }

        public ulong Ticks => ticks;
        public ulong TotalNanoseconds => ticks / TicksPerNanosecond;
        public double TotalMicroseconds => ticks / (double)TicksPerMicrosecond;
        public double TotalMilliseconds => ticks / (double)TicksPerMillisecond;
        public double TotalSeconds => ticks / (double)TicksPerSecond;

        public const ulong TicksPerSecond = TicksPerMillisecond * 1000;
        public const ulong TicksPerMillisecond = TicksPerMicrosecond * 1000;
        public const ulong TicksPerMicrosecond = TicksPerNanosecond * 1000;

        // WARNING: when changing the resolution of TimeInterval update methods: 'TryParse', 'FromTimeSpan', 'ToTimeSpan' and 'FromCPUCycles' accordingly
        public const ulong TicksPerNanosecond = 1;

        static TimeInterval()
        {
            DebugHelper.Assert(TimeSpan.TicksPerSecond == 10000000L, "Number of Ticks in TimeSpan mismatch!");
        }

        private TimeInterval(ulong ticks)
        {
            this.ticks = ticks;
        }

        private ulong ticks;
    }
}
