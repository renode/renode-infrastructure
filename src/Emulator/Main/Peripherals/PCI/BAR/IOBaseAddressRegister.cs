//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI.BAR
{
    public class IOBaseAddressRegister : BaseAddressRegister
    {
        public IOBaseAddressRegister(uint requestedSize) : base(requestedSize)
        {
        }

        public override uint Value
        {
            get
            {
                return baseAddress | 1u;
            }
            set
            {
                BaseAddress = value;
            }
        }

        protected override uint AddressMask => ~0x3u;
    }
}