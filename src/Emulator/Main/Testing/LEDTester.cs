//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using System.Threading;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Testing
{
    public static class LEDTesterExtenions
    {
        public static void CreateLEDTester(this Emulation emulation, string name, ILed led)
        {
            emulation.ExternalsManager.AddExternal(new LEDTester(led), name);
        }
    }

    public class LEDTester : IExternal
    {
        public LEDTester(ILed led)
        {
            this.led = led;
        }

        public LEDTester AssertState(bool state, int timeout = 0)
        {
            var ev = new ManualResetEvent(false);
            var method = (Action<ILed, bool>)((s, o) => ev.Set());

            try
            {
                if(timeout != 0)
                {
                    led.StateChanged += method;
                }

                if(led.State != state && !TimeoutExecutor.WaitForEvent(ev, timeout))
                {
                    throw new InvalidOperationException("LED assertion not met.");
                }
            }
            finally
            {
                led.StateChanged -= method;
            }

            return this;
        }

        private readonly ILed led;
    }
}

