//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Tests.UnitTests.Mocks
{
    public class MachineTestPeripheral : IBytePeripheral
    {
        /*
        The mock peripheral is used in unit tests to verify if Renode CreationDriver can distinguish between parameter of Machine type (for which user cannot manually assign a value) 
        and parameter that is named machine, but of non-machine type (for which manual assignment is correct)
        */
        public MachineTestPeripheral(Machine mach, int machine)
        {
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
    }
}