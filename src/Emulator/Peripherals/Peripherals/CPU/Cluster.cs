//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.CPU
{
    // A few helper methods to simplify the common operations on all cpus in the cluster.
    public static class ClusterExtensions
    {
        public static void SetPC(this Cluster cluster, ulong value)
        {
            foreach(var cpu in cluster.Clustered)
            {
                cpu.PC = value;
            }
        }
    }

    /// <summary>
    /// <see cref="Cluster"/> could be a generic class to accept any types derived from <see cref="ICPU"/>,
    /// but we wouldn't be able to use it in the platform description file (REPL), so it has a concrete type.
    /// </summary>
    public class Cluster : IPeripheralRegister<ICluster<TranslationCPU>, NullRegistrationPoint>, IPeripheralRegister<TranslationCPU, NullRegistrationPoint>, ICluster<TranslationCPU>, IHaltable
    {
        public Cluster(IMachine machine)
        {
            this.machine = machine;
        }

        public void Reset()
        {
            foreach(var clustered in Clustered)
            {
                clustered.Reset();
            }
        }

        public void Register(ICluster<TranslationCPU> cluster, NullRegistrationPoint registrationPoint)
        {
            // Intermediate clusters are registered only in the parent cluster,
            // so they can be accessed through the cluster's tree hierarchy.
            machine.RegisterAsAChildOf(this, cluster, NullRegistrationPoint.Instance);
            clusters.Add(cluster);
        }

        public void Unregister(ICluster<TranslationCPU> cluster)
        {
            machine.UnregisterAsAChildOf(this, cluster);
            clusters.Remove(cluster);
        }

        public void Register(TranslationCPU cpu, NullRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, cpu, NullRegistrationPoint.Instance);
            // Passing null will not register CPU on sysbus, but will do necessary cpu-specific actions.
            machine.SystemBus.Register(cpu, null);
            cpus.Add(cpu);
        }

        public void Unregister(TranslationCPU cpu)
        {
            // Sysbus will call a generic "Unregister" method which will handle
            // unregistering the CPU from a cluster on the machine level.
            machine.SystemBus.Unregister(cpu);
            cpus.Remove(cpu);
        }

        public bool IsHalted
        {
            set
            {
                foreach(var cpu in this.Clustered)
                {
                    cpu.IsHalted = value;
                }
            }
        }

        public IEnumerable<ICluster<TranslationCPU>> Clusters => clusters;

        public IEnumerable<TranslationCPU> Clustered => cpus.Concat(clusters.SelectMany(cluster => cluster.Clustered));

        private readonly List<ICluster<TranslationCPU>> clusters = new List<ICluster<TranslationCPU>>();
        private readonly List<TranslationCPU> cpus = new List<TranslationCPU>();

        private readonly IMachine machine;
    }
}