//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public static class MassStorageExtensions
    {
        public static void PendriveFromFile(this IMachine machine, string file, string name, IPeripheralRegister<IUSBPeripheral, USBRegistrationPoint> attachTo, byte port, bool persistent = true)
        {
            // TODO: note that port is here (or is nondefault) only due to bug/deficiency in EHCI
            // i.e. that one cannot register by first free port
            var pendrive = new MassStorage(file, persistent: persistent);
            attachTo.Register(pendrive, new USBRegistrationPoint(port));
            machine.SetLocalName(pendrive, name);
        }
    }
}

