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
        public BusHookHandler(Action<ulong, Width> action, Width width, Action updateContext)
        {
            this.action = action;
            this.width = width;
            this.updateContext = updateContext;
        }        

        public void Invoke(ulong currentAddress, Width currentWidth)
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

        public bool ContainsAction(Action<ulong, Width> actionToTest)
        {
            return action == actionToTest;
        }

        private readonly Action<ulong, Width> action;
        private readonly Width width;
        private readonly Action updateContext;
    }
}

