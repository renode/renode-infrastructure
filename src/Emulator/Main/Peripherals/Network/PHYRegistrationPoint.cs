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
    public class PHYRegistrationPoint : IRegistrationPoint, IJsonSerializable
    {
        public PHYRegistrationPoint(uint id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return string.Format("Address: {0}", Id);
        }

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

        public Object SerializeJson()
        {
            return new
            {
                Type = "Network",
                Value = Id
            };
        }

        public string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public uint Id { get; private set; }
    }
}