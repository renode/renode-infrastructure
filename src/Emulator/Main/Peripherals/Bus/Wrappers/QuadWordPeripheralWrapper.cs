//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    internal class QuadWordPeripheralWrapper : IQuadWordPeripheral
    {
        public QuadWordPeripheralWrapper(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            this.read = read;
            this.write = write;
        }

        public ulong ReadQuadWord(long offset)
        {
            return read(offset);
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            write(offset, value);
        }

        public void Reset()
        {
        }

        private readonly BusAccess.QuadWordReadMethod read;
        private readonly BusAccess.QuadWordWriteMethod write;
    }
}

