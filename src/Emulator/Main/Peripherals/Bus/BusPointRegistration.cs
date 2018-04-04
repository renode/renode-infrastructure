//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusPointRegistration : IRegistrationPoint
    {
        public BusPointRegistration(ulong address, ulong offset = 0)
        {
            StartingPoint = address;
            Offset = offset;
        }
        
        public override string ToString()
        {
            return string.Format ("{0} with offset {1}", StartingPoint, Offset);
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

        public override bool Equals(object obj)
        {
            var other = obj as BusPointRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;

            return StartingPoint == other.StartingPoint && Offset == other.Offset;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * StartingPoint.GetHashCode() + 23 * Offset.GetHashCode();
            }
        }
    }
}

