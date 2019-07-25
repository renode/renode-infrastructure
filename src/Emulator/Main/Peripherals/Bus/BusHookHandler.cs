//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusHookHandler
    {
        public BusHookHandler(Action<ICpuSupportingGdb, ulong, SysbusAccessWidth> action, SysbusAccessWidth width)
        {
            this.action = action;
            this.width = width;
            Enabled = true;
        }

        public void Invoke(ICpuSupportingGdb cpu, ulong currentAddress, SysbusAccessWidth currentWidth)
        {
            if((currentWidth & width) != 0)
            {
                action(cpu, currentAddress, currentWidth);
            }
        }

        public bool ContainsAction(Action<ICpuSupportingGdb, ulong, SysbusAccessWidth> actionToTest)
        {
            return action == actionToTest;
        }

        public bool Enabled { get; set; }

        private readonly Action<ICpuSupportingGdb, ulong, SysbusAccessWidth> action;
        private readonly SysbusAccessWidth width;
    }
}

