//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockPeripheralWithDependency : IPeripheral
    {
        // We have to keep prameter names for nunit tests which instatiate Mock dynmically
        // and use named arguments
#pragma warning disable IDE0060
        public MockPeripheralWithDependency(IPeripheral other = null, bool throwException = false)
        {
            if(throwException)
            {
                throw new ConstructionException("Fake exception");
            }
        }
#pragma warning restore IDE0060

        public void Reset()
        {
        }
    }
}