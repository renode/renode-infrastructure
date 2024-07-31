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
    public abstract class BusRegistration : IBusRegistration
    {
        protected BusRegistration(ulong startingPoint, ulong offset = 0, ICPU cpu = null, ICluster<ICPU> cluster = null)
        {
            if(cpu != null && cluster != null)
            {
                throw new ConstructionException("CPU and cluster cannot be specified at the same time");
            }

            CPU = cpu;
            Cluster = cluster;
            Offset = offset;
            StartingPoint = startingPoint;
        }

        public ICPU CPU { get; }
        public ICluster<ICPU> Cluster { get; }
        public ulong Offset { get; set; }
        public ulong StartingPoint { get; set; }

        public virtual string PrettyString
        {
            get
            {
                return ToString();
            }
        }

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
