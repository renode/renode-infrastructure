//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IGaislerAHB : IPeripheral
    {
        uint GetVendorID();
        uint GetDeviceID();
        bool IsMaster();
        GaislerAHBPlugAndPlayRecord.SpaceType GetSpaceType();
    }
}

