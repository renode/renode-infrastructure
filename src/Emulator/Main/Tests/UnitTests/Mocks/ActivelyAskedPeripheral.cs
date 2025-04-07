//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Threading;

using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class ActivelyAskedPeripheral : EmptyPeripheral
    {
        public ActivelyAskedPeripheral()
        {
            random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        }

        public override uint ReadDoubleWord(long offset)
        {
            var value = Interlocked.Read (ref counter);
            var toWait = random.Next (SpinWaitIterations);
            Thread.SpinWait(toWait);
            var exchanged = Interlocked.Exchange(ref counter, ++value);
            if(exchanged != value - 1)
            {
                failed = true;
            }
            return (uint)toWait;
        }

        public bool Failed
        {
            get
            {
                return failed;
            }
        }

        private long counter;
        private bool failed;
        private readonly PseudorandomNumberGenerator random;
        private const int SpinWaitIterations = 10000;
    }
}