//
// Copyright (c) 2021 Zisis Adamos
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32_IndependentWatchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        //TODO: Stop timer on debug stop.
        //TODO: Use RCC to set restart cause.
        public STM32_IndependentWatchdog(IMachine machine, long frequency, bool windowOption = true, uint defaultPrescaler = 0) : base(machine)
        {
            watchdogTimer = new LimitTimer(machine.ClockSource, frequency, this, "STM32_IWDG", DefaultReloadValue, workMode: WorkMode.OneShot, enabled: false, eventEnabled: true, autoUpdate: true, divider: DefaultPrescalerValue);
            watchdogTimer.LimitReached += TimerLimitReachedCallback;
            this.defaultPrescaler = defaultPrescaler;
            this.windowOption = windowOption;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            watchdogTimer.Reset();
            registersUnlocked = false;
            reloadValue = DefaultReloadValue;
            window = DefaultWindow;
            windowEnabled = false;
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.Key.Define(this)
            .WithEnumField<DoubleWordRegister, Key>(0, 16, FieldMode.Write, writeCallback: (_, value) =>
            {
                registersUnlocked = false;
                switch(value)
                {
                    case Key.Reload:
                        if(windowEnabled && watchdogTimer.Value > window)
                        {
                            this.Log(LogLevel.Warning, "Watchdog reloaded outside of window, triggering reset!");
                            machine.RequestReset();
                        }
                        else
                        {
                            Reload();
                        }
                        break;
                    case Key.Start:
                        watchdogTimer.Enabled = true;
                        break;
                    case Key.Unlock:
                        registersUnlocked = true;
                        break;
                }
            }, name: "KEY")
            .WithReservedBits(16, 16);

            Registers.Prescaler.Define(this, defaultPrescaler)
            .WithValueField(0, 3, writeCallback: (_, value) =>
            {
                if(registersUnlocked)
                {
                    var divider = (int)Math.Pow(2, (2 + value));
                    if(divider > 256)
                    {
                        divider = 256;
                    }
                    watchdogTimer.Divider = divider;
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to change watchdog prescaler value without unlocking it");
                }
            }, name: "PR")
            .WithReservedBits(3, 29);

            Registers.Reload.Define(this, DefaultReloadValue)
            .WithValueField(0, 12, writeCallback: (_, value) =>
            {
                if(registersUnlocked)
                {
                    reloadValue = (uint)value;
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to change watchdog reload value without unlocking it");
                }
            }, name: "RL")
            .WithReservedBits(12, 20);

            Registers.Status.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "PVU")
            .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "RVU")
            .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "WVU")
            .WithReservedBits(3, 29);

            if(windowOption)
            {
                Registers.Window.Define(this, DefaultWindow)
                .WithValueField(0, 12, writeCallback: (_, value) =>
                {
                    if(registersUnlocked)
                    {
                        windowEnabled = true;
                        window = (uint)value;
                        Reload();
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Trying to change watchdog window without unlocking it");
                    }
                }, name: "WIN")
                .WithReservedBits(12, 20);
            }
        }

        private void Reload()
        {
            watchdogTimer.Limit = reloadValue;
        }

        private void TimerLimitReachedCallback()
        {
            this.Log(LogLevel.Warning, "Watchdog reset triggered!");
            machine.RequestReset();
        }

        private bool registersUnlocked;
        private uint reloadValue;
        private uint window;
        private bool windowEnabled;

        private readonly LimitTimer watchdogTimer;
        private readonly uint defaultPrescaler;
        private readonly bool windowOption;

        private const uint DefaultReloadValue = 0xFFF;
        private const uint DefaultWindow = 0xFFF;
        private const int DefaultPrescalerValue = 4;

        private enum Key
        {
            Unlock = 0x5555,
            Reload  = 0xAAAA,
            Start  = 0xCCCC
        }

        private enum Registers
        {
            Key = 0x0,
            Prescaler = 0x4,
            Reload = 0x8,
            Status = 0xC,
            Window = 0x10,
        }
    }
}
