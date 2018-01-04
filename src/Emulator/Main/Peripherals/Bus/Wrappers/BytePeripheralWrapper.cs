//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    internal class BytePeripheralWrapper : IBytePeripheral
    {
        public BytePeripheralWrapper(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            this.read = read;
            this.write = write;
        }

        public byte ReadByte(long offset)
        {
            return read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            write(offset, value);
        }

        public void Reset()
        {
        }

        private readonly BusAccess.ByteReadMethod read;
        private readonly BusAccess.ByteWriteMethod write;
    }
}

