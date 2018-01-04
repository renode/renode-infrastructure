//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class CPURegistrationPoint : IRegistrationPoint
    {
        public CPURegistrationPoint(int? slot = null)
        {
            Slot = slot;
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
            return string.Format("Slot: {0}", Slot);
        }

        public int? Slot { get; private set; }

        public override bool Equals(object obj)
        {
            var other = obj as CPURegistrationPoint;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;

            return Slot == other.Slot;
        }
        

        public override int GetHashCode()
        {
            unchecked
            {
                return Slot.GetHashCode();
            }
        }
        
    }
}

