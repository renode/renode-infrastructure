//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Input
{
    public interface IPS2Peripheral : IPeripheral
    {
        byte Read();
        void Write(byte value);
        IPS2Controller Controller { get; set; }
    }
}

