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
    public class MockPeripheralWithNumericalAttrubute : IBytePeripheral
    {
        public MockPeripheralWithNumericalAttrubute(int mockInt)
        {
            MockInt = mockInt;
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

        public int MockInt { get; set; }
       
    }
}
