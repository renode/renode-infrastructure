//
// Copyright (c) 2010-2022 Antmicro
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
using System.Runtime.CompilerServices;

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
            var timeoutEvent = GetTimeoutEvent((ulong)(timeout * 1000));

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

        public LEDTester AssertAndHoldState(bool initialState, float timeoutAssert, float timeoutHold)
        {
            var locker = new Object();
            int numberOfStateChanges = 0;
            bool isHolding;

            var ev = new AutoResetEvent(false);
            var method = (Action<ILed, bool>)
            ((led, currState) => 
            {
                lock(locker)
                {
                    ++numberOfStateChanges;
                    ev.Set();
                }
            });

            try
            {
                float timeout;
                // this needs to be treated as atomic block so the state doesn't change during initialization
                lock(locker)
                {
                    led.StateChanged += method;
                    isHolding = initialState == led.State;
                    timeout = isHolding ? timeoutHold : timeoutAssert;
                }
                var timeoutEvent = GetTimeoutEvent((ulong)(timeout * 1000));
                
                do
                {
                    var eventSrc = WaitHandle.WaitAny( new [] { timeoutEvent.WaitHandle, ev } );

                    if(isHolding)
                    {
                        if(numberOfStateChanges > 0)
                        {
                            throw new InvalidOperationException("LED changed state.");
                        }
                    }
                    else
                    {
                        lock(locker)
                        {
                            if(numberOfStateChanges == 1)
                            {
                                isHolding = true;
                                timeoutEvent = GetTimeoutEvent((ulong)(timeoutHold * 1000));
                                --numberOfStateChanges;
                            }
                            else if(eventSrc == 0)
                            {
                                throw new InvalidOperationException("Initial LED assertion not met.");
                            }
                            else
                            {
                                throw new InvalidOperationException("LED changed state.");
                            }
                        }
                    }
                }
                while((!timeoutEvent.IsTriggered && isHolding) || !isHolding);
            }
            finally
            {
                led.StateChanged -= method;
                ev.Dispose();
            }

            return this;
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)] 
        private TimeoutEvent GetTimeoutEvent(ulong timeout)
        {
            return led.GetMachine().LocalTimeSource.EnqueueTimeoutEvent(timeout);
        }

        private readonly ILed led;
    }
}

