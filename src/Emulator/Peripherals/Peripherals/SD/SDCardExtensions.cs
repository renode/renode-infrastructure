//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using System;

namespace Antmicro.Renode.Peripherals.SD
{
    public static class SDCardExtensions
    {
        public static void SdCardFromFile(this Machine machine, string file, IPeripheralRegister<ISDDevice, NullRegistrationPoint> attachTo, bool persistent = true, long? size = null)
        {
            var card = new SDCard(file, size, persistent);
            attachTo.Register(card, NullRegistrationPoint.Instance);
            machine.SetLocalName(card, String.Format("SD card: {0}", file));
        }
    }
}

