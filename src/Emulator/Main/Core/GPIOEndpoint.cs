//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core
{
    public sealed class GPIOEndpoint
    {
        public GPIOEndpoint(IGPIOReceiver receiver, int number)
        {
            Receiver = receiver;
            Number = number;
        }

        public IGPIOReceiver Receiver { get; private set; }
        public int Number { get; private set; }
    }
}

