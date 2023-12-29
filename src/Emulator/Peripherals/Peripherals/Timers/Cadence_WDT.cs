//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Cadence_WDT : BasicDoubleWordPeripheral, IKnownSize
    {
        public Cadence_WDT(IMachine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();

            watchdogTimer = new LimitTimer(machine.ClockSource, frequency, this, "watchdog", enabled: false, eventEnabled: true);
            watchdogTimer.LimitReached += () =>
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                if(interruptRequestEnable.Value)
                {
                    IRQ.Blink();
                }

                if(resetEnable.Value)
                {
                    this.Log(LogLevel.Info, "Watchdog timed out. Resetting...");
                    machine.RequestReset();
                }
                LimitReached?.Invoke(this);
            };
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            watchdogTimer.Reset();
        }

        public GPIO IRQ { get; }
        public long Size => 0x1000;

        public event Action<Cadence_WDT> LimitReached;

        private void DefineRegisters()
        {
            Register.ZeroMode.Define(this, resetValue: 0x1C3)
                .WithFlag(0, out watchdogEnable, writeCallback: (oldVal, newVal) => 
                {
                    if(zeroAccessKey.Value != ZeroAccessKey)
                    {
                        watchdogEnable.Value = oldVal;
                        return;
                    }
                    watchdogTimer.Enabled = newVal;
                }, name: "WDEN")
                .WithFlag(1, out resetEnable, writeCallback: (oldVal, newVal) =>
                {
                    if(zeroAccessKey.Value != ZeroAccessKey)
                    {
                        resetEnable.Value = oldVal;
                        return;
                    }
                }, name: "RSTEN")
                .WithFlag(2, out interruptRequestEnable, writeCallback: (oldVal, newVal) =>
                {
                    if(zeroAccessKey.Value != ZeroAccessKey)
                    {
                        interruptRequestEnable.Value = oldVal;
                        return;
                    }
                }, name: "IRQEN")
                .WithTaggedFlag("EXTEN", 3)
                .WithTag("RSTLN", 4, 3)
                .WithTag("IRQLN", 7, 2)
                .WithTag("EXLN", 9, 3)
                .WithValueField(12, 12, out zeroAccessKey, FieldMode.Write, name: "ZKEY")
                .WithReservedBits(24, 8)
                .WithWriteCallback((_, __) =>
                {
                    if(zeroAccessKey.Value != ZeroAccessKey)
                    {
                        this.Log(LogLevel.Warning, "Write to the register is invalid because of the wrong access key");
                    }
                });

            Register.CounterControl.Define(this, resetValue: 0b111100)
                .WithValueField(0, 2, out counterClockPrescale, writeCallback: (oldVal, newVal) =>
                {
                    if(counterAccessKey.Value != CounterAccessKey)
                    {
                        counterClockPrescale.Value = oldVal;
                        return;
                    }
                    watchdogTimer.Divider = 1 << (int)(3 * (newVal + 1));
                }, name: "CLKSEL")
                .WithValueField(2, 12, out counterRestartValue, writeCallback: (oldVal, newVal) =>
                {
                    if(counterAccessKey.Value != CounterAccessKey)
                    {
                        counterRestartValue.Value = oldVal;
                        return;
                    }
                }, name: "CRV")
                .WithValueField(14, 12, out counterAccessKey, FieldMode.Write, name: "CKEY")
                .WithReservedBits(26, 6)
                .WithWriteCallback((_, __) =>
                {
                    if(counterAccessKey.Value != CounterAccessKey)
                    {
                        this.Log(LogLevel.Warning, "Write to the register is invalid because of the wrong access key");
                    }
                });

            Register.Restart.Define(this)
                .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, val) =>
                {
                    if(val != RestartKey)
                    {
                        this.Log(LogLevel.Warning, "Write to the register is invalid because of the wrong access key");
                        return;
                    }
                    watchdogTimer.Value = (counterRestartValue.Value << 12) | 0xFFF;
                })
                .WithReservedBits(16, 16);

            Register.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => WatchdogZero)
                .WithReservedBits(1, 31);
        }

        private bool WatchdogZero => watchdogTimer.Value == watchdogTimer.Limit;

        private readonly LimitTimer watchdogTimer;

        private IFlagRegisterField watchdogEnable;
        private IFlagRegisterField resetEnable;
        private IFlagRegisterField interruptRequestEnable;
        private IValueRegisterField zeroAccessKey;
        private IValueRegisterField counterClockPrescale;
        private IValueRegisterField counterRestartValue;
        private IValueRegisterField counterAccessKey;

        private const ushort ZeroAccessKey = 0xABC;
        private const ushort CounterAccessKey = 0x248;
        private const ushort RestartKey = 0x1999;

        private enum Register
        {
            ZeroMode = 0x0,
            CounterControl = 0x4,
            Restart = 0x8,
            Status = 0xC,
        }
    }
}
