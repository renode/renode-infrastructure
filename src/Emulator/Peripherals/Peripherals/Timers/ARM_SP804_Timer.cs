//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ARM_SP804_Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        public ARM_SP804_Timer(IMachine machine, long frequency = 1000000) : base(machine)
        {
            DefineRegisters();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                var j = i;
                innerTimers[j] = new LimitTimer(machine.ClockSource, frequency, this, ((Timer)j).ToString(), limit: uint.MaxValue, eventEnabled: true, autoUpdate: true);
                innerTimers[j].LimitReached += delegate
                {
                    this.Log(LogLevel.Noisy, "{0}: limit reached", (Timer)j);

                    if(backgroundLimitSet[j])
                    {
                        innerTimers[j].Limit = backgroundLimit[j];
                        backgroundLimitSet[j] = false;
                    }

                    interruptPending[j].Value = true;
                    UpdateInterrupts();
                };
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                innerTimers[i].Reset();
                innerTimers[i].Limit = TimerLimitResetValue;
                backgroundLimit[i] = TimerLimitResetValue;
                backgroundLimitSet[i] = false;
            }
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Timer1Load.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32, name: "Load",
                    valueProviderCallback: _ => innerTimers[idx].Limit,
                    writeCallback: (_, value) =>
                    {
                        innerTimers[idx].Limit = value;
                    }
                );
            });

            Registers.Timer1Value.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, resetValue: 0xFFFFFFFF, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32, FieldMode.Read, name: "Value",
                    valueProviderCallback: _ =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            // being here means we are on the CPU thread
                            cpu.SyncTime();
                        }
                        return (uint)innerTimers[idx].Value;
                    }
                );
            });

            Registers.Timer1Control.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, resetValue: 0x20, setup: (reg, idx) =>
            {
                reg.WithFlag(0, name: "One Shot",
                    writeCallback: (_, val) =>
                    {
                        innerTimers[idx].Mode = val ? WorkMode.OneShot : WorkMode.Periodic;
                    });
                reg.WithTaggedFlag("Timer Size", 1);
                reg.WithEnumField<DoubleWordRegister, PrescalerMode>(2, 2, name: "Timer Prescaler",
                    writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case PrescalerMode.NoPrescaler:
                                innerTimers[idx].Divider = 1;
                                break;
                            case PrescalerMode.Prescaler16:
                                innerTimers[idx].Divider = 16;
                                break;
                            case PrescalerMode.Prescaler256:
                                innerTimers[idx].Divider = 256;
                                break;
                            case PrescalerMode.Undefined:
                                this.Log(LogLevel.Error, "Timer{0} prescaler set to an undefined value!", idx);
                                break;
                        }
                    });
                reg.WithReservedBits(4, 1);
                reg.WithFlag(5, out interruptEnable[idx], name: "Interrupt Enable",
                    writeCallback: (_, val) =>
                    {
                        UpdateInterrupts();
                    });
                reg.WithTaggedFlag("Timer Mode", 6);
                reg.WithFlag(7, name: "Timer Enable",
                    writeCallback: (_, val) =>
                    {
                        innerTimers[idx].Enabled = val;
                    });
                reg.WithReservedBits(8, 24);
                reg.WithWriteCallback((_, __) =>
                {
                    if(innerTimers[idx].Enabled)
                    {
                        this.Log(LogLevel.Warning, "Timer{0}: Writing to the Control register while the timer is running!", idx);
                    }
                });
            });

            Registers.Timer1InterruptClear.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithFlag(0, out interruptPending[idx], FieldMode.WriteOneToClear, name: "Interrupt Clear Mask",
                    writeCallback: (_, val) =>
                    {
                        UpdateInterrupts();
                    });
                reg.WithReservedBits(1, 31);
            });

            Registers.Timer1RawInterruptStatus.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithFlag(0, FieldMode.Read, name: "Raw Interrupt Status",
                    valueProviderCallback: _ => interruptPending[idx].Value);
                reg.WithReservedBits(1, 31);
            });

            Registers.Timer1MaskedInterruptStatus.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithFlag(0, FieldMode.Read, name: "Masked Interrupt Status",
                    valueProviderCallback: _ => interruptEnable[idx].Value && interruptPending[idx].Value);
                reg.WithReservedBits(1, 31);
            });

            Registers.Timer1BackgroundLoad.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32, name: "Background Load",
                    // As per the SP804 TRM, reading from Background Load returns the same value as the Load register
                    valueProviderCallback: _ => (uint)innerTimers[idx].Limit,
                    writeCallback: (_, value) =>
                    {
                        backgroundLimit[idx] = value;
                        backgroundLimitSet[idx] = true;
                    }
                );
            });

            Registers.IntegrationTestControl.Define(this)
                .WithTaggedFlag("Integration Test Enable", 0)
                .WithReservedBits(1, 31);

            Registers.IntegrationTestOutputSet.Define(this)
                .WithTaggedFlag("Timer Interrupt 1", 0)
                .WithReservedBits(1, 31);
        }

        private void UpdateInterrupts()
        {
            var anyPending = interruptEnable.Select(x => x.Value)
                .Zip(interruptPending.Select(x => x.Value), (enabled, pending) => enabled && pending)
                .Any(x => x);

            this.Log(LogLevel.Noisy, "Setting IRQ to: {0}", anyPending);
            IRQ.Set(anyPending);
        }

        private readonly IFlagRegisterField[] interruptEnable = new IFlagRegisterField[NumberOfTimers];
        private readonly IFlagRegisterField[] interruptPending = new IFlagRegisterField[NumberOfTimers];
        private readonly LimitTimer[] innerTimers = new LimitTimer[NumberOfTimers];
        private readonly ulong[] backgroundLimit = new ulong[NumberOfTimers];
        private readonly bool[] backgroundLimitSet = new bool[NumberOfTimers];

        private const int NumberOfTimers = 2;
        private const uint TimersRegistersOffset = 0x20;
        private const uint TimerLimitResetValue = 0xFFFFFFFF;

        private enum PrescalerMode
        {
            NoPrescaler,
            Prescaler16,
            Prescaler256,
            Undefined
        }

        private enum Timer
        {
            Timer1,
            Timer2
        }

        private enum Registers
        {
            Timer1Load = 0x00,
            Timer1Value = 0x04,
            Timer1Control = 0x08,
            Timer1InterruptClear = 0x0C,
            Timer1RawInterruptStatus = 0x10,
            Timer1MaskedInterruptStatus = 0x14,
            Timer1BackgroundLoad = 0x18,

            Timer2Load = 0x20,
            Timer2Value = 0x24,
            Timer2Control = 0x28,
            Timer2InterruptClear = 0x2C,
            Timer2RawInterruptStatus = 0x30,
            Timer2MaskedInterruptStatus = 0x34,
            Timer2BackgroundLoad = 0x38,

            IntegrationTestControl = 0xF00,
            IntegrationTestOutputSet = 0xF04,
        }
    }
}
