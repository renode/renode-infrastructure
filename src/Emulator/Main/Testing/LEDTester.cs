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
        public static void CreateLEDTester(this Emulation emulation, string name, ILed led, float defaultTimeout = 0)
        {
            emulation.ExternalsManager.AddExternal(new LEDTester(led, defaultTimeout), name);
        }
    }

    public class LEDTester : IExternal
    {
        public LEDTester(ILed led, float defaultTimeout = 0)
        {
            ValidateArgument(defaultTimeout, nameof(defaultTimeout), allowZero: true);
            this.led = led;
            this.machine = led.GetMachine();
            this.defaultTimeout = defaultTimeout;
        }

        public LEDTester AssertState(bool state, float? timeout = null, bool pauseEmulation = false)
        {
            timeout = timeout ?? defaultTimeout;
            ValidateArgument(timeout.Value, nameof(timeout), allowZero: true);

            var emulation = EmulationManager.Instance.CurrentEmulation;
            var timeoutEvent = GetTimeoutEvent((ulong)(timeout * 1000));
            AutoResetEvent emulationPausedEvent = null;

            var ev = new ManualResetEvent(false);
            var method = (Action<ILed, bool>)
            ((led, currState) =>
            {
                if(pauseEmulation && currState == state)
                {
                    machine.PauseAndRequestEmulationPause(precise: true);
                }
                ev.Set();
            });

            try
            {
                led.StateChanged += method;
                // Don't start the emulation if the assert would succeed instantly
                // or regardless of the LED state if the timeout is 0
                if(led.State != state && timeout != 0)
                {
                    emulationPausedEvent = StartEmulationAndGetPausedEvent(emulation, pauseEmulation);
                }

                do
                {
                    if(led.State == state)
                    {
                        emulationPausedEvent?.WaitOne();
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

        public LEDTester AssertAndHoldState(bool initialState, float timeoutAssert, float timeoutHold, bool pauseEmulation = false)
        {
            ValidateArgument(timeoutAssert, nameof(timeoutAssert), allowZero: true);
            ValidateArgument(timeoutHold, nameof(timeoutHold));
            var emulation = EmulationManager.Instance.CurrentEmulation;
            AutoResetEvent emulationPausedEvent = null;
            var locker = new Object();
            int numberOfStateChanges = 0;
            bool isHolding = false;

            var ev = new AutoResetEvent(false);
            TimeoutEvent timeoutEvent;
            var method = (Action<ILed, bool>)
            ((led, currState) =>
            {
                lock(locker)
                {
                    ++numberOfStateChanges;
                    if(!isHolding && numberOfStateChanges == 1)
                    {
                        // Create a new event for holding at the precise moment that the LED state changed
                        timeoutEvent = GetTimeoutEvent((ulong)(timeoutHold * 1000), MakePauseRequest(emulation, pauseEmulation));
                    }
                    ev?.Set();
                }
            });

            try
            {
                // this needs to be treated as atomic block so the state doesn't change during initialization
                lock(locker)
                {
                    led.StateChanged += method;
                    isHolding = initialState == led.State;
                    var timeout = isHolding ? timeoutHold : timeoutAssert;
                    // If we're already holding, make the first timeout event pause the emulation
                    timeoutEvent = GetTimeoutEvent((ulong)(timeout * 1000),
                        MakePauseRequest(emulation, pauseEmulation && isHolding));
                    emulationPausedEvent = StartEmulationAndGetPausedEvent(emulation, pauseEmulation);
                }

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
                lock(locker)
                {
                    led.StateChanged -= method;
                    ev.Dispose();
                    ev = null;
                }
            }

            emulationPausedEvent?.WaitOne();

            return this;
        }

        public LEDTester AssertDutyCycle(float testDuration, double expectedDutyCycle, double tolerance = 0.05, bool pauseEmulation = false)
        {
            ValidateArgument(testDuration, nameof(testDuration));
            ValidateArgument(expectedDutyCycle, nameof(expectedDutyCycle), min: 0, max: 1);
            ValidateArgument(tolerance, nameof(tolerance), min: 0, max: 1);
            var emulation = EmulationManager.Instance.CurrentEmulation;
            AutoResetEvent emulationPausedEvent = null;
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
                var timeoutEvent = GetTimeoutEvent((ulong)(testDuration * 1000), MakePauseRequest(emulation, pauseEmulation));
                emulationPausedEvent = StartEmulationAndGetPausedEvent(emulation, pauseEmulation);

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

            emulationPausedEvent?.WaitOne();

            return this;
        }

        public LEDTester AssertIsBlinking(float testDuration, double onDuration, double offDuration, double tolerance = 0.05, bool pauseEmulation = false)
        {
            ValidateArgument(testDuration, nameof(testDuration));
            ValidateArgument(onDuration, nameof(onDuration));
            ValidateArgument(offDuration, nameof(offDuration));
            var emulation = EmulationManager.Instance.CurrentEmulation;
            AutoResetEvent emulationPausedEvent = null;
            var stateChanged = false;
            var patternMismatchEvent = new ManualResetEvent(false);
            var method = MakeStateChangeHandler((currState, dt) =>
            {
                stateChanged = true;
                // currState is after a switch, so when it's high we need to check the off duration
                var expectedDuration = currState ? offDuration : onDuration;
                if(!IsInRange(dt.TotalSeconds, expectedDuration, tolerance))
                {
                    patternMismatchEvent.Set();
                }
            });

            try
            {
                led.StateChanged += method;
                var timeoutEvent = GetTimeoutEvent((ulong)(testDuration * 1000), MakePauseRequest(emulation, pauseEmulation));
                emulationPausedEvent = StartEmulationAndGetPausedEvent(emulation, pauseEmulation);
                var eventIdx = WaitHandle.WaitAny( new [] { timeoutEvent.WaitHandle, patternMismatchEvent } );

                if(!stateChanged)
                {
                    throw new InvalidOperationException("Expected blinking pattern not detected (LED state never changed)");
                }
                if(eventIdx == 1)
                {
                    throw new InvalidOperationException("Expected blinking pattern not detected (State duration was out of specified range)");
                }
            }
            finally
            {
                led.StateChanged -= method;
            }

            emulationPausedEvent?.WaitOne();

            return this;
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool IsInRange(double actualValue, double expectedValue, double tolerance)
        {
            return (actualValue >= expectedValue * (1 - tolerance)) && (actualValue <= (expectedValue * (1 + tolerance)));
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private TimeoutEvent GetTimeoutEvent(ulong timeout, Action callback = null)
        {
            return machine.LocalTimeSource.EnqueueTimeoutEvent(timeout, callback);
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

        private Action MakePauseRequest(Emulation emulation, bool pause)
        {
            return pause ? (Action)(() =>
            {
                emulation.PauseAll();
            }) : null;
        }

        private AutoResetEvent StartEmulationAndGetPausedEvent(Emulation emulation, bool pause)
        {
            var emulationPausedEvent = pause ? emulation.GetStartedStateChangedEvent(false) : null;
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }
            return emulationPausedEvent;
        }

        // If no min or max is provided, this function checks whether the argument is not negative
        // (if allowZero) or positive (otherwise)
        private static void ValidateArgument(double value, string name, bool allowZero = false,
            double? min = null, double? max = null)
        {
            if(min != null || max != null)
            {
                if(value < min || value > max)
                {
                    throw new ArgumentException($"Value must be in range [{min}; {max}], but was {value}", name);
                }
            }
            else if(value < 0 || !allowZero && value == 0)
            {
                var explanation = allowZero ? "not be negative" : "be positive";
                throw new ArgumentException($"Value must {explanation}, but was {value}", name);
            }
        }

        private readonly ILed led;
        private readonly IMachine machine;
        private readonly float defaultTimeout;
    }
}

