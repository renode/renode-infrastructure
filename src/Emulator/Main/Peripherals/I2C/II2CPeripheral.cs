//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.I2C
{
    public interface II2CPeripheral : IPeripheral
    {
        void Write(byte[] data);
        byte[] Read(int count = 1);
        void FinishTransmission();
    }
}

