//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32H7_SystemWindowWatchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H7_SystemWindowWatchdog(IMachine machine, ulong apbFrequency) : base(machine)
        {
            // The WWDG counter works in two stages and is reported differently than the internal LimitTimer value:
            // - The internal LimitTimer counts "ticks remaining until CNT reaches 0x40".
            // - The visible counter (as software sees it) is CNT = 0x40 + remaining_ticks before EWI.
            //
            // When the Early Wakeup Interrupt (EWI) is set, the visible counter stays at 0x40 until the final tick down to 0x3F
            // triggers a system reset. After firing the EWI, the timer is rescheduled with watchdogTimer.Value = 1 so that the
            // next tick will reach the limit and trigger the reset. If software reloads the counter with a value below 0x3F,
            // no IRQ is set, and an immediate reset is triggered.
            watchdogTimer = new LimitTimer(
                machine.ClockSource,
                apbFrequency,
                this,
                "internalTimer",
                DefaultReloadValue,
                workMode: WorkMode.Periodic,
                enabled: true,
                eventEnabled: false,
                autoUpdate: true,
                divider: InternalDivider
            );

            watchdogTimer.LimitReached += TimerLimitReachedCallback;

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            watchdogTimer.Reset();

            nextTimeoutShouldReset = false;

            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x0000007F)
                .WithValueField(0, 7, name: "T",
                    writeCallback: (_, value) => Reload(value),
                    valueProviderCallback: (_) => GetVisibleCounter())
                // Set by software, only cleared by hardware after a reset
                .WithFlag(7, FieldMode.Set | FieldMode.Read, name: "WDGA",
                    writeCallback: (_, value) => WatchdogEnabled = value,
                    valueProviderCallback: (_) => WatchdogEnabled)
                .WithReservedBits(8, 24)
            ;

            Registers.Configuration.Define(this, 0x0000007F)
                .WithValueField(0, 7, out windowValue, name: "W")
                .WithReservedBits(7, 2)
                .WithFlag(9, out earlyWakeupInterruptEnabled, name: "EWI",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithReservedBits(10, 1)
                .WithValueField(11, 3, name: "WDGTB",
                    writeCallback: (_, value) =>
                    {
                        var divider = InternalDivider << (int)value;
                        watchdogTimer.Divider = divider;
                        this.DebugLog($"Prescaler updated: WDGTB={value}, divider={divider}");
                    })
                .WithReservedBits(14, 18)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, out earlyWakeupInterruptPending, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EWIF",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithReservedBits(1, 31)
            ;
        }

        private void Reload(ulong value)
        {
            if(WatchdogEnabled && value < CountdownTriggerIrqValue)
            {
                this.ErrorLog("Watchdog reset triggered, write to T register with T[6] not set");
                machine.RequestReset();
                return;
            }

            var currentVisibleCounter = GetVisibleCounter();
            if(WatchdogEnabled && currentVisibleCounter > windowValue.Value)
            {
                this.ErrorLog("Watchdog reset triggered, write to T register outside of allowed window");
                machine.RequestReset();
                return;
            }

            // As we use a LimitTimer, we firstly count time until the IRQ is generated
            var valueToSet = value - CountdownTriggerIrqValue;
            watchdogTimer.Value = valueToSet;
            nextTimeoutShouldReset = false;
        }

        private uint GetVisibleCounter()
        {
            if(nextTimeoutShouldReset)
            {
                return CountdownTriggerResetValue + (uint)watchdogTimer.Value;
            }

            return CountdownTriggerIrqValue + (uint)watchdogTimer.Value;
        }

        private void UpdateInterrupts()
        {
            var interrupt = earlyWakeupInterruptEnabled.Value && earlyWakeupInterruptPending.Value;

            this.Log(LogLevel.Debug, "IRQ: {0} -> {1}", IRQ.IsSet, interrupt);
            IRQ.Set(interrupt);
        }

        private void TimerLimitReachedCallback()
        {
            if(nextTimeoutShouldReset)
            {
                // Second stage: visible CNT reached 0x3F, reset MCU
                this.Log(LogLevel.Warning, "Reset triggered");
                machine.RequestReset();
                return;
            }

            // First stage: visible CNT reached 0x40, raise EWI
            nextTimeoutShouldReset = true;
            earlyWakeupInterruptPending.Value = true;

            UpdateInterrupts();

            // Schedule next tick for the second stage
            watchdogTimer.Value = 1;
        }

        private bool WatchdogEnabled
        {
            get => watchdogTimer.EventEnabled;
            set => watchdogTimer.EventEnabled = value;
        }

        private bool nextTimeoutShouldReset;

        private IFlagRegisterField earlyWakeupInterruptEnabled;
        private IFlagRegisterField earlyWakeupInterruptPending;

        private IValueRegisterField windowValue;

        private readonly LimitTimer watchdogTimer;

        private const uint CountdownTriggerResetValue = 0x3F;
        private const uint CountdownTriggerIrqValue = 0x40;
        private const uint DefaultReloadValue = 0x7F - CountdownTriggerIrqValue;
        private const ulong InternalDivider = 4096;

        private enum Registers
        {
            Control = 0x00,          // WWDG_CR
            Configuration = 0x04,    // WWDG_CFR
            Status = 0x08,           // WWDG_SR
        }
    }
}