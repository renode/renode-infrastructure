﻿//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MockPeripheralWithBoolAttribute : IBytePeripheral
    {
        public MockPeripheralWithBoolAttribute(bool mockBool)
        {
            MockBool = mockBool;
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

        public bool MockBool { get; set; }
    }
}