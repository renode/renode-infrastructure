//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.Collections
{
    /// <summary>
    /// Implementation of an unsigned 128-bit integer type.
    /// </summary>
    /// <remarks>
    /// This type exists as a workaround for the lack of the
    /// <c>System.UInt128</c> type in the Mono runtime.
    /// Once support for Mono is dropped, this implementation
    /// can be safely removed and replaced with the native
    /// .NET equivalent.
    /// </remarks>
    public
#if NET
    readonly
#endif
    struct MonoUInt128 : IEquatable<MonoUInt128>
    {
        private readonly ulong High;
        private readonly ulong Low;

        public MonoUInt128(ulong low)
        {
            High = 0;
            Low = low;
        }

        public MonoUInt128(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        public string ToHexString()
        {
            return "0x" + High.ToString("X16") + Low.ToString("X16");
        }

        public static readonly MonoUInt128 Zero = new MonoUInt128(0);
        public static readonly MonoUInt128 One  = new MonoUInt128(1);
        public static readonly MonoUInt128 MaxValue = new MonoUInt128(ulong.MaxValue, ulong.MaxValue);

        public bool IsZero => High == 0 && Low == 0;

        // 'Shift Count Masking' prevents easy branchless implementation
        public static MonoUInt128 operator <<(MonoUInt128 value, int shift)
        {
            // 'Shift Count Masking' to make it consistent with UInt128
            shift &= 0x7F;

            if(shift == 0)
                return value;

            if(shift >= 64)
            {
                return new MonoUInt128(
                    value.Low << (shift - 64),
                    0
                );
            }

            return new MonoUInt128(
                (value.High << shift) | (value.Low >> (64 - shift)),
                value.Low << shift
            );
        }

        public static MonoUInt128 operator &(MonoUInt128 a, MonoUInt128 b)
        {
            return new MonoUInt128(
                a.High & b.High,
                a.Low & b.Low
            );
        }

        public static MonoUInt128 operator |(MonoUInt128 a, MonoUInt128 b)
        {
            return new MonoUInt128(
                a.High | b.High,
                a.Low | b.Low
            );
        }

        public bool Equals(MonoUInt128 other)
            => Low == other.Low && High == other.High;

        public override int GetHashCode() => unchecked(397 * Low.GetHashCode() + High.GetHashCode());

        public static bool operator ==(MonoUInt128 a, MonoUInt128 b) => a.Equals(b);

        public static bool operator !=(MonoUInt128 a, MonoUInt128 b) => !a.Equals(b);

        public override bool Equals(object obj)
        {
            return obj is MonoUInt128 && Equals((MonoUInt128)obj);
        }
    }
}
