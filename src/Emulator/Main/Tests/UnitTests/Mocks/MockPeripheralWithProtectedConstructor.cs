//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockPeripheralWithProtectedConstructor : IDoubleWordPeripheral, IKnownSize
    {
        protected MockPeripheralWithProtectedConstructor()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public void Reset()
        {
        }

        public void WriteDoubleWord(long offset, uint value)
        {
        }

        public long Size => 4;
    }
}


