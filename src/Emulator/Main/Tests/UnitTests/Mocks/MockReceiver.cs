//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockReceiver : IGPIOReceiver, IBytePeripheral
    {
        public MockReceiver()
        {
            IRQ = new GPIO();
        }

        public void Reset()
        {
        }

        public void OnGPIO(int number, bool value)
        {
        }

        public GPIO IRQ
        {
            get;
            set;
        }

        public byte ReadByte(long offset)
        {
            throw new System.NotImplementedException();
        }

        public void WriteByte(long offset, byte value)
        {
            throw new System.NotImplementedException();
        }
    }

}
