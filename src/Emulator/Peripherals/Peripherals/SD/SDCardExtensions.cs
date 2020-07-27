//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.SPI;
﻿using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using System;

namespace Antmicro.Renode.Peripherals.SD
{
    public static class SDCardExtensions
    {
        public static void SdCardFromFile(this Machine machine, string file, IPeripheralRegister<DeprecatedSDCard, NullRegistrationPoint> attachTo, bool persistent = true, long? size = null, string name = null)
        {
            var card = new DeprecatedSDCard(file, size, persistent);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdCardFromFile(this Machine machine, string file, IPeripheralRegister<SDCard, NullRegistrationPoint> attachTo, bool persistent = true, long? size = null, string name = null)
        {
            var card = new SDCard(file, size, persistent);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdCardFromFile(this Machine machine, string file, IPeripheralRegister<ISPIPeripheral, NullRegistrationPoint> attachTo, bool persistent = true, long? size = null, string name = null)
        {
            var card = new SDCard(file, size, persistent, spiMode: true);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdhcCardFromFile(this Machine machine, string file, IPeripheralRegister<SDCard, NullRegistrationPoint> attachTo, bool persistent = true, long? size = null, string name = null)
        {
            var card = new SDCard(file, size, persistent, highCapacityMode: true);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }
    }
}

