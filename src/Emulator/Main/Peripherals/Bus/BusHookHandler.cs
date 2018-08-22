//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusHookHandler
    {
        public BusHookHandler(Action<ulong, SysbusAccessWidth> action, SysbusAccessWidth width, Action updateContext)
        {
            this.action = action;
            this.width = width;
            this.updateContext = updateContext;
            Enabled = true;
        }

        public void Invoke(ulong currentAddress, SysbusAccessWidth currentWidth)
        {
            if(updateContext != null)
            {
                updateContext();
            }
            if((currentWidth & width) != 0)
            {
                action(currentAddress, currentWidth);
            }
        }

        public bool ContainsAction(Action<ulong, SysbusAccessWidth> actionToTest)
        {
            return action == actionToTest;
        }

        public bool Enabled { get; set; }

        private readonly Action<ulong, SysbusAccessWidth> action;
        private readonly SysbusAccessWidth width;
        private readonly Action updateContext;
    }
}

