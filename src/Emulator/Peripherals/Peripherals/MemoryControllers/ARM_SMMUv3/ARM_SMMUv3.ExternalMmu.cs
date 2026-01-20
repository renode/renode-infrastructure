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

using static Antmicro.Renode.Peripherals.Bus.WindowMMUBusController;

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
                if(!enabled)
                {
                    skippedLastFault = false;
                }
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
            // Simplified description of the flow used for signaling asynchronous aborts:
            // * SMMU transaction faults, an event is recorded if requested and the instruction
            //   that caused the fault is restarted.
            // * Restarting the instruction allows the CPU to service the event queue interrupt
            //   if it is pending.
            // * After returning from the interrupt (or immediately if no interrupt is pending)
            //   the faulting instruction is reexecuted, so it should fault again and this time
            //   an external abort is triggered on the CPU.
            // * If a different SMMU fault happens before the external abort is triggered on the CPU
            //   the external abort is triggered immediately.

            if(!firstTry)
            {
                if(skippedLastFault)
                {
                    // Second fault happend while accessing the same address - signal an external abort.
                    skippedLastFault = false;
                    return ExternalMmuResult.ExternalAbort;
                }

                // Permission fault happens when access is invalid, but a window was found.
                // If window was not found this is a different kind of fault (e.g. translation fault),
                // which is signaled in `GetWindowFromPageTable`
                if(faultyWindowId.HasValue)
                {
                    smmu.SignalPermissionFaultEvent(cpu, faultAddress, accessType);
                }

                // No fault is requested here to allow the CPU to service the Event IRQ signal.
                // This is to simulate an asynchronous external data/prefetch abort exception.
                skippedLastFault = true;
                return ExternalMmuResult.NoFault;
            }

            smmu.NoisyLog("MMU fault 0x{0:x} {1} win={2}", faultAddress, accessType, faultyWindowId);

            MMUWindow pageWindow = null;
            // Don't enqueue events if the last fault was skipped to prevent
            // the same event from being enqueued twice.
            using(smmu.BlockEventQueues(block: skippedLastFault))
            {
                pageWindow = smmu.GetWindowFromPageTable(faultAddress, cpu, accessType);
            }
            if(pageWindow == null)
            {
                return ExternalMmuResult.ExternalAbort;
            }

            var windowId = cpu.AcquireExternalMmuWindow(Privilege.All);
            cpu.SetMmuWindowStart(windowId, pageWindow.Start);
            cpu.SetMmuWindowEnd(windowId, pageWindow.End);
            cpu.SetMmuWindowAddend(windowId, (ulong)pageWindow.Offset);
            cpu.SetMmuWindowPrivileges(windowId, BusAccessPrivilegesToExternalMmuWindowPrivileges(pageWindow.Privileges));
            return ExternalMmuResult.NoFault;
        }

        private bool enabled;
        private bool skippedLastFault;

        private readonly ARM_SMMUv3 smmu;
        private readonly ICPUWithExternalMmu cpu;

        private const ExternalMmuPosition SmmuPosition = ExternalMmuPosition.AfterInternal;
    }
}
