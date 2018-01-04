//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
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

        public bool Failed
        {
            get
            {
                return failed;
            }
        }

        public override uint ReadDoubleWord (long offset)
        {
            var value = Interlocked.Read (ref counter);
            var toWait = random.Next (spinWaitIterations);
            Thread.SpinWait (toWait);
            var exchanged = Interlocked.Exchange(ref counter, ++value);
            if(exchanged != value - 1)
            {
                failed = true;
            }
            return (uint)toWait;
        }

        private long counter;
        private bool failed;
        private readonly PseudorandomNumberGenerator random;
        private const int spinWaitIterations = 10000;
    }
}

