//
// Copyright (c) 2010-2019 Antmicro
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
    public class Murax_Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        public Murax_Timer(Machine machine, long frequency = 12000000) : base(machine)
        {
            DefineRegisters();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                var j = i;
                innerTimers[j] = new LimitTimer(machine.ClockSource, frequency, this, ((Timer)j).ToString(), limit: ushort.MaxValue, eventEnabled: true, direction: Direction.Ascending, autoUpdate: true, workMode: WorkMode.OneShot);
                innerTimers[j].LimitReached += delegate
                {
                    this.Log(LogLevel.Noisy, "{0}: limit reached", (Timer)j);
                    interruptPending[j].Value = true;
                    UpdateInterrupts();
                };
            }
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var timer in innerTimers)
            {
                timer.Reset();
            }
            UpdateInterrupts();
        }

        public long Size => 0x5C;

        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Prescaler.Define(this)
                .WithValueField(0, 32, out prescaler, name: "prescaler", writeCallback: (_, val) =>
                    {
                        for(var i = 0; i < NumberOfTimers; i++)
                        {
                            UpdatePrescaler(i);
                        }
                    })
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, out interruptPending[(int)Timer.TimerA], FieldMode.Read | FieldMode.WriteOneToClear, name: "timerA pending")
                .WithFlag(1, out interruptPending[(int)Timer.TimerB], FieldMode.Read | FieldMode.WriteOneToClear, name: "timerB pending")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out interruptEnable[(int)Timer.TimerA], name: "timerA interrupt enable")
                .WithFlag(1, out interruptEnable[(int)Timer.TimerB], name: "timerB interrupt enable")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TimerAClearTicks.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithEnumField<DoubleWordRegister, EnableMode>(0, 2, out enableMode[idx], name: "timerEnable",
                    writeCallback: (_, val) =>
                    {
                        UpdatePrescaler(idx);
                        innerTimers[idx].Enabled = (val != EnableMode.Disabled);
                    });
                reg.WithFlag(16, name: "periodic",
                    writeCallback: (_, val) =>
                    {
                        innerTimers[idx].Mode = val ? WorkMode.Periodic : WorkMode.OneShot;
                    });
            });

            Registers.TimerALimit.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32,
                    valueProviderCallback: _ => (uint)innerTimers[(int)Timer.TimerA].Limit,
                    writeCallback: (_, value) =>
                    {
                        // the effective limit value is 1 higher than the value written to the register
                        innerTimers[idx].Limit = value + 1;
                    }
                );
            });

            Registers.TimerAValue.DefineMany(this, NumberOfTimers, stepInBytes: TimersRegistersOffset, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32,
                    valueProviderCallback: _ => (uint)innerTimers[(int)Timer.TimerA].Value,
                    writeCallback: (_, value) =>
                    {
                        // writing any value to this register clears the value
                        innerTimers[idx].Value = 0;
                    }
                );
            });
        }

        private void UpdateInterrupts()
        {
            var anyPending = interruptEnable.Select(x => x.Value)
                .Zip(interruptPending.Select(x => x.Value), (enabled, pending) => enabled && pending)
                .Any(x => x);

            this.Log(LogLevel.Noisy, "Setting IRQ to: {0}", anyPending);
            IRQ.Set(anyPending);
        }

        private void UpdatePrescaler(int timerIdx)
        {
            // the effective prescaler's value is one higher than the value written to the register
            innerTimers[timerIdx].Divider = (enableMode[timerIdx].Value == EnableMode.Prescaler) ? (int)(prescaler.Value + 1) : 1;
        }

        private IValueRegisterField prescaler;
        private IEnumRegisterField<EnableMode>[] enableMode = new IEnumRegisterField<EnableMode>[NumberOfTimers];

        private readonly IFlagRegisterField[] interruptEnable = new IFlagRegisterField[NumberOfTimers];
        private readonly IFlagRegisterField[] interruptPending = new IFlagRegisterField[NumberOfTimers];
        private readonly LimitTimer[] innerTimers = new LimitTimer[NumberOfTimers];

        private const int NumberOfTimers = 2;
        private const int TimersRegistersOffset = 0x10;

        private enum Timer
        {
            TimerA,
            TimerB
        }

        // interpret this as two bits indicating if:
        // * direct clock is enabled
        // * clock routed through prescaler is enabled
        //
        // In case both are set, direct clock wins.
        private enum EnableMode
        {
            Disabled,
            NoPrescaler,
            Prescaler,
            BothPrescalerAndNoPrescaler
        }

        private enum Registers
        {
            Prescaler = 0x0,
            InterruptStatus = 0x10,
            InterruptMask = 0x14,

            TimerAClearTicks = 0x40,
            TimerALimit = 0x44,
            TimerAValue = 0x48,

            TimerBClearTicks = 0x50,
            TimerBLimit = 0x54,
            TimerBValue = 0x58
        }
    }
}
