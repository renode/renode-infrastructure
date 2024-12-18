//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public abstract class BusRegistration : IBusRegistration, IConditionalRegistration
    {
        protected BusRegistration(ulong startingPoint, ulong offset = 0, IPeripheral cpu = null, ICluster<ICPU> cluster = null, StateMask? stateMask = null, string condition = null)
        {
            if(cpu != null && cluster != null)
            {
                throw new ConstructionException("CPU and cluster cannot be specified at the same time");
            }

            if(stateMask != null && condition != null)
            {
                throw new ConstructionException("State mask and condition cannot be specified at the same time");
            }

            Initiator = cpu;
            Cluster = cluster;
            StateMask = stateMask;
            Condition = condition;
            Offset = offset;
            StartingPoint = startingPoint;
        }

        public IPeripheral Initiator { get; }
        public ICluster<ICPU> Cluster { get; }
        public StateMask? StateMask { get; }
        public string Condition { get; }
        public ulong Offset { get; set; }
        public ulong StartingPoint { get; set; }

        public virtual string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as BusRegistration;
            if(other == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;

            return StartingPoint == other.StartingPoint && Offset == other.Offset && Initiator == other.Initiator && Cluster == other.Cluster
                && StateMask.Equals(other.StateMask) && Condition == other.Condition;
        }

        public override int GetHashCode()
        {
            return 17 * StartingPoint.GetHashCode() + 23 * Offset.GetHashCode() + 101 * (Initiator?.GetHashCode() ?? 0) + 397 * (Cluster?.GetHashCode() ?? 0)
                + 401 * (StateMask?.GetHashCode() ?? 0) + 409 * (Condition?.GetHashCode() ?? 0);
        }

        public abstract IConditionalRegistration WithInitiatorAndStateMask(IPeripheral initiator, StateMask mask);

        protected void RegisterForEachContextInner<T>(Action<T> register, Func<ICPU, T> registrationForCpuGetter)
            where T : BusRegistration
        {
            if(Cluster != null)
            {
                foreach(var cpu in Cluster.Clustered)
                {
                    register(registrationForCpuGetter(cpu));
                }
            }
            else
            {
                register((T)this);
            }
        }
    }
}
