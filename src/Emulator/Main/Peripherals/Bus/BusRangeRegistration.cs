//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.CPU;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusRangeRegistration : BusRegistration
    {
        public BusRangeRegistration(Range range, ulong offset = 0, IPeripheral cpu = null, ICluster<ICPU> cluster = null) : this(range, stateMask: null, offset, cpu, cluster)
        {
        }

        public BusRangeRegistration(Range range, string condition, ulong offset = 0) : this(range, stateMask: null, offset, condition: condition)
        {
        }

        public BusRangeRegistration(ulong address, ulong size, ulong offset = 0, IPeripheral cpu = null, ICluster<ICPU> cluster = null) : this(new Range(address, size), stateMask: null, offset, cpu, cluster)
        {
        }

        public BusRangeRegistration(ulong address, ulong size, string condition, ulong offset = 0) : this(new Range(address, size), stateMask: null, offset, condition: condition)
        {
        }

        public override string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public override string ToString()
        {
            var result = Range.ToString();
            if(Offset != 0)
            {
                result += $" with offset 0x{Offset:X}";
            }
            if(Initiator != null)
            {
                result += $" for core {Initiator}";
            }
            if(Condition != null)
            {
                result += $" with condition \"{Condition}\"";
            }
            return result;
        }

        public static implicit operator BusRangeRegistration(Range range)
        {
            return new BusRangeRegistration(range);
        }

        public Range Range { get; set; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && Range.Size == ((BusRangeRegistration)obj).Range.Size;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * base.GetHashCode() + 101 * Range.Size.GetHashCode();
            }
        }

        public override IConditionalRegistration WithInitiatorAndStateMask(IPeripheral initiator, StateMask mask)
        {
            return new BusRangeRegistration(Range, mask, Offset, initiator, condition: Condition);
        }

        public void RegisterForEachContext(Action<BusRangeRegistration> register)
        {
            RegisterForEachContextInner(register, cpu => new BusRangeRegistration(Range, StateMask, Offset, cpu));
        }

        protected BusRangeRegistration(Range range, StateMask? stateMask, ulong offset = 0, IPeripheral cpu = null, ICluster<ICPU> cluster = null, string condition = null) : base(range.StartAddress, offset, cpu, cluster, stateMask, condition)
        {
            Range = range;
        }
    }
}

