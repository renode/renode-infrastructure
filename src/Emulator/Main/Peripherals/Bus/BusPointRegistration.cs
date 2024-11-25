//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusPointRegistration : BusRegistration
    {
        public BusPointRegistration(ulong address, ulong offset = 0, IPeripheral cpu = null, ICluster<ICPU> cluster = null, StateMask? cpuState = null, string condition = null) : base(address, offset, cpu, cluster, cpuState, condition)
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

        public void RegisterForEachContext(Action<BusPointRegistration> register)
        {
            RegisterForEachContextInner(register, cpu => new BusPointRegistration(StartingPoint, Offset, cpu));
        }
    }
}

