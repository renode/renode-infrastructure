//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MockPeripheralWithRangeAttribute : IBytePeripheral
    {
        public MockPeripheralWithRangeAttribute(Range mockRange)
        {
            MockRange = mockRange;
        }

        public void Reset()
        {
        }

        public byte ReadByte(long offset)
        {
            throw new NotImplementedException();
        }

        public void WriteByte(long offset, byte value)
        {
            throw new NotImplementedException();
        }

        public Range MockRange { get; set; }

    }
}
