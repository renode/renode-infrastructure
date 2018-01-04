//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    internal class DoubleWordPeripheralWrapper : IDoubleWordPeripheral
    {
        public DoubleWordPeripheralWrapper(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            this.read = read;
            this.write = write;
        }

        public uint ReadDoubleWord(long offset)
        {
            return read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            write(offset, value);
        }

        public void Reset()
        {
        }

        private readonly BusAccess.DoubleWordReadMethod read;
        private readonly BusAccess.DoubleWordWriteMethod write;
    }
}

