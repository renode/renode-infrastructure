//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MH1903_Watchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_Watchdog(IMachine machine, ulong frequency) : base(machine)
        {
            // Create timer with default reload value (0xFFFFFFFF)
            // Timer counts down from reload value to 0
            watchdogTimer = new LimitTimer(machine.ClockSource, frequency, this, "MH1903_WDT",
                DefaultReloadValue, workMode: WorkMode.Periodic, enabled: false, eventEnabled: true, autoUpdate: true);
            watchdogTimer.LimitReached += TimerLimitReachedCallback;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            watchdogTimer.Reset();
            watchdogTimer.Limit = DefaultReloadValue;
            enabled = false;
            responseMode = false;
            interruptTriggered = false;
            interruptStatus = false;
            reloadValue = DefaultReloadValue;
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // WDT_CR - Control Register (0x00)
            Registers.WDT_CR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "WDT_EN",
                    writeCallback: (_, value) =>
                    {
                        if(value && !enabled)
                        {
                            // Once enabled, cannot be disabled (only by system reset)
                            enabled = true;
                            watchdogTimer.Enabled = true;
                            this.Log(LogLevel.Info, "Watchdog enabled - cannot be disabled until system reset");
                        }
                        else if(!value && enabled)
                        {
                            this.Log(LogLevel.Warning, "Attempted to disable watchdog - once enabled it cannot be disabled");
                        }
                    },
                    valueProviderCallback: _ => enabled)
                .WithFlag(1, name: "RMOD",
                    writeCallback: (_, value) =>
                    {
                        responseMode = value;
                        this.Log(LogLevel.Debug, "Response mode set to: {0}", value ? "Interrupt then Reset" : "Direct Reset");
                    },
                    valueProviderCallback: _ => responseMode)
                .WithReservedBits(2, 30);

            // Reserved register at 0x04
            Registers.RESERVED_04.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);

            // WDT_CCVR - Current Counter Value Register (0x08) - Read Only
            Registers.WDT_CCVR.Define(this, resetValue: 0x0000FFFF)
                .WithValueField(0, 32, FieldMode.Read, name: "WDT_CCVR",
                    valueProviderCallback: _ => (uint)watchdogTimer.Value);

            // WDT_CRR - Counter Restart Register (0x0C) - Write Only
            Registers.WDT_CRR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, FieldMode.Write, name: "WDT_CRR",
                    writeCallback: (_, value) =>
                    {
                        if(value == RestartKey)
                        {
                            this.Log(LogLevel.Debug, "Watchdog counter restarted (kicked)");
                            watchdogTimer.Value = reloadValue;

                            // Clear interrupt if in response mode
                            if(responseMode && interruptStatus)
                            {
                                interruptStatus = false;
                                interruptTriggered = false;
                                this.Log(LogLevel.Debug, "Watchdog interrupt cleared by restart");
                            }
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Invalid watchdog restart key: 0x{0:X2} (expected 0x76)", value);
                        }
                    })
                .WithReservedBits(8, 24);

            // WDT_STAT - Interrupt Status Register (0x10) - Read Only
            Registers.WDT_STAT.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "STAT",
                    valueProviderCallback: _ => interruptStatus)
                .WithReservedBits(1, 31);

            // WDT_EOI - End of Interrupt Register (0x14) - Read Only (clears on read)
            Registers.WDT_EOI.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "EOI",
                    valueProviderCallback: _ =>
                    {
                        // Reading this register clears the interrupt and resets counter
                        if(interruptStatus)
                        {
                            this.Log(LogLevel.Debug, "Watchdog interrupt cleared via WDT_EOI read");
                            interruptStatus = false;
                            interruptTriggered = false;
                            watchdogTimer.Value = reloadValue;
                        }
                        return false;
                    })
                .WithReservedBits(1, 31);

            // Reserved register at 0x18
            Registers.RESERVED_18.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);

            // WDT_RLD - Reload Value Register (0x1C)
            Registers.WDT_RLD.Define(this, resetValue: DefaultReloadValue)
                .WithValueField(0, 32, name: "WDT_RLD",
                    writeCallback: (_, value) =>
                    {
                        reloadValue = (uint)value;
                        watchdogTimer.Limit = reloadValue;
                        this.Log(LogLevel.Debug, "Watchdog reload value set to: 0x{0:X}", value);
                    },
                    valueProviderCallback: _ => reloadValue);
        }

        private void TimerLimitReachedCallback()
        {
            if(!enabled)
            {
                return;
            }

            if(responseMode)
            {
                // Response mode: First timeout -> interrupt, Second timeout -> reset
                if(!interruptTriggered)
                {
                    // First timeout - generate interrupt (note: no physical IRQ line in this SoC)
                    interruptTriggered = true;
                    interruptStatus = true;
                    this.Log(LogLevel.Warning, "Watchdog first timeout - interrupt status set");
                }
                else
                {
                    // Second timeout without clearing interrupt - system reset
                    this.Log(LogLevel.Warning, "Watchdog second timeout without interrupt clear - triggering system reset!");
                    machine.RequestReset();
                }
            }
            else
            {
                // Direct reset mode - immediate system reset on timeout
                this.Log(LogLevel.Warning, "Watchdog timeout - triggering system reset!");
                machine.RequestReset();
            }
        }

        private bool enabled;
        private bool responseMode;
        private bool interruptTriggered;
        private bool interruptStatus;
        private uint reloadValue;

        private readonly LimitTimer watchdogTimer;

        private const uint DefaultReloadValue = 0xFFFFFFFF;
        private const byte RestartKey = 0x76;

        private enum Registers : long
        {
            WDT_CR = 0x00,      // Control Register
            RESERVED_04 = 0x04, // Reserved
            WDT_CCVR = 0x08,    // Current Counter Value Register
            WDT_CRR = 0x0C,     // Counter Restart Register
            WDT_STAT = 0x10,    // Interrupt Status Register
            WDT_EOI = 0x14,     // End of Interrupt Register
            RESERVED_18 = 0x18, // Reserved
            WDT_RLD = 0x1C,     // Reload Value Register
        }
    }
}
