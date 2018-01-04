//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests.Mocks
{
    [GPIO(NumberOfInputs = 5)]
    public class MockReceiverConstrained : IGPIOReceiver
    {
        public void Reset()
        {
        }

        public void OnGPIO(int number, bool value)
        {

        }
    }
}
