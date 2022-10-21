//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.Structure
{
    public class TypedNumberRegistrationPoint<T> : IRegistrationPoint
    {
        public T Address { get; private set; }
        public Type Type { get; private set; }

        public TypedNumberRegistrationPoint(T address)
        {
            Address = address;
            Type = typeof(IPeripheral);
        }

        public TypedNumberRegistrationPoint(T address, Type type)
        {
            Address = address;
            Type = type;
        }

        public TypedNumberRegistrationPoint<T> WithType<P>()
        {
            return new TypedNumberRegistrationPoint<T>(Address, typeof(P));
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
            return string.Format("Type: {0}, Address: {1}", Type, Address);
        }

        public override bool Equals(object obj)
        {
            var other = obj as TypedNumberRegistrationPoint<T>;
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
                return (Address != null ? new Tuple<Type, T>(Type, Address).GetHashCode() : 0);
            }
        }
    }
}
