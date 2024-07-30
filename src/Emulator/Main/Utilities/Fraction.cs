//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.CompilerServices;

namespace Antmicro.Renode.Utilities
{
    public
#if NET
    readonly
#endif
    struct Fraction
    {
        public readonly ulong Numerator;
        public readonly ulong Denominator;
        public readonly bool Minus;

        public ulong Integer => Numerator / Denominator;
        public Fraction Fractional => new Fraction(Numerator % Denominator, Denominator, skipReduce: true);

        public Fraction(ulong numerator, ulong denominator, bool minus = false) : this(numerator, denominator, minus, skipReduce: false)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator +(Fraction a) => a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator -(Fraction a) => new Fraction(a.Numerator, a.Denominator, !a.Minus, skipReduce: true);

        public static Fraction operator +(Fraction a, Fraction b)
        {
            var anum = a.Numerator;
            var bnum = b.Numerator;
            var newden = a.Denominator;

            if(anum == 0)
            {
                return b;
            }
            if(bnum == 0)
            {
                return a;
            }

            if(a.Denominator != b.Denominator)
            {
                newden = Misc.LCM(a.Denominator, b.Denominator);
                anum *= newden / a.Denominator;
                bnum *= newden / b.Denominator;
            }

            if(a.Minus == b.Minus)
            {
                return new Fraction(anum + bnum, newden, a.Minus);
            }
            else
            {
                var minus = bnum > anum ? b.Minus : a.Minus;
                return new Fraction(Math.Max(bnum, anum) - Math.Min(bnum, anum), newden, minus);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator -(Fraction a, Fraction b)
            => a + (-b);

        public static Fraction operator *(Fraction a, Fraction b)
        {
            var anum = a.Numerator;
            var bnum = b.Numerator;
            var aden = a.Denominator;
            var bden = b.Denominator;
            Reduce(ref anum, ref bden);
            Reduce(ref aden, ref bnum);
            return new Fraction(anum * bnum, aden * bden, a.Minus != b.Minus, skipReduce: true);
        }

        public static Fraction operator /(Fraction a, Fraction b)
        {
            var anum = a.Numerator;
            var bnum = b.Numerator;
            var aden = a.Denominator;
            var bden = b.Denominator;
            if(bnum == 0)
            {
                throw new DivideByZeroException();
            }
            Reduce(ref anum, ref bnum);
            Reduce(ref aden, ref bden);
            return new Fraction(anum * bden, aden * bnum, a.Minus != b.Minus, skipReduce: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator +(Fraction a, ulong b)
        {
            if(b == 0)
            {
                return a;
            }
            return a + new Fraction(b, 1, skipReduce: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator +(ulong b, Fraction a)
            => a + b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator -(Fraction a, ulong b)
        {
            if(b == 0)
            {
                return a;
            }
            return a + new Fraction(b, 1, minus: true, skipReduce: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator -(ulong b, Fraction a)
        {
            if(b == 0)
            {
                return -a;
            }
            return new Fraction(b, 1, skipReduce: true) - a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator *(Fraction a, ulong b)
        {
            if(b == 0)
            {
                return Zero;
            }
            if(b == 1)
            {
                return a;
            }
            return a * new Fraction(b, 1, skipReduce: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator *(ulong b, Fraction a)
            => a * b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator /(Fraction a, ulong b)
        {
            if(b == 1)
            {
                return a;
            }
            return a / new Fraction(b, 1, skipReduce: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fraction operator /(ulong b, Fraction a)
        {
            if(b == 0)
            {
                return Zero;
            }
            return new Fraction(b, 1, skipReduce: true) / a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Fraction a, Fraction b)
            => (a.Numerator == b.Numerator && a.Denominator == b.Denominator && a.Minus == b.Minus) || (a.Numerator == 0 && b.Numerator == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Fraction a, Fraction b)
            => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Fraction a, Fraction b)
            => (a - b).Minus;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Fraction a, Fraction b)
            => (b - a).Minus;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Fraction a, Fraction b)
            => !(a > b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Fraction a, Fraction b)
            => !(a < b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Fraction a, ulong b)
            => a.Integer == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Fraction a, ulong b)
            => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Fraction a, ulong b)
            => a.Integer < b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Fraction a, ulong b)
            => a.Integer > b || (a.Integer == b && a.Fractional.Numerator > 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Fraction a, ulong b)
            => !(a > b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Fraction a, ulong b)
            => !(a < b);

        public int CompareTo(Fraction other)
        {
            if(this == other)
            {
                return 0;
            }
            if(this < other)
            {
                return -1;
            }
            return 1;
        }

        public override bool Equals(object obj)
        {
            return (obj is Fraction f) && this == f;
        }

        public override int GetHashCode()
        {
            return (int)(Numerator ^ Denominator * (Minus ? 2L : 1UL));
        }

        public override string ToString()
        {
            char sign = Minus ? '-' : '+';
            if(Denominator == 1)
            {
                return $"{sign}{Numerator}";
            }
            else
            {
                return $"{sign}{Numerator}/{Denominator}";
            }
        }

        public static explicit operator ulong(Fraction f) => f.Integer;
        public static explicit operator Fraction(ulong u) => new Fraction(u, 1, false, skipReduce: true);
        public static readonly Fraction Zero = new Fraction(0, 1, skipReduce: true);

        private Fraction(ulong numerator, ulong denominator, bool minus = false, bool skipReduce = false)
        {
            // Denominator cannot be zero. If the numerator is 0, represent as 0/1 (see Zero)
            if(denominator == 0 || numerator == 0)
            {
                denominator = 1;
            }

            if(!skipReduce)
            {
                Reduce(ref numerator, ref denominator);
            }
            Numerator = numerator;
            Denominator = denominator;
            Minus = minus;
        }

        private static void Reduce(ref ulong a, ref ulong b)
        {
            var gcd = Misc.GCD(a, b);
            if(gcd > 1)
            {
                a /= gcd;
                b /= gcd;
            }
        }
    }
}
