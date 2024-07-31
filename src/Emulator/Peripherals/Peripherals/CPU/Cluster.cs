//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.CPU
{
    // A few helper methods to simplify the common operations on all cpus in the cluster.
    public static class ClusterExtensions
    {
        public static void SetIsHalted(this Cluster cluster, bool value)
        {
            foreach(var cpu in cluster.Clustered)
            {
                cpu.IsHalted = value;
            }
        }

        public static void SetPC(this Cluster cluster, ulong value)
        {
            foreach (var cpu in cluster.Clustered)
            {
                cpu.PC = value;
            }
        }
    }

    /// <summary>
    /// <see cref="Cluster"/> could be a generic class to accept any types derived from <see cref="ICPU"/>,
    /// but we wouldn't be able to use it in the platform description file (REPL), so it has a concrete type.
    /// </summary>
    public class Cluster : IPeripheralRegister<ICluster<TranslationCPU>, NullRegistrationPoint>, IPeripheralRegister<TranslationCPU, NullRegistrationPoint>, ICluster<TranslationCPU>
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
            // CPUs are registered both on sysbus and in the parent cluster,
            // so they can be accessed either directly or through the cluster's tree hierarchy.
            machine.RegisterAsAChildOf(this, cpu, NullRegistrationPoint.Instance);
            machine.SystemBus.Register(cpu, new CPURegistrationPoint());
            cpus.Add(cpu);
        }

        public void Unregister(TranslationCPU cpu)
        {
            machine.UnregisterAsAChildOf(this, cpu);
            machine.SystemBus.Unregister(cpu);
            cpus.Remove(cpu);
        }

        public IEnumerable<ICluster<TranslationCPU>> Clusters => clusters;

        public IEnumerable<TranslationCPU> Clustered => cpus.Concat(clusters.SelectMany(cluster => cluster.Clustered));

        private readonly List<ICluster<TranslationCPU>> clusters = new List<ICluster<TranslationCPU>>();
        private readonly List<TranslationCPU> cpus = new List<TranslationCPU>();

        private readonly IMachine machine;
    }
}
