//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class PULP_EventController : IIRQController
    {
        public PULP_EventController(PULP_InterruptController parent)
        {
            this.parent = parent;
        }

        public void OnGPIO(int number, bool value)
        {
            parent.OnEvent(number, value);
        }

        public void Reset()
        {
            // intentionally left blank
        }

        private readonly PULP_InterruptController parent;
    }
}
