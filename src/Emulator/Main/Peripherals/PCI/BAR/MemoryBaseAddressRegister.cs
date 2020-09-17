//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI.BAR
{
    public class MemoryBaseAddressRegister : BaseAddressRegister
    {
        public MemoryBaseAddressRegister(uint requestedSize, BarType barType, bool prefetchable) : base(requestedSize)
        {
            this.barType = barType;
            this.prefetchable = prefetchable;
        }

        public override uint Value
        {
            get
            {
                return baseAddress | ((prefetchable ? 1u : 0u) << 3) | ((uint)barType << 1);
            }
            set
            {
                BaseAddress = value;
            }
        }

        protected override uint addressMask => ~0xFu;

        private readonly bool prefetchable;
        private readonly BarType barType;

        public enum BarType
        {
            LocateIn32Bit = 0,
            LocateIn64Bit = 2
        }
    }
}