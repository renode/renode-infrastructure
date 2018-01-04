//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Core.Structure
{
    public sealed class USBRegistrationPoint : NumberRegistrationPoint<byte?>
    {
        public USBRegistrationPoint(byte? port = null) : base(port)
        {
        }

        public override string ToString()
        {
            return string.Format("Port {0}", Address);
        }
        public override bool Equals (object obj)
        {
            var other = obj as USBRegistrationPoint;
            if(other == null)
                return false;
            return base.Equals(other);
        }

        public override int GetHashCode ()
        {
            return base.GetHashCode ();
        }
    }
}

