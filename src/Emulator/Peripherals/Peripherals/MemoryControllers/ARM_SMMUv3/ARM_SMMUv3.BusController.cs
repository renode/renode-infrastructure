//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class ARM_SMMUv3BusController : WindowMMUBusController, ISMMUv3StreamController
    {
        // TODO: Fault handling
        public ARM_SMMUv3BusController(ARM_SMMUv3 smmu, IBusController parentController) : base(smmu, parentController)
        {
            this.smmu = smmu;
            locker = new object();
        }

        public void InvalidateTlb(ulong? virtualAddress = null)
        {
            if(!virtualAddress.HasValue)
            {
                Windows.Clear();
                return;
            }

            lock(locker)
            {
                if(virtualAddress is ulong va && TryFindWindowIndex(va, out var index))
                {
                    Windows.RemoveAt(index);
                }
            }
        }

        public bool Enabled { get; set; }

        protected override bool ValidateOperation(ref ulong address, BusAccessPrivileges accessType, IPeripheral context = null)
        {
            if(!Enabled)
            {
                return true;
            }

            var accessKind = BusPrivilegesToAccessType(accessType);
            var windowFound = TryFindWindowIndex(address, out var _);
            if(!windowFound)
            {
                var win = smmu.GetWindowFromPageTable(address, context, accessKind);
                if(win != null)
                {
                    windowFound = true;
                    Windows.Add(win);
                }
            }

            var operationValid = base.ValidateOperation(ref address, accessType, context);
            if(!operationValid && windowFound)
            {
                // Permission fault happens when access is invalid, but a window was found.
                // If window was not found this is a different kind of fault (e.g. translation fault),
                // which is signaled in `GetWindowFromPageTable`
                smmu.SignalPermissionFaultEvent(context, address, accessKind);
            }

            return operationValid;
        }

        private AccessType BusPrivilegesToAccessType(BusAccessPrivileges busAccess)
        {
            if(busAccess.HasFlag(BusAccessPrivileges.Read))
            {
                return AccessType.Read;
            }
            else if(busAccess.HasFlag(BusAccessPrivileges.Write))
            {
                return AccessType.Write;
            }
            else if(busAccess.HasFlag(BusAccessPrivileges.Other))
            {
                return AccessType.Execute;
            }

            smmu.ErrorLog("Unexpected bus access type: {0}, falling back to: {1}", busAccess, AccessType.Read);
            return AccessType.Read;
        }

        private readonly ARM_SMMUv3 smmu;
        private readonly object locker;
    }
}
