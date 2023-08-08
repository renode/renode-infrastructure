//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF52840_RNG : BasicDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_RNG(IMachine machine) : base(machine)
        {
            rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public event Action<uint> EventTriggered;

        private void DefineRegisters()
        {
            Registers.Start.Define(this)
                .WithFlag(0, out started, FieldMode.Write, name: "TASKS_START")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update())
            ;

            Registers.Stop.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => started.Value &= !value, name: "TASKS_STOP")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update())
            ;

            Registers.ValueReady.Define(this)
                .WithFlag(0, writeCallback: (_, value) =>
                {
                    if(!value)
                    {
                        IRQ.Unset();
                    }
                    Update();
                }, valueProviderCallback: _ => started.Value, name: "EVENTS_VALRDY")
                .WithReservedBits(1, 31)
            ;

            Registers.Shorts.Define(this, name: "SHORTS")
                .WithFlag(0, out readyToStopShortEnabled, name: "VALRDY_STOP")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update())
            ;

            Registers.InterruptEnableSet.Define(this, name: "INTENSET")
                .WithFlag(0, out interruptEnabled, FieldMode.Set | FieldMode.Read, name: "VALRDY")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update())
            ;

            Registers.InterruptEnableClear.Define(this, name: "INTENCLR")
                .WithFlag(0, writeCallback: (_, value) => interruptEnabled.Value &= !value, valueProviderCallback: _ => interruptEnabled.Value, name: "VALRDY")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update())
            ;

            Registers.Config.Define(this, name: "CONFIG")
                .WithTaggedFlag("DERCEN", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.Value.Define(this, name: "VALUE")
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => started.Value ? (uint)rng.Next(0, byte.MaxValue) : 0u, name: "VALUE")
                .WithReservedBits(8, 24)
            ;
        }

        private void Update()
        {
            var value = started.Value && interruptEnabled.Value;
            if(value)
            {
                this.Log(LogLevel.Noisy, "Generated new interrupt for RNG");
                EventTriggered?.Invoke(0);
            }
            IRQ.Set(value);
            if(started.Value && readyToStopShortEnabled.Value)
            {
                started.Value = false;
                IRQ.Set(false);
            }
        }

        private readonly PseudorandomNumberGenerator rng;
        private IFlagRegisterField started;
        private IFlagRegisterField readyToStopShortEnabled;
        private IFlagRegisterField interruptEnabled;

        private enum Registers
        {
            Start = 0x0,
            Stop = 0x4,
            ValueReady = 0x100,
            Shorts = 0x200,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            Config = 0x504,
            Value = 0x508
        }
    }
}
