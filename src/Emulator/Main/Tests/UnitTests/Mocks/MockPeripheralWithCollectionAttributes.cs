//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MockPeripheralWithCollectionAttributes : IBytePeripheral
    {
        public MockPeripheralWithCollectionAttributes(List<int> mockIntList = null, List<string> mockStringList = null, List<ICPU> mockCpuList = null, int[] mockIntArray = null)
        {
            MockIntList = mockIntList;
            MockStringList = mockStringList;
            MockCpuList = mockCpuList;
            MockIntArray = mockIntArray;
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

        public List<int> MockIntList { get; set; }

        public List<string> MockStringList { get; set; }

        public List<ICPU> MockCpuList { get; set; }

        public int[] MockIntArray { get; set; }
    }
}