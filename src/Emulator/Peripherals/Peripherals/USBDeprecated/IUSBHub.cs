//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public interface IUSBHub : IUSBPeripheral, IUSBHubBase
    {
        IUSBPeripheral GetDevice(byte port);
        IUSBHub Parent{ set; }
        byte NumberOfPorts { get; set; }
    }
}
