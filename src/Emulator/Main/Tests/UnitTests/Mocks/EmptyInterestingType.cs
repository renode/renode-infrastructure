//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class EmptyInterestingType : IPeripheralRegister<ICPU, NullRegistrationPoint>
    {
        // note: Register and Unregister methods are empty, because the purpose of this type is to test
        // casting of types (that is why this type does not implement IPeripheral) and they will not be used

        public void Register(ICPU peripheral, NullRegistrationPoint registrationPoint)
        {
        }

        public void Unregister(ICPU peripheral)
        {
        }
    }
}
