//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MockPeripheralWithEnumAttribute : IBytePeripheral
    {
        public MockPeripheralWithEnumAttribute(MockEnum mockEnum = MockEnum.ValidValue, MockEnumWithAttribute mockEnumWithAttribute = MockEnumWithAttribute.ValidValue)
        {
            MockEnumValue = mockEnum;
            MockEnumWithAttributeValue = mockEnumWithAttribute;
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

        public MockEnum MockEnumValue { get; set; }
        public MockEnumWithAttribute MockEnumWithAttributeValue { get; set; }

        public enum MockEnum : byte
        {
            ValidValue = 1
        }

        [AllowAnyNumericalValue]
        public enum MockEnumWithAttribute : byte
        {
            ValidValue = 1
        }
    }
}
