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
    public class MockIrqSender : IBytePeripheral
    {
        public MockIrqSender()
        {
            Irq = new GPIO();
        }

        public GPIO Irq { get; set; }

        public void Reset()
        {
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

