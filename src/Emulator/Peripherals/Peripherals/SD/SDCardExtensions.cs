//
// Copyright (c) 2010-2024 Antmicro
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
        public static void SdCardFromFile(this IMachine machine, string file, IPeripheralRegister<DeprecatedSDCard, NullRegistrationPoint> attachTo, long size, bool persistent = true, string name = null)
        {
            var card = new DeprecatedSDCard(file, size, persistent);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdCardFromFile(this IMachine machine, string file, IPeripheralRegister<SDCard, NullRegistrationPoint> attachTo, long size, bool persistent = true, string name = null)
        {
            var card = new SDCard(file, size, persistent);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdCardFromFile(this IMachine machine, string file, IPeripheralRegister<ISPIPeripheral, NullRegistrationPoint> attachTo, long size, bool persistent = true, string name = null)
        {
            var card = new SDCard(file, size, persistent, spiMode: true);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void SdCardFromFile(this IMachine machine, string file, IPeripheralRegister<ISPIPeripheral, NumberRegistrationPoint<int>> attachTo, int port, long size, bool persistent = true, string name = null)
        {
            var card = new SDCard(file, size, persistent, spiMode: true);
            attachTo.Register(card, new NumberRegistrationPoint<int>(port));
            machine.SetLocalName(card, name ?? "sdCard");
        }

        public static void EmptySdCard(this IMachine machine, IPeripheralRegister<ISPIPeripheral, NumberRegistrationPoint<int>> attachTo, int port, long size, string name = null)
        {
            var card = new SDCard(size, spiMode: true);
            attachTo.Register(card, new NumberRegistrationPoint<int>(port));
            machine.SetLocalName(card, name ?? "sdCard");
        }
    }
}

