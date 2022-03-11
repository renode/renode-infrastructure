//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class IEEE802_15_4Extensions
    {
        public static void CreateIEEE802_15_4Medium(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new IEEE802_15_4Medium(), name);
        }
    }

    public sealed class IEEE802_15_4Medium : WirelessMedium 
    {
        public IEEE802_15_4Medium() {}
    }
}
