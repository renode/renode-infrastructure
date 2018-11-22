//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.USB
{
    public interface IUSBDevice : IPeripheral
    {
        USBDeviceCore USBCore { get; }
    }
}