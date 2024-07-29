//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MiV_CoreTimer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public MiV_CoreTimer(IMachine machine, long clockFrequency) : base(machine.ClockSource, clockFrequency, limit: uint.MaxValue, autoUpdate: true, eventEnabled: true)
        {
            this.machine = machine;
            IRQ = new GPIO();
            LimitReached += delegate
            {
                IRQ.Set(true);
            };

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Load, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "LoadValue",
                        writeCallback: (_, val) =>
                            {
                                Limit = val;
                                if(Mode == WorkMode.OneShot)
                                {
                                    Enabled = true;
                                }
                            },
                        valueProviderCallback: (_) => checked((uint)Limit)
                    )
                },

                {(long)Registers.Value, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        return checked((uint)Value);
                    }, name: "CurrentValue")},

                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) => Enabled = val, valueProviderCallback: _ => Enabled, name: "TimerEnable")
                    .WithFlag(1, writeCallback: (_, val) => EventEnabled = val, valueProviderCallback: _ => EventEnabled, name: "InterruptEnable")
                    .WithValueField(2, 1, writeCallback: (_, val) => Mode = val == 0 ? WorkMode.Periodic : WorkMode.OneShot, valueProviderCallback: _ => Mode == WorkMode.OneShot ? 1 : 0u, name: "TimerMode")},
                    // bits 31:3 not used according to the documentation

                {(long)Registers.ClockPrescale, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "Prescale", writeCallback: (_, val) =>
                        {
                            Divider = (2 << (val < 9 ? (int)val : 9));
                        }, valueProviderCallback: _ =>
                        {
                            var currDivider = (Divider >> 1) - 1;
                            var result = 0u;
                            while(currDivider != 0)
                            {
                                currDivider >>= 1;
                                result++;
                            }
                            return result;
                        })},

                {(long)Registers.InterruptClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, __) => { IRQ.Set(false); this.ClearInterrupt(); })},

                {(long)Registers.RawInterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => this.RawInterrupt)},

                {(long)Registers.MaskedInterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => this.Interrupt)}
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            IRQ.Set(false);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; private set; }

        public long Size => 0x1C;

        private readonly DoubleWordRegisterCollection registers;
        private readonly IMachine machine;

        private enum Registers : long
        {
            Load = 0x0,
            Value = 0x04,
            Control = 0x08,
            ClockPrescale = 0x0C,
            InterruptClear = 0x10,
            RawInterruptStatus = 0x14,
            MaskedInterruptStatus = 0x18
        }
    }
}
