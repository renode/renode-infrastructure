//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Core
{
    public struct Range
    {
        public static bool TryCreate(ulong startAddress, ulong size, out Range range)
        {
            range = default(Range);
            range.StartAddress = startAddress;
            range.EndAddress = startAddress + size - 1;
            return true;
        }

        public Range(ulong startAddress, ulong size):this()
        {
            if(!TryCreate(startAddress, size, out this))
            {
                throw new ArgumentException("Size has to be positive or zero.", "size");
            }
        }

        /// <summary>
        /// Checks if this <c>Range</c> can be expanded by <c>range</c> so that
        /// <c>this.Contains(address) || range.Contains(address)</c> equals
        /// <c>this.Expand(range).Contains(address)</c> for any address.
        /// </summary>
        public bool CanBeExpandedBy(Range range)
        {
            return Intersects(range) || IsAdjacentTo(range);
        }

        public bool Contains(ulong address)
        {
            // The empty range does not contain any addresses.
            // Unfortunately, the empty range is indistinguishable from a
            // 1-byte-long range starting at address 0, so such a range
            // will be said not to contain address 0.
            if(this == Empty)
            {
                return false;
            }

            return address >= StartAddress && address <= EndAddress;
        }

        public bool Contains(long address)
        {
            // Range operates on positive numbers
            // so it cannot contain a negative number.
            if(address < 0)
            {
                return false;
            }

            return Contains((ulong)address);
        }

        public bool Contains(Range range)
        {
            // Every range contains the empty range.
            // See `Contains` for a caveat about 1-byte ranges starting at
            // address 0 - here it means that every range will be said to
            // contain a 1-byte-long range starting at address 0.

            if(range == Empty)
            {
                return true;
            }

            return range.StartAddress >= StartAddress && range.EndAddress <= EndAddress;
        }

        /// <param name="range">
        /// <c>range</c> has to overlap or be adjacent to this <c>Range</c>
        /// which can be tested with <c>CanBeExpandedBy(range)</c>.
        /// An <c>ArgumentException</c> is thrown if the condition isn't satisfied.
        /// </param>
        public Range Expand(Range range)
        {
            if(!CanBeExpandedBy(range))
            {
                throw new ArgumentException($"{this} can't be expanded by {range}.");
            }
            var startAddress = Math.Min(StartAddress, range.StartAddress);
            var endAddress = Math.Max(EndAddress, range.EndAddress);

            // + 1 because the argument of `ulong.To` is the first byte after the Range ends.
            return startAddress.To(endAddress + 1);
        }

        public Range Intersect(Range range)
        {
            var startAddress = Math.Max(StartAddress, range.StartAddress);
            var endAddress = Math.Min(EndAddress, range.EndAddress);
            if(startAddress > endAddress)
            {
                return Range.Empty;
            }
            return new Range(startAddress, endAddress - startAddress + 1);
        }

        public bool IsAdjacentTo(Range range)
        {
            return (StartAddress == range.EndAddress + 1) || (EndAddress == range.StartAddress - 1);
        }

        public List<Range> Subtract(Range sub)
        {
            // If the subtracted range does not intersect this range, return this range
            if(!sub.Intersects(this))
            {
                return new List<Range> { this };
            }

            // If the subtracted range contains this range, return an empty list
            if(sub.Contains(this))
            {
                return new List<Range> { };
            }

            // If the subtracted range contains the start of this range,
            // return a range from the end of the subtracted range to the end of this range
            if(sub.Contains(StartAddress))
            {
                return new List<Range> { new Range(sub.EndAddress + 1, EndAddress - sub.EndAddress) };
            }

            // If the subtracted range contains the end of this range,
            // return a range from the start of this range to the start of the subtracted range
            if(sub.Contains(EndAddress))
            {
                return new List<Range> { new Range(StartAddress, sub.StartAddress - StartAddress) };
            }

            // If the subtracted range is contained within this range, return two ranges:
            // one from the start of this range to the start of the subtracted range, and
            // one from the end of the subtracted range to the end of this range.
            // We probably don't need to check this because it's the only possibility left
            if(this.Contains(sub))
            {
                return new List<Range>
                {
                    new Range(StartAddress, sub.StartAddress - StartAddress),
                    new Range(sub.EndAddress + 1, EndAddress - sub.EndAddress)
                };
            }

            throw new Exception("Unreachable");
        }

        public bool Intersects(Range range)
        {
            return Intersect(range) != Range.Empty;
        }

        public ulong StartAddress
        {
            get;
            private set;
        }

        public ulong EndAddress
        {
            get;
            private set;
        }

        public ulong Size
        {
            get
            {
                return EndAddress - StartAddress + 1;
            }
        }

        public Range ShiftBy(long shiftValue)
        {
            return new Range(checked(shiftValue >= 0 ? StartAddress + (ulong)shiftValue : StartAddress - (ulong)(-shiftValue)), Size);
        }

        public Range MoveToZero()
        {
            return new Range(0, Size);
        }

        public override string ToString()
        {
            return string.Format("<0x{0:X8}, 0x{1:X8}>", StartAddress, EndAddress);
        }

        public override bool Equals(object obj)
        {
            if(obj == null)
            {
                return false;
            }
            if(obj.GetType() != typeof(Range))
            {
                return false;
            }
            var other = (Range)obj;
            return this == other;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 7 * StartAddress.GetHashCode() ^ 31 * EndAddress.GetHashCode();
            }
        }

        public static bool operator==(Range range, Range other)
        {
            return range.StartAddress == other.StartAddress && range.EndAddress == other.EndAddress;
        }

        public static bool operator!=(Range range, Range other)
        {
            return !(range == other);
        }

        public static Range operator+(Range range, long addend)
        {
            return range.ShiftBy(addend);
        }

        public static Range operator-(Range range, long minuend)
        {
            return range.ShiftBy(-minuend);
        }

        public static Range Empty;
    }

    public class MinimalRangesCollection : IEnumerable
    {
        /// <summary>
        /// Adds a <c>range</c> expanding any elements, if possible. Therefore ranges overlapping,
        /// and adjacent to the <c>range</c> are removed from the collection and re-added as a
        /// single expanded <c>Range</c>.
        /// </summary>
        public void Add(Range range)
        {
            // ToArray is necessary because ranges gets modified in the code block.
            foreach(var expandableRange in ranges.Where(collectionRange => collectionRange.CanBeExpandedBy(range)).ToArray())
            {
                ranges.Remove(expandableRange);
                range = range.Expand(expandableRange);
            }
            ranges.Add(range);
        }

        public void Clear()
        {
            ranges.Clear();
        }

        public bool ContainsOverlappingRange(Range range)
        {
            return ranges.Any(existingRange => existingRange.Intersects(range));
        }

        public IEnumerator GetEnumerator()
        {
            return ranges.GetEnumerator();
        }

        public bool Remove(Range range)
        {
            return ranges.SubtractAll(range);
        }

        public int Count => ranges.Count;

        private readonly HashSet<Range> ranges = new HashSet<Range>();
    }

    public static class RangeExtensions
    {
        /// <summary>
        /// Subtracts the given <c>range</c> from each overlapping element in <c>ranges</c>.
        /// </summary>
        /// <returns>True if <c>range</c> was subtracted from any element in <c>ranges</c>.</returns>
        public static bool SubtractAll(this ICollection<Range> ranges, Range range)
        {
            // ToArray is necessary because ranges gets modified in the code block.
            var overlappingRanges = ranges.Where(collectionRange => collectionRange.Intersects(range)).ToArray();
            foreach(var overlappingRange in overlappingRanges)
            {
                ranges.Remove(overlappingRange);
                foreach(var remainingRange in overlappingRange.Subtract(range))
                {
                    ranges.Add(remainingRange);
                }
            }
            return overlappingRanges.Any();
        }

        public static Range By(this ulong startAddress, ulong size)
        {
            return new Range(startAddress, size);
        }

        public static Range By(this long startAddress, ulong size)
        {
            return new Range(checked((ulong)startAddress), size);
        }

        public static Range By(this long startAddress, long size)
        {
            return new Range(checked((ulong)startAddress), checked((ulong)size));
        }

        public static Range By(this int startAddress, ulong size)
        {
            return new Range(checked((ulong)startAddress), size);
        }

        public static Range By(this int startAddress, long size)
        {
            return new Range(checked((ulong)startAddress), checked((ulong)size));
        }

        public static Range By(this uint startAddress, ulong size)
        {
            return new Range(startAddress, size);
        }

        public static Range By(this uint startAddress, long size)
        {
            return new Range(startAddress, checked((ulong)size));
        }

        public static Range To(this int startAddress, long endAddress)
        {
            return new Range(checked((ulong)startAddress), checked((ulong)(endAddress - startAddress)));
        }

        public static Range To(this long startAddress, long endAddress)
        {
            return new Range(checked((ulong)startAddress), checked((ulong)(endAddress - startAddress)));
        }

        public static Range To(this ulong startAddress, ulong endAddress)
        {
            return new Range(startAddress, checked(endAddress - startAddress));
        }
    }
}

