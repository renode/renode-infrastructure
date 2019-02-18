//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MockPeripheralUsingReferenceAttribute : IBytePeripheral
    {
        public MockPeripheralUsingReferenceAttribute(Antmicro.Renode.Peripherals.IPeripheral mockReference)
        {
            MockReference = mockReference;
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

        public Peripherals.IPeripheral MockReference { get; set; }

    }
}
