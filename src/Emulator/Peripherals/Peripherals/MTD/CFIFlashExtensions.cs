//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using System.IO;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.MTD
{
    public static class CFIFlashExtensions
    {
        public static void CFIFlashFromFile(this IMachine machine, string fileName, ulong whereToRegister, string name, SysbusAccessWidth busWidth = SysbusAccessWidth.DoubleWord, bool nonPersistent = false, int? size = null)
        {
            CFIFlash flash;
            try
            {
                flash = new CFIFlash(fileName, size, busWidth, nonPersistent);
            }
            catch(Exception e)
            {
                throw new ConstructionException(String.Format("Could not create object of type {0}", typeof(CFIFlash).Name), e);
            }
            machine.SystemBus.Register(flash, new BusPointRegistration(whereToRegister));
            machine.SetLocalName(flash, name);
        }
    }
}

