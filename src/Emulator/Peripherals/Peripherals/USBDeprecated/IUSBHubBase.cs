//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public interface IUSBHubBase : IPeripheralRegister<IUSBHub, USBRegistrationPoint>,  IPeripheralContainer<IUSBPeripheral, USBRegistrationPoint>
    {
         event Action <uint> Connected ;
         event Action <uint,uint> Disconnected ;
         event Action <IUSBHub> RegisterHub ;
         event Action <IUSBPeripheral> ActiveDevice ;
    }
}
