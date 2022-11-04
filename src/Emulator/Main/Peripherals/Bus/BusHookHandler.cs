//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public delegate void BusHookDelegate(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth access, ulong value);

    public class BusHookHandler
    {
        public BusHookHandler(BusHookDelegate action, SysbusAccessWidth width)
        {
            this.action = action;
            this.width = width;
            Enabled = true;
        }

        public void Invoke(ICpuSupportingGdb cpu, ulong currentAddress, SysbusAccessWidth currentWidth, ulong value = 0)
        {
            if((currentWidth & width) != 0)
            {
                action(cpu, currentAddress, currentWidth, value);
            }
        }

        public bool ContainsAction(BusHookDelegate actionToTest)
        {
            return action == actionToTest;
        }

        public bool Enabled { get; set; }

        private readonly BusHookDelegate action;
        private readonly SysbusAccessWidth width;
    }
}

