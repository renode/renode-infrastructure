//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusRangeRegistration : IRegistrationPoint
    {
        public BusRangeRegistration(Range range, ulong offset = 0)
        {
            Range = range;
            Offset = offset;
        }

        public BusRangeRegistration(ulong address, ulong size, ulong offset = 0) :
            this(new Range(address, size), offset)
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
            if(Offset != 0)
            {
                return string.Format ("{0} with offset {1}", Range, Offset);
            }
            return string.Format("{0}", Range);
        }

        public static implicit operator BusRangeRegistration(Range range)
        {
            return new BusRangeRegistration(range);
        }

        public Range Range { get; set; }
        public ulong Offset { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as BusRangeRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            return Range == other.Range && Offset == other.Offset;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * Range.GetHashCode() + 23 * Offset.GetHashCode();
            }
        }
    }
}

