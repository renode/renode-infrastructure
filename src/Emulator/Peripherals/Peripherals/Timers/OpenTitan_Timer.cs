//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2021 Google LLC
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    // OpenTitan rv_timer has a configurable number of timers and harts/Harts, but this implementation is limited to 1 hart and 1 timer.
    // It is compliant with the v1.11 RISC-V privilege specification.
    // The counters are all 64-bit and each timer has a configurable prescaler and step.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class OpenTitan_Timer : BasicDoubleWordPeripheral, IKnownSize, IRiscVTimeProvider
    {
        public OpenTitan_Timer(IMachine machine, long frequency = 24000000) : base(machine)
        {
            IRQ = new GPIO();
            FatalAlert = new GPIO();
            underlyingTimer = new ComparingTimer(machine.ClockSource, frequency, this, "timer", workMode: Time.WorkMode.Periodic, eventEnabled: true);

            underlyingTimer.CompareReached += UpdateInterrupts;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            FatalAlert.Unset();

            underlyingTimer.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x120;

        public ulong TimerValue => underlyingTimer.Value;

        public GPIO IRQ { get; }
        public GPIO FatalAlert { get; }

        private void DefineRegisters()
        {
            Registers.AlertTest.Define(this, 0x0)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                .WithReservedBits(1, 31);

            Registers.Control0.Define(this)
                .WithFlag(0, name: "CONTROL0", writeCallback: (_, val) =>
                {
                    underlyingTimer.Enabled = val;
                    UpdateInterrupts();
                })
                .WithReservedBits(1, 31)
            ;

            Registers.CompareLowHart0.Define(this, 0xFFFFFFFF)
                 .WithValueField(0, 32, name: "COMPARELOW",
                    valueProviderCallback: _ => (uint)(underlyingTimer.Compare),
                    writeCallback: (_, val) =>
                    {
                        underlyingTimer.Compare = (underlyingTimer.Compare & 0xFFFFFFFF00000000uL) | val;
                        UpdateInterrupts();
                    })
            ;

            Registers.CompareHighHart0.Define(this, 0xFFFFFFFF)
                 .WithValueField(0, 32, name: "COMPAREHI",
                    valueProviderCallback: _ => (uint)(underlyingTimer.Compare >> 32),
                    writeCallback: (_, val) =>
                    {
                        underlyingTimer.Compare = (underlyingTimer.Compare & 0x00000000FFFFFFFFuL) | (((ulong)val) << 32);
                        UpdateInterrupts();
                    })
            ;

            Registers.ConfigurationHart0.Define(this, 0x10000)
                .WithValueField(0, 12, out prescaler, name: "PRESCALE")
                .WithValueField(16, 8, out step, name: "STEP")
                .WithWriteCallback((_, __) => TimerUpdateConfiguration())
                .WithReservedBits(24, 8)
            ;

            Registers.ValueLowHart0.Define(this)
                .WithValueField(0, 32, name: "VALUELOW",
                    valueProviderCallback: _ => (uint)(underlyingTimer.Value),
                    writeCallback: (_, val) =>
                    {
                        underlyingTimer.Value = (underlyingTimer.Value & 0xFFFFFFFF00000000uL) | val;
                        UpdateInterrupts();
                    })
            ;

            Registers.ValueHighHart0.Define(this)
                .WithValueField(0, 32, name: "VALUEHI",
                    valueProviderCallback: _ => (uint)(underlyingTimer.Value >> 32),
                    writeCallback: (_, val) =>
                    {
                        underlyingTimer.Value = (underlyingTimer.Value & 0x00000000FFFFFFFFuL) | (((ulong)val) << 32);
                        UpdateInterrupts();
                    })
            ;

            Registers.InterruptEnableHart0.Define(this)
                .WithFlag(0, out interruptEnabled, name: "IE")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptStatusHart0.Define(this)
                .WithFlag(0, FieldMode.Read, name: "IS", valueProviderCallback: _ => IRQ.IsSet)
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptTestHart0.Define(this)
                .WithFlag(0, FieldMode.Write, name: "T",
                    writeCallback: (_, newValue) =>
                    {
                        if(newValue)
                        {
                            IRQ.Set(true);
                        }
                    }
                )
            ;
        }

        private void TimerUpdateConfiguration()
        {
            var divider = (uint)(prescaler.Value + 1u);
            underlyingTimer.Divider = (divider == 0) ? 1 : divider;
            underlyingTimer.Step = (uint)step.Value;
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(
                underlyingTimer.Enabled
                && interruptEnabled.Value
                && (underlyingTimer.Value >= underlyingTimer.Compare));
        }

        private IFlagRegisterField interruptEnabled;
        private IValueRegisterField prescaler;
        private IValueRegisterField step;

        private readonly ComparingTimer underlyingTimer;

        private enum Registers
        {
            AlertTest = 0x0,
            Control0 = 0x4,
            InterruptEnableHart0 = 0x100,
            InterruptStatusHart0 = 0x104,
            InterruptTestHart0 = 0x108,
            ConfigurationHart0 = 0x10c,
            ValueLowHart0 = 0x110,
            ValueHighHart0 = 0x114,
            CompareLowHart0 = 0x118,
            CompareHighHart0 = 0x11c,
        }
    }
}
