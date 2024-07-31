//
// Copyright (c) 2010-2024 Antmicro
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
    public class BusPointRegistration : BusRegistration
    {
        public BusPointRegistration(ulong address, ulong offset = 0, ICPU cpu = null, ICluster<ICPU> cluster = null) : base(address, offset, cpu, cluster)
        {
        }

        public override string ToString()
        {
            var result = $"0x{StartingPoint:X}";
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

        public override string PrettyString
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

        public override bool Equals(object obj)
        {
            var other = obj as BusPointRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;

            return StartingPoint == other.StartingPoint && Offset == other.Offset && CPU == other.CPU && Cluster == other.Cluster;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * StartingPoint.GetHashCode() + 23 * Offset.GetHashCode() + 101 * (CPU?.GetHashCode() ?? 0) + 397 * (Cluster?.GetHashCode() ?? 0);
            }
        }

        public void RegisterForEachContext(Action<BusPointRegistration> register)
        {
            RegisterForEachContextInner(register, cpu => new BusPointRegistration(StartingPoint, Offset, cpu));
        }
    }
}

