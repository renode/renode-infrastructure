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
    public class MockPeripheralWithStringAttribute : IBytePeripheral
    {
        public MockPeripheralWithStringAttribute(string mockString)
        {
            MockString = mockString;
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

        public string MockString { get; set; }

    }
}
