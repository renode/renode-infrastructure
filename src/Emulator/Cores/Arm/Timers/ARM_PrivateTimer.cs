//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ARM_PrivateTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public ARM_PrivateTimer(IMachine machine, ulong frequency) : base(machine)
        {
            if(frequency > long.MaxValue)
            {
                throw new ConstructionException($"Timer doesn't support frequency greater than {long.MaxValue}, given {frequency}.");
            }
            Frequency = (long)frequency;

            BuildRegisters();
            timer = new LimitTimer(machine.ClockSource, Frequency, this, "Timer",
                limit: uint.MaxValue, direction: Direction.Descending, workMode: WorkMode.OneShot, autoUpdate: true);
            timer.LimitReached += UpdateInterrupt;
        }

        public override void Reset()
        {
            base.Reset();
            timer.Reset();
            UpdateInterrupt();
        }

        public long Size => 0x200;

        public GPIO IRQ { get; } = new GPIO();
        public long Frequency { get; }

        private void BuildRegisters()
        {
            Registers.Load.Define(this)
                .WithValueField(0, 32, name: "Load",
                    writeCallback: (_, val) => timer.Limit = val,
                    valueProviderCallback: (_) => (uint)timer.Limit
                );
            Registers.Counter.Define(this)
                .WithValueField(0, 32, name: "Counter",
                    writeCallback: (_, val) =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        timer.Value = val;
                    },
                    valueProviderCallback: (_) => (uint)timer.Value
                );
            Registers.Control.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(8, 8, name: "Prescaler",
                    writeCallback: (_, val) => timer.Divider = (int)val + 1,
                    valueProviderCallback: (_) => (ulong)timer.Divider - 1
                )
                .WithReservedBits(3, 5)
                .WithFlag(2, name: "InterruptEnabled",
                    writeCallback: (_, val) => timer.EventEnabled = val,
                    valueProviderCallback: (_) => timer.EventEnabled
                )
                .WithFlag(1, name: "AutoLoad",
                    writeCallback: (_, val) => timer.Mode = val ? WorkMode.Periodic : WorkMode.OneShot,
                    valueProviderCallback: (_) => timer.Mode == WorkMode.Periodic
                )
                .WithFlag(0, name: "Enabled",
                    writeCallback: (_, val) => timer.Enabled = val,
                    valueProviderCallback: (_) => timer.Enabled
                )
                .WithWriteCallback(
                    (_, __) => UpdateInterrupt()
                );
            Registers.InterruptStatus.Define(this)
                .WithReservedBits(1, 31)
                .WithFlag(0, name: "InterruptStatus",
                    writeCallback: (_, val) => { if(val) timer.ClearInterrupt(); },
                    valueProviderCallback: (_) => timer.RawInterrupt
                )
                .WithWriteCallback(
                    (_, __) => UpdateInterrupt()
                );
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(timer.Interrupt);
        }

        private readonly LimitTimer timer;

        private enum Registers : long
        {
            Load = 0x00,
            Counter = 0x04,
            Control = 0x08,
            InterruptStatus = 0x0C
        }
    }
}
