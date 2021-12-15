//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusPerCorePointRegistration : BusPointRegistration, IPerCoreRegistration 
    {
        public BusPerCorePointRegistration(ulong address, ICPU cpu, ulong offset = 0) : base(address, offset)
        {
            CPU = cpu;
        }

        public ICPU CPU { get; }

        public BusPerCoreRangeRegistration ToBusPerCoreRangeRegistration(ulong size)
        {
            return new BusPerCoreRangeRegistration(StartingPoint, size, CPU, Offset);
        }

        public override string ToString()
        {
            return $"{base.ToString()} per core {CPU}";
        }
        
        public override bool Equals(object obj)
        {
            var other = obj as BusPerCorePointRegistration;
            if(other == null)
            {
                return false;
            }
            if(ReferenceEquals(this, obj))
            {
                return true;
            }
            if(!base.Equals(obj))
            {
                return false;
            }
            return CPU == other.CPU;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return base.GetHashCode() + 101 * CPU.GetHashCode();
            }
        }        
    }
}

