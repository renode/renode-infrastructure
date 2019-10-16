//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32_RTCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32_RTCC(Machine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();

            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "rtcc", enabled: false, eventEnabled: true);
            innerTimer.CompareReached += () => { channel0InterruptPending.Value = true; Update(); };
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            innerTimer.Reset();
            IRQ.Set(false);
        }

        public long Size => 0x400;

        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.Control.Define(this)
                .WithFlag(0,
                    writeCallback: (_, val) => { innerTimer.Enabled = val; },
                    valueProviderCallback: _ => innerTimer.Enabled,
                    name: "ENABLE")
                .WithValueField(8, 4,
                    writeCallback: (_, val) => { innerTimer.Divider = (uint)Math.Pow(2, val); },
                    valueProviderCallback: _ => (uint)Math.Log(innerTimer.Divider, 2),
                    name: "CNTPRESC");

            Register.CounterValue.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => (uint)innerTimer.Value,
                    writeCallback: (_, value) => { innerTimer.Value = value; },
                    name: "CNT");

            Register.InterruptFlags.Define(this)
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => channel0InterruptPending.Value, name: "CC0");

            Register.InterruptFlagSet.Define(this)
                .WithFlag(1, FieldMode.Set, changeCallback: (_, val) => { channel0InterruptPending.Value = val; },
                    name: "CC0")
                .WithWriteCallback((_, __) => Update());

            Register.InterruptFlagClear.Define(this)
                .WithFlag(1, out channel0InterruptPending, FieldMode.ReadToClear | FieldMode.WriteOneToClear, name: "CC0")
                .WithWriteCallback((_, __) => Update())
                .WithReadCallback((_, __) => Update());

            Register.InterruptEnable.Define(this)
                .WithFlag(1, out channel0InterruptEnabled, name: "CC0")
                .WithWriteCallback((_, __) => Update());

            Register.CaptureValueC0.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => { innerTimer.Compare = val; },
                    name: "CCV");

            Register.Retention0.DefineMany(this, NumberOfRetentionRegisters, (reg, idx) =>
                reg.WithValueField(0, 32, name: "REG")); //these registers store user-written values, no additional logic
        }

        private void Update()
        {
            IRQ.Set(channel0InterruptPending.Value && channel0InterruptEnabled.Value);
        }

        private IFlagRegisterField channel0InterruptEnabled;
        private IFlagRegisterField channel0InterruptPending;
        private readonly ComparingTimer innerTimer;

        private const int NumberOfRetentionRegisters = 32;

        private enum Register
        {
            Control = 0x0,
            PreCounter = 0x4,
            CounterValue = 0x8,
            CombinedPreCouterValue = 0xC,
            Time = 0x10,
            Date = 0x14,
            InterruptFlags = 0x18,
            InterruptFlagSet = 0x1C,
            InterruptFlagClear = 0x20,
            InterruptEnable = 0x24,
            Status = 0x28,
            Command = 0x2C,
            SyncBusy = 0x30,
            PowerDown = 0x34,
            ConfLock = 0x34,
            WakeUpEnable = 0x38,
            ChanelControlC0 = 0x3C,
            CaptureValueC0 = 0x44,
            CaptureTimeC0 = 0x48,
            CaptureDateC0 = 0x4C,
            ChanelControlC1 = 0x50,
            CaptureValueC1 = 0x54,
            CaptureTimeC1 = 0x58,
            CaptureDateC1 = 0x5C,
            ChanelControlC2 = 0x60,
            CaptureValueC2 = 0x64,
            CaptureTimeC2 = 0x68,
            CaptureDateC2 = 0x6C,
            Retention0 = 0x104,
            Retention1 = 0x108,
            // ...
            Retention31 = 0x180,
        }
    }
}
