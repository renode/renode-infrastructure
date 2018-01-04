//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core.Structure
{
    //TODO: constraint on T
    public class NumberRegistrationPoint<T> : IRegistrationPoint
    {
        public T Address { get; private set ;}
        
        public NumberRegistrationPoint(T address)
        {
            Address = address;
        }
        
        public string PrettyString
        {
            get
            {
                return ToString();
            }
        }
        
        public override string ToString()
        {
            return string.Format("Address: {0}", Address);
        }

        public override bool Equals(object obj)
        {
            var other = obj as NumberRegistrationPoint<T>;
            if(other == null)
            {
                return false;
            }
            if(ReferenceEquals(this, obj))
            {
                return true;
            }
            return Address.Equals(other.Address);
        }
        

        public override int GetHashCode()
        {
            unchecked
            {
                return (Address != null ? Address.GetHashCode() : 0);
            }
        }
        
    }
}

