//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

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

        public bool Contains(ulong address)
        {
            return address >= StartAddress && address <= EndAddress;
        }

        public bool Contains(Range range)
        {
            return range.StartAddress >= StartAddress && range.EndAddress <= EndAddress;
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

    public static class RangeExtensions
    {
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

