//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusMultiRegistration : BusRangeRegistration
    {
        public BusMultiRegistration(ulong address, ulong size, string region) : base(address, size)
        {
            if(string.IsNullOrWhiteSpace(region))
            {
                throw new ConstructionException("'Region' parameter cannot be null or empty.");
            }
            Address = address;
            ConnectionRegionName = region;
        }

        public ulong Address { get; private set; }
        public string ConnectionRegionName { get; private set; }
        public override string PrettyString { get { return ToString(); } }

        public override bool Equals(object obj)
        {
            var other = obj as BusMultiRegistration;
            if(other == null)
            {
                return false;
            }
            if(ReferenceEquals(this, obj))
            {
                return true;
            }
            return Address == other.Address && ConnectionRegionName == other.ConnectionRegionName;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * Address.GetHashCode() + 101 * ConnectionRegionName.GetHashCode();
            }
        }        
    }
}

