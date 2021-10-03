//
// Copyright (c) 2021 Zisis Adamos
// Copyright (c) 2010-2022 Antmicro
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
    public class STM32F4_IndependentWatchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        //TODO: Stop timer on debug stop.
        //TODO: Use RCC to set restart cause.
        public STM32F4_IndependentWatchdog(Machine machine, long frequency) : base(machine)
        {
            watchdogTimer = new LimitTimer(machine.ClockSource, frequency, this, "STM32_IWDG", DefaultReloadValue, workMode: WorkMode.OneShot, enabled: false, eventEnabled: true);
            watchdogTimer.LimitReached += TimerLimitReachedCallback;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            watchdogTimer.Reset();
            registersUnlocked = false;
            reloadValue = DefaultReloadValue;
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
                        watchdogTimer.Limit = reloadValue;
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

            Registers.Prescaler.Define(this)
            .WithValueField(0, 3, writeCallback: (_, value) =>
            {
                if(registersUnlocked)
                {
                    watchdogTimer.Divider = (int)Math.Pow(2, (2 + value));
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to change watchdog prescaler value without unlocking it");
                }
            }, name: "PR")
            .WithReservedBits(3, 29);

            Registers.Reload.Define(this)
            .WithValueField(0, 12, writeCallback: (_, value) =>
            {
                if(registersUnlocked)
                {
                    reloadValue = value;
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
            .WithReservedBits(2, 30);
        }

        private void TimerLimitReachedCallback()
        {
            this.Log(LogLevel.Warning, "Watchdog reset triggered!");
            machine.RequestReset();
        }

        private bool registersUnlocked;
        private uint reloadValue;

        private readonly LimitTimer watchdogTimer;

        private const uint DefaultReloadValue = 0xFFF;
        private const uint DefaultPrescalerValue = 4;

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
            Status = 0xC
        }
    }
}
