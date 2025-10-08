//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class Ulpi : IPhysicalLayer<byte>
    {
        public Ulpi(long baseAddress)
        {
            BaseAddress = baseAddress;
        }

        public byte Read(byte offset)
        {
            switch((Registers)offset)
            {
            case Registers.VendorIDLow:
                LastReadValue = (byte)(VendorID & 0xFF);
                break;
            case Registers.VendorIDHigh:
                LastReadValue = (byte)((VendorID & 0xFF00) >> 8);
                break;
            case Registers.ProductIDLow:
                LastReadValue = (byte)(ProductID & 0xFF);
                break;
            case Registers.ProductIDHigh:
                LastReadValue = (byte)((ProductID & 0xFF00) >> 8);
                break;
            case Registers.Scratch:
                LastReadValue = scratchRegister;
                break;
            default:
                this.LogUnhandledRead(offset);
                LastReadValue = 0;
                break;
            }

            return LastReadValue;
        }

        public void Write(byte offset, byte value)
        {
            switch((Registers)offset)
            {
            case Registers.Scratch:
                scratchRegister = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            scratchRegister = 0;
            LastReadValue = 0;
        }

        public long BaseAddress { get; private set; }

        public byte LastReadValue { get; private set; }

        private byte scratchRegister;

        private const uint VendorID = 0x04cc;
        private const uint ProductID = 0x1504;

        private enum Registers : uint
        {
            VendorIDLow = 0x0,
            VendorIDHigh = 0x1,
            ProductIDLow = 0x2,
            ProductIDHigh = 0x3,
            Scratch = 0x16
        }
    }
}