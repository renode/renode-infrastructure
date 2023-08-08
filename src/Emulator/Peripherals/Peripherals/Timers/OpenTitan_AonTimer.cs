//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class OpenTitan_AonTimer: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_AonTimer(IMachine machine, OpenTitan_PowerManager powerManager, OpenTitan_ResetManager resetManager, long frequency = 200000): base(machine)
        {
            this.powerManager = powerManager;
            this.resetManager = resetManager;

            powerManager.LowPowerStateChanged += HandleLowPowerTransition;

            WakeupTimerExpired = new GPIO();
            WatchdogTimerBark = new GPIO();
            FatalAlert = new GPIO();

            wkupTimer = new ComparingTimer(machine.ClockSource, frequency, this, "wkup_timer", workMode: Time.WorkMode.Periodic, eventEnabled: true);
            wkupTimer.CompareReached += UpdateWakeupInterrupts;

            wdogBarkInnerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "wdog_bark_inner_timer", workMode: Time.WorkMode.Periodic, eventEnabled: true);
            wdogBarkInnerTimer.CompareReached += UpdateWdogBarkInterrupts;
            wdogBiteInnerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "wdog_bite_inner_timer", workMode: Time.WorkMode.Periodic, eventEnabled: true);
            wdogBiteInnerTimer.CompareReached += UpdateWdogBiteInterrupts;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            FatalAlert.Unset();

            wkupTimer.Reset();
            wdogBarkInnerTimer.Reset();
            wdogBiteInnerTimer.Reset();
            UpdateWakeupInterrupts();
            UpdateWdogBarkInterrupts();
            UpdateWdogBiteInterrupts();

            lowPowerState = false;
        }

        public long Size => 0x30;

        public GPIO WakeupTimerExpired { get; }
        public GPIO WatchdogTimerBark { get; }
        public GPIO FatalAlert { get; }

        private void HandleLowPowerTransition(bool lowPower)
        {
            if(lowPowerState == lowPower)
            {
                return;
            }

            if(pauseInSleep.Value)
            {
                wdogBarkInnerTimer.Enabled = !lowPower;
                wdogBiteInnerTimer.Enabled = !lowPower;
            }

            lowPowerState = lowPower;
            this.Log(LogLevel.Debug, "Low power state set to {0}", lowPower);
        }

        private void DefineRegisters()
        {
            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                .WithReservedBits(1, 31);

            Registers.WakeupTimerControl.Define(this)
                .WithFlag(0, name: "enable", writeCallback: (_, val) =>
                {
                    wkupTimer.Enabled = val;
                    UpdateWakeupInterrupts();
                })
                .WithValueField(1, 12, name: "prescaler",
                    valueProviderCallback: _ => wkupTimer.Divider - 1,
                    writeCallback: (_, val) => wkupTimer.Divider = (uint)(val + 1u))
                .WithReservedBits(13, 19);

            Registers.WakeupTimerThreshold.Define(this)
                .WithValueField(0, 32, name: "threshold",
                    valueProviderCallback: _ => (uint)wkupTimer.Compare,
                    writeCallback: (_, val) => 
                    {
                        wkupTimer.Compare = val;
                        UpdateWakeupInterrupts();
                    });

            Registers.WakeupTimerCount.Define(this)
                .WithValueField(0, 32, name: "count",
                    valueProviderCallback: _ => (uint)wkupTimer.Value,
                    writeCallback: (_, val) => 
                    {
                        wkupTimer.Value = val;
                        UpdateWakeupInterrupts();
                    });

            Registers.WatchdogTimerWriteEnable.Define(this, 0x1)
                .WithFlag(0, out wdogConfigNotLocked, FieldMode.Read | FieldMode.WriteZeroToClear, name: "regwen")
                .WithReservedBits(1, 31);

            Registers.WatchdogTimerControl.Define(this)
                .WithFlag(0, name: "enable", writeCallback: (_, val) =>
                {
                    if(!wdogConfigNotLocked.Value)
                    {
                        this.Log(LogLevel.Warning, "Watchdog timer configuration is locked");
                        return;
                    }

                    wdogBiteInnerTimer.Enabled = val;
                    UpdateWdogBiteInterrupts();
                    wdogBarkInnerTimer.Enabled = val;
                    UpdateWdogBarkInterrupts();
                })
                .WithFlag(1, out pauseInSleep, name: "pause_in_sleep")
                .WithReservedBits(2, 30);

            Registers.WatchdogTimerBarkThreshold.Define(this)
                .WithValueField(0, 32, name: "threshold",
                    valueProviderCallback: _ => (uint)wdogBarkInnerTimer.Compare,
                    writeCallback: (_, val) => 
                    {
                        if(!wdogConfigNotLocked.Value)
                        {
                            this.Log(LogLevel.Warning, "Watchdog timer configuration is locked");
                            return;
                        }

                        wdogBarkInnerTimer.Compare = val;
                        UpdateWdogBarkInterrupts();
                    });

            Registers.WatchdogTimerBiteThreshold.Define(this)
                .WithValueField(0, 32, name: "threshold",
                    valueProviderCallback: _ => (uint)wdogBiteInnerTimer.Compare,
                    writeCallback: (_, val) => 
                    {
                        if(!wdogConfigNotLocked.Value)
                        {
                            this.Log(LogLevel.Warning, "Watchdog timer configuration is locked");
                            return;
                        }

                        wdogBiteInnerTimer.Compare = val;
                        UpdateWdogBiteInterrupts();
                    });

            Registers.WatchdogTimerCount.Define(this)
                .WithValueField(0, 32, name: "count",
                    valueProviderCallback: _ => (uint)wdogBiteInnerTimer.Value,
                    writeCallback: (_, val) => 
                    {
                        if(!wdogConfigNotLocked.Value)
                        {
                            this.Log(LogLevel.Warning, "Watchdog timer configuration is locked");
                            return;
                        }

                        wdogBiteInnerTimer.Value = val;
                        UpdateWdogBiteInterrupts();
                        wdogBarkInnerTimer.Value = val;
                        UpdateWdogBarkInterrupts();
                    });

            Registers.InterruptState.Define(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "wkup_timer_expired",
                    valueProviderCallback: _ => WakeupTimerExpired.IsSet,
                    writeCallback: (_, val) => { if(val) WakeupTimerExpired.Set(false); })
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "wdog_timer_bark",
                    valueProviderCallback: _ => WatchdogTimerBark.IsSet,
                    writeCallback: (_, val) => { if(val) WatchdogTimerBark.Set(false); })
                .WithReservedBits(2, 30);

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, name: "wkup_timer_expired",
                    writeCallback: (_, newValue) =>
                    {
                        if(newValue)
                        {
                            WakeupTimerExpired.Set(true);
                        }
                    })
                .WithFlag(1, FieldMode.Write, name: "wdog_timer_bark",
                    writeCallback: (_, newValue) =>
                    {
                        if(newValue)
                        {
                            WatchdogTimerBark.Set(true);
                        }
                    })
                .WithReservedBits(2, 30);

            Registers.WakeupRequestStatus.Define(this)
                .WithFlag(0, out wakeupLevelSignal, FieldMode.Read | FieldMode.WriteZeroToClear, name: "cause")
                .WithReservedBits(1, 31);
        }

        private void UpdateWakeupInterrupts()
        {
            var isActive = wkupTimer.Enabled && wkupTimer.Value >= wkupTimer.Compare;
            wakeupLevelSignal.Value = isActive;
            if(isActive && lowPowerState)
            {
                // Set wakeup level signal 
                powerManager.RequestWakeup();
            }
            WakeupTimerExpired.Set(isActive);
        }

        private void UpdateWdogBarkInterrupts()
        {
            var isActive = wdogBarkInnerTimer.Enabled && wdogBarkInnerTimer.Value >= wdogBarkInnerTimer.Compare;
            WatchdogTimerBark.Set(isActive);
            this.Log(LogLevel.Debug, "Watchdog bark interrupt is {0}", isActive ? "active" : "inactive");
        }

        private void UpdateWdogBiteInterrupts()
        {
            var isActive = wdogBiteInnerTimer.Enabled && wdogBiteInnerTimer.Value >= wdogBiteInnerTimer.Compare;
            if(isActive)
            {
                // Set bite level signal
                this.Log(LogLevel.Info, "Watchdog timer bite");
                resetManager.PeripheralRequestedReset(OpenTitan_ResetManager.HardwareResetReason.Watchdog, lowPowerState);
            }
        }

        private readonly ComparingTimer wkupTimer, wdogBarkInnerTimer, wdogBiteInnerTimer;

        private IFlagRegisterField pauseInSleep;
        private IFlagRegisterField wakeupLevelSignal;
        private IFlagRegisterField wdogConfigNotLocked;

        private readonly OpenTitan_PowerManager powerManager;
        private readonly OpenTitan_ResetManager resetManager;
        private bool lowPowerState;

        public enum Registers
        {
            AlertTest = 0x0,
            WakeupTimerControl = 0x4,
            WakeupTimerThreshold = 0x8,
            WakeupTimerCount = 0xc,
            WatchdogTimerWriteEnable = 0x10,
            WatchdogTimerControl = 0x14,
            WatchdogTimerBarkThreshold = 0x18,
            WatchdogTimerBiteThreshold = 0x1c,
            WatchdogTimerCount = 0x20,
            InterruptState = 0x24,
            InterruptTest = 0x28,
            WakeupRequestStatus = 0x2c,
        }
    }
}
