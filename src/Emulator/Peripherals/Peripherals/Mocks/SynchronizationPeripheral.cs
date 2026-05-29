//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Concurrent;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class SynchronizationPeripheral : IDoubleWordPeripheral, IKnownSize, IUART
    {
        public SynchronizationPeripheral(IMachine machine)
        {
            this.Machine = machine;
        }

        public void ReleaseCpu(string cpu)
        {
            GetCpuReleasedEvent(cpu).Set();
        }

        public void ReleaseCpuOnAccess(string accessingCpu, string cpuToRelease)
        {
            GetCpusToReleaseOnAccess(accessingCpu).Enqueue(cpuToRelease);
        }

        public void UnhaltCpuOnAccess(string accessingCpu, string cpuToUnhalt)
        {
            GetCpusToUnhaltOnAccess(accessingCpu).Enqueue(cpuToUnhalt);
        }

        public uint ReadDoubleWord(long _)
        {
            BlockCurrentCpuIfRequested();

            return 0;
        }

        public void WriteDoubleWord(long _, uint value)
        {
            BlockCurrentCpuIfRequested();
            CharReceived?.Invoke((byte)value);
        }

        public void WaitForCpuAccess(string cpuName)
        {
            string coreCurrentlyAccessing = null;
            while(true)
            {
                coreCurrentlyAccessing = blockingCollection.Take();
                if(coreCurrentlyAccessing != cpuName)
                {
                    ReleaseCpu(coreCurrentlyAccessing);
                }
                else
                {
                    return;
                }
            }
        }

        public void Reset()
        {
            cpusToReleaseOnAccess.Clear();
            cpusToUnhaltOnAccess.Clear();
        }

        public void WriteChar(byte value)
        {
        }

        public bool BlockAccesses { get; set; } = true;

        public uint BaudRate => 0;

        public Parity ParityBit => Parity.None;

        public long Size => 0x4;

        public Bits StopBits => Bits.None;

        [field: Transient]
        public event Action<byte> CharReceived;

        public readonly IMachine Machine;

        private void WaitForCpuReleasedEvent(string cpuName)
        {
            GetCpuReleasedEvent(cpuName).WaitOne();
        }

        private string TriggerCpuAccessedEvent()
        {
            var cpuName = GetCurrentCpuName();
            blockingCollection.Add(cpuName);
            HandleCpuAccessActions(cpuName);
            return cpuName;
        }

        private void BlockCurrentCpuIfRequested()
        {
            if(!BlockAccesses)
            {
                return;
            }

            var cpuName = TriggerCpuAccessedEvent();
            WaitForCpuReleasedEvent(cpuName);
        }

        private string GetCurrentCpuName()
        {
            if(!Machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                throw new Exception("this method must be executed as a result of cpu access from translated code");
            }

            var currentCpuName = Machine.GetLocalName(cpu);
            return currentCpuName;
        }

        private AutoResetEvent GetCpuReleasedEvent(string cpuName)
        {
            return cpuReleasedEvents.GetOrAdd(cpuName, _ => new AutoResetEvent(false));
        }

        private ConcurrentQueue<string> GetCpusToReleaseOnAccess(string accessingCpu)
        {
            return cpusToReleaseOnAccess.GetOrAdd(accessingCpu, _ => new ConcurrentQueue<string>());
        }

        private ConcurrentQueue<string> GetCpusToUnhaltOnAccess(string accessingCpu)
        {
            return cpusToUnhaltOnAccess.GetOrAdd(accessingCpu, _ => new ConcurrentQueue<string>());
        }

        private void HandleCpuAccessActions(string accessingCpu)
        {
            if(cpusToUnhaltOnAccess.TryGetValue(accessingCpu, out var cpusToUnhalt))
            {
                while(cpusToUnhalt.TryDequeue(out var cpuToUnhalt))
                {
                    UnhaltCpu(cpuToUnhalt);
                }
            }

            if(cpusToReleaseOnAccess.TryGetValue(accessingCpu, out var cpusToRelease))
            {
                while(cpusToRelease.TryDequeue(out var cpuToRelease))
                {
                    ReleaseCpu(cpuToRelease);
                }
            }
        }

        private void UnhaltCpu(string cpuName)
        {
            if(!Machine.TryGetByName<ICPU>($"sysbus.{cpuName}", out var cpu))
            {
                throw new Exception($"CPU '{cpuName}' not found");
            }

            cpu.IsHalted = false;
        }

        private readonly BlockingCollection<string> blockingCollection = new();
        private readonly ConcurrentDictionary<string, AutoResetEvent> cpuReleasedEvents = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> cpusToReleaseOnAccess = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> cpusToUnhaltOnAccess = new();
    }
}
