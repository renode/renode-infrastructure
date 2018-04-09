//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    internal class BusHookHandler
    {
        public BusHookHandler(Action<long, SysbusAccessWidth> action, SysbusAccessWidth width, Action updateContext)
        {
            this.action = action;
            this.width = width;
            this.updateContext = updateContext;
        }        

        public void Invoke(long currentAddress, SysbusAccessWidth currentWidth)
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

        public bool ContainsAction(Action<long, SysbusAccessWidth> actionToTest)
        {
            return action == actionToTest;
        }

        private readonly Action<long, SysbusAccessWidth> action;
        private readonly SysbusAccessWidth width;
        private readonly Action updateContext;
    }
}

