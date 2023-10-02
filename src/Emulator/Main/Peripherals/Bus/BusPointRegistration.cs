//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusPointRegistration : IPerCoreRegistration
    {
        public BusPointRegistration(ulong address, ulong offset = 0, ICPU cpu = null)
        {
            StartingPoint = address;
            Offset = offset;
            CPU = cpu;
        }

        public override string ToString()
        {
            var result = StartingPoint.ToString();
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

        public string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public static implicit operator BusPointRegistration(ulong address)
        {
            return new BusPointRegistration(address);
        }

        public ulong StartingPoint { get; set; }
        public ulong Offset { get; set; }
        public ICPU CPU { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as BusPointRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;

            return StartingPoint == other.StartingPoint && Offset == other.Offset && CPU == other.CPU;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * StartingPoint.GetHashCode() + 23 * Offset.GetHashCode() + 101 * (CPU?.GetHashCode() ?? 0);
            }
        }
    }
}

