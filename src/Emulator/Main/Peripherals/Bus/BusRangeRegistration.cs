//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.CPU;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusRangeRegistration : IPerCoreRegistration
    {
        public BusRangeRegistration(Range range, ulong offset = 0, ICPU cpu = null)
        {
            Range = range;
            Offset = offset;
            CPU = cpu;
        }

        public BusRangeRegistration(ulong address, ulong size, ulong offset = 0, ICPU cpu = null) :
            this(new Range(address, size), offset, cpu)
        {
        }

        public virtual string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public override string ToString()
        {
            var result = Range.ToString();
            if(Offset != 0)
            {
                result += $" with offset {Offset}";
            }
            if(CPU != null)
            {
                result += $" for core {CPU}";
            }
            return result;
        }

        public static implicit operator BusRangeRegistration(Range range)
        {
            return new BusRangeRegistration(range);
        }

        public ICPU CPU { get; }
        public Range Range { get; set; }
        public ulong Offset { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as BusRangeRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            return Range == other.Range && Offset == other.Offset && CPU == other.CPU;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * Range.GetHashCode() + 23 * Offset.GetHashCode() + 101 * (CPU?.GetHashCode() ?? 0);
            }
        }
    }
}

