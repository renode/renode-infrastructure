//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Network
{
    public class PHYRegistrationPoint : IRegistrationPoint
    {
        public PHYRegistrationPoint(uint id)
        {
            Id = id;
        }

        public string PrettyString {
            get {
                return ToString();
            }
        }
     
        public override string ToString()
        {
            return string.Format("Address: {0}", Id);
        }
        
        public uint Id {get; private set;}

        public override bool Equals(object obj)
        {
            var other = obj as PHYRegistrationPoint;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            return Id == other.Id;
        }
        

        public override int GetHashCode()
        {
            unchecked
            {
                return Id.GetHashCode();
            }
        }
        
    }
}

