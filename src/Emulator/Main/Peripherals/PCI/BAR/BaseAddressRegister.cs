//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.PCI.BAR
{
    public abstract class BaseAddressRegister
    {
        public BaseAddressRegister(uint requestedSize)
        {
            RequestedSize = requestedSize;
        }

        public uint BaseAddress
        {
            get { return RequestedSize != 0 ? baseAddress : 0; }
            set { baseAddress = ((value & AddressMask) & ~(RequestedSize - 1)); }
        }

        //Base Address Register only implements as many bits as are necessary to decode the block size that it represents.
        //When RequestedSize is equal to zero, the BAR is not used and BaseAdress should always return 0.
        public uint RequestedSize { get; }

        public abstract uint Value { get; set; }

        protected abstract uint AddressMask { get; }

        protected uint baseAddress;
    }
}