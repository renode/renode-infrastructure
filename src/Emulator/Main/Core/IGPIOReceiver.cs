//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public interface IGPIOReceiver : IPeripheral
    {
        void OnGPIO(int number, bool value);
    }
}

