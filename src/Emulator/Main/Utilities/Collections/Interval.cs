//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    /// <summary>
    /// Defines a basic interval concept. A stucture that has a beginning and and end and can answer the questions, whether it contains a scalar, or encloses another interval.
    /// </summary>
    public interface IInterval<TScalar> where TScalar : struct, IComparable<TScalar>
    {
        bool Contains(TScalar x);

        bool Contains(IInterval<TScalar> interval);

        TScalar Start { get; }

        TScalar End { get; }
    }

    /// <summary>
    /// Interval comparer. Implements normal comparer and equality comparer which is needed by Distinct Linq method.
    /// Interval is larger than the other when it start later. If both start at the same address, the one that ends earlier is larger.
    /// It makes the symbols that are enclosed within other to appear later than them.
    /// </summary>
    public class IntervalComparer<TScalar> : Comparer<IInterval<TScalar>>, IEqualityComparer<IInterval<TScalar>> where TScalar : struct, IComparable<TScalar>
    {
        public override int Compare(IInterval<TScalar> lhs, IInterval<TScalar> rhs)
        {
            if(lhs.Start.CompareTo(rhs.Start) != 0)
            {
                return lhs.Start.CompareTo(rhs.Start);
            }
            return rhs.End.CompareTo(lhs.End);
        }

        public bool Equals(IInterval<TScalar> x, IInterval<TScalar> y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(IInterval<TScalar> obj)
        {
            unchecked
            {
                const int funnyPrime1 = 9901;
                const int funnyPrime2 = 99990001;
                int hash1 = obj.Start.GetHashCode();
                int hash2 = obj.End.GetHashCode();
                int hash = (hash1 * funnyPrime1) ^ (hash2 * funnyPrime2);
                return hash;
            }
        }
    }

    /// <summary>
    /// Basic reference implementation of an interval.
    /// </summary>
    public class Interval<TScalar> : IInterval<TScalar> where TScalar : struct, IComparable<TScalar>
    {
        public Interval(TScalar start, TScalar end)
        {
            Start = start;
            End = end;
        }

        public bool Contains(TScalar x)
        {
            var startCompare = Start.CompareTo(x);
            return (startCompare == 0 || ((startCompare < 0) && (End.CompareTo(x) > 0)));
        }

        public bool Contains(IInterval<TScalar> other)
        {
            return (Start.CompareTo(other.Start) <= 0) && (End.CompareTo(other.End) > 0);
        }

        public TScalar Start { get; set; }

        public TScalar End { get; set; }
    }
}

