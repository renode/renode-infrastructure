//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;

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

            if(!TryFindWindowIndex(address, out var index))
            {
                var win = smmu.GetWindowFromPageTable(address, context);
                if(win != null)
                {
                    Windows.Add(win);
                }
            }
            return base.ValidateOperation(ref address, accessType, context);
        }

        private readonly ARM_SMMUv3 smmu;
        private readonly object locker;
    }
}