//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    internal class WordPeripheralWrapper : IWordPeripheral
    {
        public WordPeripheralWrapper(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            this.read = read;
            this.write = write;
        }

        public ushort ReadWord(long offset)
        {
            return read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            write(offset, value);
        }

        public void Reset()
        {
        }

        private readonly BusAccess.WordReadMethod read;
        private readonly BusAccess.WordWriteMethod write;
    }
}

