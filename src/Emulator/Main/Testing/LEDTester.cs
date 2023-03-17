//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Time;
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

        public LEDTester AssertDutyCycle(float testDuration, double expectedDutyCycle, double tolerance = 0.05)
        {
            ulong lowTicks = 0;
            ulong highTicks = 0;

            var method = MakeStateChangeHandler((currState, dt) =>
            {
                if(currState)
                {
                    // we switch to high, so up to this point it was low
                    lowTicks += dt.Ticks;
                }
                else
                {
                    highTicks += dt.Ticks;
                }
            });

            try
            {
                led.StateChanged += method;

                var timeoutEvent = GetTimeoutEvent((ulong)(testDuration * 1000));
                timeoutEvent.WaitHandle.WaitOne();

                var highPercentage = (double)highTicks / (highTicks + lowTicks) * 100;
                if(highPercentage < expectedDutyCycle - (tolerance * 100) || expectedDutyCycle > expectedDutyCycle + (tolerance * 100))
                {
                    throw new InvalidOperationException($"Fill assertion not met: expected {expectedDutyCycle} with tolerance {tolerance * 100}%, but got {highPercentage}");
                }
            }
            finally
            {
                led.StateChanged -= method;
            }

            return this;
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private TimeoutEvent GetTimeoutEvent(ulong timeout)
        {
            return led.GetMachine().LocalTimeSource.EnqueueTimeoutEvent(timeout);
        }

        private Action<ILed, bool> MakeStateChangeHandler(Action<bool, TimeInterval> stateChanged)
        {
            TimeStamp? previousEventTimestamp = null;
            return (Action<ILed, bool>)((led, currState) =>
            {
                if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
                {
                    throw new InvalidOperationException("Couldn't obtain virtual time");
                }

                // first we need to "sync" with the first state change;
                // it doesn't matter if that's low/high or high/low transition
                if(previousEventTimestamp == null)
                {
                    previousEventTimestamp = vts;
                    return;
                }

                TimeInterval dt;
                // TODO: Below `if` block is a way to avoid TimeInterval underflow and the resulting exception
                // that occurs when the OnGPIO comes from a different thread(core) than expected.
                // This should be fixed by making sure we always get the Timestamp from the correct thread.
                // Until then the results will be radomly less accurate.
                if(vts.TimeElapsed > previousEventTimestamp.Value.TimeElapsed)
                {
                    dt = vts.TimeElapsed - previousEventTimestamp.Value.TimeElapsed;
                }
                else
                {
                    dt = TimeInterval.Empty;
                }

                stateChanged.Invoke(currState, dt);

                previousEventTimestamp = vts;
            });
        }

        private readonly ILed led;
    }
}

