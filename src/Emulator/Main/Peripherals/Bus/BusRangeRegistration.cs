//
// Copyright (c) 2010-2024 Antmicro
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
    public class BusRangeRegistration : BusRegistration
    {
        public BusRangeRegistration(Range range, ulong offset = 0, ICPU cpu = null, ICluster<ICPU> cluster = null) : base(range.StartAddress, offset, cpu, cluster)
        {
            Range = range;
        }

        public BusRangeRegistration(ulong address, ulong size, ulong offset = 0, ICPU cpu = null, ICluster<ICPU> cluster = null) :
            this(new Range(address, size), offset, cpu, cluster)
        {
        }

        public override string PrettyString
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
                result += $" with offset 0x{Offset:X}";
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

        public Range Range { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as BusRangeRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            return Range == other.Range && Offset == other.Offset && CPU == other.CPU && Cluster == other.Cluster;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * Range.GetHashCode() + 23 * Offset.GetHashCode() + 101 * (CPU?.GetHashCode() ?? 0) + 397 * (Cluster?.GetHashCode() ?? 0);
            }
        }

        public void RegisterForEachContext(Action<BusRangeRegistration> register)
        {
            RegisterForEachContextInner(register, cpu => new BusRangeRegistration(Range, Offset, cpu));
        }
    }
}

