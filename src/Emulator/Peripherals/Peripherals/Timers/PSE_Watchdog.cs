//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PSE_Watchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public PSE_Watchdog(Machine machine, long frequency) : base(machine)
        {
            DefineRegisters();
            internalTimer = new LimitTimer(machine.ClockSource, frequency, this, String.Empty, TimeDefault - MSVPDefault, workMode: WorkMode.OneShot, eventEnabled: true);
            internalTimer.LimitReached += TimerLimitReached;

            RefreshEnable = new GPIO();
            Trigger = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();
            Trigger.Unset();
            RefreshEnable.Unset();
            state = State.ForbiddenRegion;
            internalTimer.Reset();
        }

        public long Size => 0x1000;

        //TODO: Locking of registers. `locked` field has a correct value, but it is not actively used
        private void DefineRegisters()
        {
            Registers.Refresh.Define(this)
                .WithValueField(0, 32, writeCallback: (_, value) =>
                {
                    if(!internalTimer.Enabled)
                    {
                        //this `if` is a microoptimisation, but as it may happen very frequently and writing to LimitTimer is expensive, I'd like to keep it
                        this.Log(LogLevel.Debug, "Starting watchdog.");
                        internalTimer.Enabled = true;
                        SetState(State.ForbiddenRegion);
                    }
                    else if(state == State.RefreshRegion && value == WatchdogReset)
                    {
                        this.Log(LogLevel.Noisy, "Refreshing watchdog.");
                        SetState(State.ForbiddenRegion);
                    }
                    else if(state == State.ForbiddenRegion && forbiddenRangeEnabled.Value)
                    {
                        this.Log(LogLevel.Warning, "Watchdog refreshed in forbidden region, triggering NMI.");
                        SetState(State.AfterTrigger);
                    }
                }, valueProviderCallback: _ => GetCurrentTimerValue(), name: "REFRESH")
                .WithWriteCallback((_, __) => locked.Value = true);
            ;

            Registers.Control.Define(this, 2)
                .WithFlag(0, out refreshInterruptEnabled, changeCallback: (_, __) => Update(), name: "INTEN_MSVP")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "INTEN_TRIG")
                .WithTaggedFlag("INTEN_SLEEP", 2)
                .WithTaggedFlag("ACTIVE_SLEEP", 3)
                .WithFlag(6, out forbiddenRangeEnabled, name: "ENABLE_FORBIDDEN")
                .WithWriteCallback((_, __) => locked.Value = true);
            ;

            Registers.Status.Define(this)
                .WithFlag(0, out refreshPermittedLevelTripped, FieldMode.Read | FieldMode.WriteOneToClear, name: "MVRP_TRIPPED")
                .WithFlag(1, out watchdogTripped, FieldMode.Read | FieldMode.WriteOneToClear, name: "WDOG_TRIPPED")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => forbiddenRangeEnabled.Value && state == State.ForbiddenRegion, name: "FORBIDDEN")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => state == State.AfterTrigger, name: "TRIGGERED")
                .WithFlag(4, out locked, FieldMode.Read, name: "LOCKED")
                .WithTaggedFlag("DEVRST", 5)
                .WithWriteCallback((_, __) => { locked.Value = true; Update(); });
            ;

            Registers.Time.Define(this, TimeDefault)
                .WithValueField(0, 24, out time, name: "WDOGTIME")
                .WithWriteCallback((_, __) => locked.Value = true);
            ;

            Registers.MSVP.Define(this, MSVPDefault)
                .WithValueField(0, 24, out maximumValueForWhichRefreshIsPermitted, name: "WDOGMVRP")
            ;

            Registers.Trigger.Define(this, TriggerDefault)
                .WithValueField(0, 12, out triggerValue, name: "WDOGTRIG")
            ;

            Registers.Force.Define(this)
                .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(state == State.AfterTrigger && value == ResetTrigger)
                    {
                        TriggerReset();
                    }
                    else
                    {
                        SetState(State.AfterTrigger);
                    }
                }, name: "FORCE")
                //The line below is controversial, as the docs are self contradictory - "write to any other register" against "write from 0x0 to 0xc").
                //Elsewhere it is mentioned that write to TIME blocks MSVP and TRIGGER registers.
                //Elsewhere it says that the registers are blocked when MVRP is passed for the first time, but the timer is not enabled before the first refresh...
                .WithWriteCallback((_, __) => locked.Value = true);
            ;
        }

        private void TimerLimitReached()
        {
            this.Log(LogLevel.Noisy, "Watchdog event reached in state {0}.", state);
            if(state == State.AfterTrigger)
            {
                TriggerReset();
            }
            else
            {
                //ForbiddenRegion -> RefreshRegion, RefreshRegion -> AfterTrigger
                SetState(state + 1);
            }
        }

        private void TriggerReset()
        {
            this.Log(LogLevel.Warning, "Watchdog reset triggered!");
            machine.RequestReset();
        }

        private uint GetCurrentTimerValue()
        {
            var rest = 0u;
            switch(state)
            {
                case State.ForbiddenRegion:
                    rest = maximumValueForWhichRefreshIsPermitted.Value;
                    break;
                case State.RefreshRegion:
                    rest = triggerValue.Value;
                    break;
                //intentionally no State.AfterTrigger, rest = 0
            }
            return (uint)internalTimer.Value + rest;
        }

        private void SetState(State state)
        {
            this.Log(LogLevel.Noisy, "Switching state to {0}.", state);
            this.state = state;
            switch(state)
            {
                case State.ForbiddenRegion:
                    internalTimer.Limit = time.Value - maximumValueForWhichRefreshIsPermitted.Value;
                    refreshPermittedLevelTripped.Value = false;
                    watchdogTripped.Value = false;
                    break;
                case State.RefreshRegion:
                    refreshPermittedLevelTripped.Value = true;
                    watchdogTripped.Value = false;
                    internalTimer.Limit = maximumValueForWhichRefreshIsPermitted.Value - triggerValue.Value;
                    break;
                case State.AfterTrigger:
                    watchdogTripped.Value = true;
                    internalTimer.Limit = triggerValue.Value;
                    break;
                default:
                    throw new ArgumentException("Trying to set invalid watchdog state.", nameof(state));
            }
            //We do this to get proper info in GetClockSourceInfo (need to set Limit) and because we might change state by writing registers
            internalTimer.Value = internalTimer.Limit;
            internalTimer.Enabled = true;
            Update();
        }

        private void Update()
        {
            Trigger.Set(watchdogTripped.Value);
            //not sure about this. Should we set it when we refresh in the forbidden region and jump to AfterTrigger?
            RefreshEnable.Set(refreshInterruptEnabled.Value && refreshPermittedLevelTripped.Value);
            this.Log(LogLevel.Noisy, "Sending interrupts, RefreshEnable: {0}, Trigger: {1}", RefreshEnable.IsSet, Trigger.IsSet);
        }

        public GPIO Trigger { get; }
        public GPIO RefreshEnable { get; }

        private LimitTimer internalTimer;
        private State state;

        private IFlagRegisterField locked;
        private IValueRegisterField time;
        private IValueRegisterField maximumValueForWhichRefreshIsPermitted;
        private IValueRegisterField triggerValue;
        private IFlagRegisterField refreshInterruptEnabled;
        private IFlagRegisterField forbiddenRangeEnabled;
        private IFlagRegisterField refreshPermittedLevelTripped;
        private IFlagRegisterField watchdogTripped;

        private const uint ResetTrigger = 0xDEAD;
        private const uint WatchdogReset = 0xDEADC0DE;
        private const uint TimeDefault = 0xFFFFF0;
        private const uint MSVPDefault = 0x989680;
        private const uint TriggerDefault = 0x3E0;

        private enum State
        {
            ForbiddenRegion,
            RefreshRegion,
            AfterTrigger
        }

        private enum Registers
        {
            Refresh = 0x0,
            Control = 0x4,
            Status = 0x8,
            Time = 0xC,
            //I'm not sure how to expand this name
            MSVP = 0x10,
            Trigger = 0x14,
            Force = 0x18
        }
    }
}
