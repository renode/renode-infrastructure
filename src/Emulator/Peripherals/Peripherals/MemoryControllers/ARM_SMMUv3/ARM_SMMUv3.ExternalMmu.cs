//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public sealed class ARM_SMMUv3ExternalMmu : ExternalMmuBase, ISMMUv3StreamController, IDisposable
    {
        public ARM_SMMUv3ExternalMmu(ARM_SMMUv3 smmu, ICPUWithExternalMmu cpu) : base(cpu, windowsCount: 0, position: ExternalMmuPosition.None)
        {
            this.smmu = smmu;
            this.cpu = cpu;
            cpu.AddHookOnMmuFault(MmuFaultHook);
        }

        public void InvalidateTlb(ulong? virtualAddress = null)
        {
            if(virtualAddress is ulong va)
            {
                cpu.ResetMmuWindowsCoveringAddress(va);
                cpu.FlushTlbPage(va);
            }
            else
            {
                cpu.ResetAllMmuWindows();
                cpu.FlushTlb();
            }
        }

        public void Dispose()
        {
            cpu.RemoveHookOnMmuFault(MmuFaultHook);
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if(enabled == value)
                {
                    return;
                }
                enabled = value;
                cpu.EnableExternalWindowMmu(value ? SmmuPosition : ExternalMmuPosition.None);
                InvalidateTlb();
            }
        }

        // These enums actually have the same representation, so this could just be a cast.
        private static Privilege BusAccessPrivilegesToExternalMmuWindowPrivileges(BusAccessPrivileges p)
        {
            var result = Privilege.None;
            if(p.HasFlag(BusAccessPrivileges.Read))
            {
                result |= Privilege.Read;
            }
            if(p.HasFlag(BusAccessPrivileges.Write))
            {
                result |= Privilege.Write;
            }
            if(p.HasFlag(BusAccessPrivileges.Other))
            {
                result |= Privilege.Execute;
            }
            return result;
        }

        private ExternalMmuResult MmuFaultHook(ulong faultAddress, AccessType accessType, ulong? faultyWindowId, bool firstTry)
        {
            if(!firstTry)
            {
                // Permission fault happens when access is invalid, but a window was found.
                // If window was not found this is a different kind of fault (e.g. translation fault),
                // which is signaled in `GetWindowFromPageTable`
                if(faultyWindowId.HasValue)
                {
                    smmu.SignalPermissionFaultEvent(cpu, faultAddress, accessType);
                }
                return ExternalMmuResult.Fault;
            }

            smmu.NoisyLog("MMU fault 0x{0:x} {1} win={2}", faultAddress, accessType, faultyWindowId);

            var pageWindow = smmu.GetWindowFromPageTable(faultAddress, cpu, accessType);
            if(pageWindow == null)
            {
                return ExternalMmuResult.Fault;
            }

            var windowId = cpu.AcquireExternalMmuWindow(Privilege.All);
            cpu.SetMmuWindowStart(windowId, pageWindow.Start);
            cpu.SetMmuWindowEnd(windowId, pageWindow.End);
            cpu.SetMmuWindowAddend(windowId, (ulong)pageWindow.Offset);
            cpu.SetMmuWindowPrivileges(windowId, BusAccessPrivilegesToExternalMmuWindowPrivileges(pageWindow.Privileges));
            return ExternalMmuResult.NoFault;
        }

        private bool enabled;

        private readonly ARM_SMMUv3 smmu;
        private readonly ICPUWithExternalMmu cpu;

        private const ExternalMmuPosition SmmuPosition = ExternalMmuPosition.AfterInternal;
    }
}
