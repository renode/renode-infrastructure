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
        public BusHookHandler(Action<long, Width> action, Width width, Action updateContext)
        {
            this.action = action;
            this.width = width;
            this.updateContext = updateContext;
        }        

        public void Invoke(long currentAddress, Width currentWidth)
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

        public bool ContainsAction(Action<long, Width> actionToTest)
        {
            return action == actionToTest;
        }

        private readonly Action<long, Width> action;
        private readonly Width width;
        private readonly Action updateContext;
    }
}

