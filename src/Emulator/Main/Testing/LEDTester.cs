//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using System.Threading;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;

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

        public LEDTester AssertState(bool state, float timeout = 0)
        {
            var machine = led.GetMachine();
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)(timeout * 1000));

            var ev = new ManualResetEvent(false);
            var method = (Action<ILed, bool>)((s, o) => ev.Set());

            try
            {
                led.StateChanged += method;
                do
                {
                    if(led.State == state)
                    {
                        return this;
                    }

                    WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, ev });
                }
                while(!timeoutEvent.IsTriggered);
            }
            finally
            {
                led.StateChanged -= method;
            }

            throw new InvalidOperationException("LED assertion not met.");
        }

        private readonly ILed led;
    }
}

