//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusMultiRegistration : BusRangeRegistration
    {
        public BusMultiRegistration(ulong address, ulong size, string region, IPeripheral cpu = null, ICluster<ICPU> cluster = null) : this(address, size, region, stateMask: null, cpu, cluster)
        {
        }

        public BusMultiRegistration(ulong address, ulong size, string region, string condition) : this(address, size, region, stateMask: null, condition: condition)
        {
        }

        public string ConnectionRegionName { get; private set; }
        public override string PrettyString { get { return ToString(); } }

        public override bool Equals(object obj)
        {
            var other = obj as BusMultiRegistration;
            if(other == null)
            {
                return false;
            }
            if(!base.Equals(obj))
            {
                return false;
            }
            return ConnectionRegionName == other.ConnectionRegionName;
        }

        public override string ToString()
        {
            return base.ToString() + $" [region: {ConnectionRegionName}]";
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 * base.GetHashCode() + 101 * ConnectionRegionName.GetHashCode();
            }
        }

        public override IConditionalRegistration WithInitiatorAndStateMask(IPeripheral initiator, StateMask mask)
        {
            return new BusMultiRegistration(StartingPoint, Range.Size, ConnectionRegionName, mask, cpu: initiator);
        }

        public void RegisterForEachContext(Action<BusMultiRegistration> register)
        {
            RegisterForEachContextInner(register, cpu => new BusMultiRegistration(Range.StartAddress, Range.Size, ConnectionRegionName, stateMask: StateMask, cpu));
        }

        private BusMultiRegistration(ulong address, ulong size, string region, StateMask? stateMask, IPeripheral cpu = null, ICluster<ICPU> cluster = null, string condition = null) : base(new Range(address, size), stateMask, 0, cpu, cluster, condition)
        {
            if(string.IsNullOrWhiteSpace(region))
            {
                throw new ConstructionException("'Region' parameter cannot be null or empty.");
            }
            ConnectionRegionName = region;
        }
    }
}

