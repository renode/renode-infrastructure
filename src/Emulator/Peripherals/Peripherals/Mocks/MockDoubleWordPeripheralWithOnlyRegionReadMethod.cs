//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class MockDoubleWordPeripheralWithOnlyRegionReadMethod : IDoubleWordPeripheral
    {
        public MockDoubleWordPeripheralWithOnlyRegionReadMethod()
        {
        }

        public void Reset()
        {
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
        }

        // Write method is intentionally omitted to test error checking logic.
        [ConnectionRegion("region")]
        public uint ReadDoubleWordFromRegion(long offset)
        {
            return 0;
        }
    }
}
