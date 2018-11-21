//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI.BAR
{
    public abstract class BaseAddressRegister
    {
        public BaseAddressRegister(uint requestedSize)
        {
            RequestedSize = requestedSize;
        }

        public abstract uint Value { get; set; }
        public uint BaseAddress
        {
            get { return baseAddress; }
            set { baseAddress = ((value & AddressMask) & ~(RequestedSize - 1)); }
        }

        public uint RequestedSize { get; }
        protected abstract uint AddressMask { get; }
        protected uint baseAddress;
    }
}