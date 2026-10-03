//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MCXN94_WWDT : BasicDoubleWordPeripheral, IKnownSize
    {
        public MCXN94_WWDT(IMachine machine, ulong frequency = DefaultFrequency) : base(machine)
        {
            IRQ = new GPIO();

            timeoutTimer = new LimitTimer(machine.ClockSource, frequency, this, "WWDTTimeout", 0xFFFFFF, workMode: WorkMode.OneShot, enabled: false, eventEnabled: true, autoUpdate: true);
            timeoutTimer.LimitReached += HandleTimeout;

            warningTimer = new LimitTimer(machine.ClockSource, frequency, this, "WWDTWarning", 0xFFFFFF, workMode: WorkMode.OneShot, enabled: false, eventEnabled: true, autoUpdate: true);
            warningTimer.LimitReached += HandleWarning;

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            timeoutTimer.Reset();
            warningTimer.Reset();

            watchdogEnabled = false;
            watchdogResetEnabled = false;
            timeoutFlag = false;
            warningFlag = false;
            timeoutValue = DefaultTimeoutValue;
            warningValue = 0;
            windowValue = DisabledWindowValue;
            feedStage = FeedStage.None;

            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Mode.Define(this)
                .WithValueField(0, 8, name: "MOD",
                    valueProviderCallback: _ => ComposeModeRegister(),
                    writeCallback: (_, value) => WriteModeRegister((uint)value))
                .WithReservedBits(8, 24);

            Registers.TimeoutConstant.Define(this, DefaultTimeoutValue)
                .WithValueField(0, 24, name: "TC",
                    valueProviderCallback: _ => timeoutValue,
                    writeCallback: (_, value) =>
                    {
                        timeoutValue = SanitizeTimeoutValue((uint)value);
                        this.DebugLog("WWDT timeout constant updated to 0x{0:X6}", timeoutValue);
                    })
                .WithReservedBits(24, 8);

            Registers.Feed.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "FEED",
                    writeCallback: (_, value) => HandleFeed((byte)value))
                .WithReservedBits(8, 24);

            Registers.TimerValue.Define(this)
                .WithValueField(0, 24, FieldMode.Read, name: "TV",
                    valueProviderCallback: _ => GetCurrentTimerValue())
                .WithReservedBits(24, 8);

            Registers.WarningInterrupt.Define(this)
                .WithValueField(0, 10, name: "WARNINT",
                    valueProviderCallback: _ => warningValue,
                    writeCallback: (_, value) => warningValue = (uint)value)
                .WithReservedBits(10, 22);

            Registers.Window.Define(this, DisabledWindowValue)
                .WithValueField(0, 24, name: "WINDOW",
                    valueProviderCallback: _ => windowValue,
                    writeCallback: (_, value) => windowValue = (uint)value & TimerMask)
                .WithReservedBits(24, 8);
        }

        private uint ComposeModeRegister()
        {
            var value = 0u;
            value |= watchdogEnabled ? 1u << 0 : 0u;
            value |= watchdogResetEnabled ? 1u << 1 : 0u;
            value |= timeoutFlag ? 1u << 2 : 0u;
            value |= warningFlag ? 1u << 3 : 0u;
            return value;
        }

        private void WriteModeRegister(uint value)
        {
            if((value & (1u << 0)) != 0)
            {
                if(!watchdogEnabled)
                {
                    watchdogEnabled = true;
                    this.DebugLog("WWDT enabled");
                    ReloadWatchdog();
                }
            }
            if((value & (1u << 1)) != 0)
            {
                watchdogResetEnabled = true;
            }

            if((value & (1u << 2)) != 0)
            {
                timeoutFlag = false;
            }

            if((value & (1u << 3)) != 0)
            {
                warningFlag = false;
            }

            UpdateInterrupts();
        }

        private void HandleFeed(byte value)
        {
            switch(feedStage)
            {
            case FeedStage.None:
                feedStage = value == FirstFeedWord ? FeedStage.GotFirstWord : FeedStage.None;
                return;

            case FeedStage.GotFirstWord:
                feedStage = FeedStage.None;
                if(value == SecondFeedWord)
                {
                    RefreshWatchdog();
                    return;
                }

                if(value == FirstFeedWord)
                {
                    feedStage = FeedStage.GotFirstWord;
                }
                break;
            }
        }

        private void RefreshWatchdog()
        {
            if(!watchdogEnabled)
            {
                this.Log(LogLevel.Warning, "Ignoring WWDT refresh while watchdog is disabled");
                return;
            }

            if(windowValue != DisabledWindowValue && GetCurrentTimerValue() > windowValue)
            {
                TriggerTimeout("Feed sequence performed outside the allowed WWDT window");
                return;
            }

            ReloadWatchdog();
        }

        private void ReloadWatchdog()
        {
            var reloadValue = Math.Max(1u, timeoutValue);

            timeoutTimer.Limit = reloadValue;
            timeoutTimer.Value = reloadValue;
            timeoutTimer.Enabled = watchdogEnabled;
            timeoutTimer.EventEnabled = watchdogEnabled;

            var warningDelay = CalculateWarningDelay(reloadValue);
            warningTimer.Enabled = warningDelay.HasValue;
            warningTimer.EventEnabled = warningDelay.HasValue;
            if(warningDelay.HasValue)
            {
                warningTimer.Limit = warningDelay.Value;
                warningTimer.Value = warningDelay.Value;
            }

            warningFlag = false;
            UpdateInterrupts();
        }

        private uint? CalculateWarningDelay(uint reloadValue)
        {
            if(!watchdogEnabled || warningValue == 0)
            {
                return null;
            }

            var delay = reloadValue > warningValue ? reloadValue - warningValue + 1 : 1;
            return Math.Max(1u, delay);
        }

        private uint GetCurrentTimerValue()
        {
            if(!watchdogEnabled)
            {
                return timeoutValue;
            }

            if(timeoutTimer.Enabled)
            {
                return (uint)timeoutTimer.Value & TimerMask;
            }

            return timeoutFlag ? 0u : timeoutValue;
        }

        private uint SanitizeTimeoutValue(uint value)
        {
            var masked = value & TimerMask;
            return Math.Max(MinimumTimeoutValue, masked);
        }

        private void HandleWarning()
        {
            warningFlag = true;
            this.DebugLog("WWDT warning interrupt asserted");
            UpdateInterrupts();
        }

        private void HandleTimeout()
        {
            TriggerTimeout("WWDT timed out");
        }

        private void TriggerTimeout(string reason)
        {
            timeoutFlag = true;
            timeoutTimer.Enabled = false;
            warningTimer.Enabled = false;

            this.Log(LogLevel.Warning, reason);
            UpdateInterrupts();

            if(watchdogResetEnabled)
            {
                machine.RequestReset();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(warningFlag);
        }

        private bool watchdogEnabled;
        private bool watchdogResetEnabled;
        private bool timeoutFlag;
        private bool warningFlag;

        private uint timeoutValue;
        private uint warningValue;
        private uint windowValue;

        private FeedStage feedStage;

        private readonly LimitTimer timeoutTimer;
        private readonly LimitTimer warningTimer;

        private const byte FirstFeedWord = 0xAA;
        private const byte SecondFeedWord = 0x55;
        private const uint TimerMask = 0x00FF_FFFF;
        private const uint DisabledWindowValue = TimerMask;
        private const uint DefaultTimeoutValue = TimerMask;
        private const uint MinimumTimeoutValue = 0xFF;
        private const ulong DefaultFrequency = 1;

        private enum FeedStage
        {
            None,
            GotFirstWord
        }

        private enum Registers : long
        {
            Mode = 0x0,
            TimeoutConstant = 0x4,
            Feed = 0x8,
            TimerValue = 0xC,
            WarningInterrupt = 0x14,
            Window = 0x18
        }
    }
}
