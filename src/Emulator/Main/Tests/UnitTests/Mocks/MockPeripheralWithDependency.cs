//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockPeripheralWithDependency : IPeripheral
    {
        public MockPeripheralWithDependency(IPeripheral other = null, bool throwException = false)
        {
            if(throwException)
            {
                throw new ConstructionException("Fake exception");
            }
        }

        public void Reset()
        {
            
        }
    }
}
