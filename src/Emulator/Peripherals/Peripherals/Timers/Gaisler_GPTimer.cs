//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static Antmicro.Renode.Peripherals.Bus.GaislerAPBPlugAndPlayRecord;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Gaisler_GPTimer : BasicDoubleWordPeripheral, IKnownSize, IGaislerAPB, INumberedGPIOOutput
    {
        public Gaisler_GPTimer(IMachine machine, uint numberOfTimers = 4, int scalerWidth = 8,
            int frequency = DefaultTimerFrequency, bool supportsTimeLatch = false, bool separateInterrupts = true) : base(machine)
        {
            if(numberOfTimers < 1 || numberOfTimers > MaximumNumberOfTimers)
            {
                throw new ConstructionException($"Unsupported number of timers {numberOfTimers}, must be in range [1; {MaximumNumberOfTimers}]");
            }
            if(scalerWidth < 1 || scalerWidth > 32)
            {
                throw new ConstructionException($"Unsupported scaler width {scalerWidth}, must be in range [1; 32]");
            }
            this.numberOfTimers = numberOfTimers;
            this.scalerWidth = scalerWidth;
            scalerResetValue = (uint)BitHelper.Bits(0, scalerWidth);
            this.supportsTimeLatch = supportsTimeLatch;
            this.separateInterrupts = separateInterrupts;

            timers = new TimerUnit[numberOfTimers];
            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfTimers; i++)
            {
                timers[i] = new TimerUnit(machine.ClockSource, frequency, this, i);
                connections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(connections);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var timer in timers)
            {
                timer.Reset();
            }
            ScalerReloadValue = (int)scalerResetValue + 1;
        }

        public uint GetVendorID() => VendorID;

        public uint GetDeviceID() => supportsTimeLatch ? DeviceIDWithLatch : DeviceID;

        public SpaceType GetSpaceType() => SpaceType.APBIOSpace;

        public uint GetInterruptNumber() => this.GetCpuInterruptNumber(Connections[0]);

        public long Size => 0x100;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            Registers.ScalerValue.Define(this, scalerResetValue)
                .WithValueField(0, scalerWidth, name: "scalerValue",
                    readCallback: (_, __) => this.WarningLog("Reading the scaler value is not supported"),
                    writeCallback: (_, __) => this.WarningLog("Setting the scaler value is not supported"))
                .WithReservedBits(scalerWidth, 32 - scalerWidth);

            Registers.ScalerReloadValue.Define(this, scalerResetValue)
                .WithValueField(0, scalerWidth, name: "scalerReloadValue",
                    valueProviderCallback: _ => (ulong)ScalerReloadValue - 1, changeCallback: (_, v) => ScalerReloadValue = (int)v + 1)
                .WithReservedBits(scalerWidth, 32 - scalerWidth);

            Registers.Configuration.Define(this)
                .WithValueField(0, 3, FieldMode.Read, name: "timers", valueProviderCallback: _ => numberOfTimers)
                .WithValueField(3, 5, FieldMode.Read, name: "firstIrq", valueProviderCallback: _ => GetInterruptNumber())
                .WithFlag(8, FieldMode.Read, name: "separateInterrupts", valueProviderCallback: _ => separateInterrupts)
                .WithTaggedFlag("disableFreeze", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("enableLatching", 11)
                .WithReservedBits(12, 20);

            if(supportsTimeLatch)
            {
                Registers.LatchConfiguration.Define(this)
                    .WithTag("latchSelect", 0, 32);
            }

            // Each timer unit has 3 or 4 registers depending on time latch support
            Registers.Timer1CounterValue.DefineMany(this, numberOfTimers, stepInBytes: TimerStride, setup: (register, timerIndex) => register
                .WithValueField(0, 32, name: "counterValue",
                    valueProviderCallback: _ => timers[timerIndex].Value,
                    writeCallback: (_, value) => timers[timerIndex].Value = value)
            );

            // Normally a reload value of 9 means the interrupt will fire every 10 ticks, but
            // the GRLIB BSP writes 0xffffffff here when resetting the timer, so we clamp the limit.
            Registers.Timer1ReloadValue.DefineMany(this, numberOfTimers, stepInBytes: TimerStride, setup: (register, timerIndex) => register
                .WithValueField(0, 32, name: "reloadValue",
                    valueProviderCallback: _ => timers[timerIndex].Limit - 1,
                    changeCallback: (_, value) => timers[timerIndex].Limit = Math.Min(value + 1, uint.MaxValue))
            );

            Registers.Timer1Control.DefineMany(this, numberOfTimers, stepInBytes: TimerStride, setup: (register, timerIndex) => register
                .WithFlag(0, name: "enable",
                    valueProviderCallback: _ => timers[timerIndex].Enabled,
                    changeCallback: (_, v) => timers[timerIndex].Enabled = v)
                .WithFlag(1, name: "restart",
                    valueProviderCallback: _ => timers[timerIndex].AutoReload,
                    changeCallback: (_, v) => timers[timerIndex].AutoReload = v)
                .WithFlag(2, FieldMode.WriteOneToClear, name: "load", writeCallback: (_, v) =>
                    {
                        if(v)
                        {
                            timers[timerIndex].Value = timers[timerIndex].Limit;
                        }
                    })
                .WithFlag(3, out timers[timerIndex].interruptEnable, name: "interruptEnable",
                    changeCallback: (_, __) => UpdateInterrupt(timerIndex))
                .WithFlag(4, out timers[timerIndex].interruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "interruptPending",
                    changeCallback: (_, __) => UpdateInterrupt(timerIndex)) // Cleared by writing '1' as in the newer hardware revision
                .WithTaggedFlag("chain", 5)
                .WithTaggedFlag("debugHalt", 6)
                .WithReservedBits(7, 25)
            );

            if(supportsTimeLatch)
            {
                Registers.Timer1Latch.DefineMany(this, numberOfTimers, stepInBytes: TimerStride, setup: (register, timerIndex) => register
                    .WithTag("latchedTimerCounterValue", 0, 32)
                );
            }
        }

        private void UpdateInterrupt(int index)
        {
            if(!separateInterrupts)
            {
                var timer = timers[index];
                var state = timer.interruptEnable.Value && timer.interruptPending.Value;
                if(state)
                {
                    this.NoisyLog("Signaling IRQ");
                    Connections[0].Blink();
                }
                return;
            }

            if(timers[index].interruptEnable.Value && timers[index].interruptPending.Value)
            {
                this.NoisyLog("Signaling IRQ {0}", index);
                Connections[index].Blink();
            }
        }

        private int ScalerReloadValue
        {
            get
            {
                // All timers share the same scaler so we just take the first one here
                return timers[0].Divider;
            }
            set
            {
                foreach(var timer in timers)
                {
                    timer.Divider = value;
                }
            }
        }

        private readonly TimerUnit[] timers;
        private readonly uint scalerResetValue;
        private readonly uint numberOfTimers;
        private readonly int scalerWidth;
        private readonly bool supportsTimeLatch;
        private readonly bool separateInterrupts;

        private const uint VendorID = 0x01; // Gaisler Research
        private const uint DeviceID = 0x011; // GPTIMER
        private const uint DeviceIDWithLatch = 0x038; // GRTIMER
        private const int DefaultTimerFrequency = 1000000;
        private const int MaximumNumberOfTimers = 7;
        private const uint TimerStride = Registers.Timer2CounterValue - Registers.Timer1CounterValue;

        private class TimerUnit : ITimer
        {
            public TimerUnit(IClockSource clockSource, long frequency, Gaisler_GPTimer parent, int index)
            {
                this.parent = parent;
                this.index = index;
                timer = new LimitTimer(clockSource, frequency, parent, $"timer{index}", limit: uint.MaxValue, eventEnabled: true);
                timer.LimitReached += OnLimitReached;
            }

            public void Reset()
            {
                timer.Reset();
                parent.UpdateInterrupt(index);
            }

            public ulong Value
            {
                get => timer.Value;
                set => timer.Value = value;
            }

            public bool Enabled
            {
                get => timer.Enabled;
                set => timer.Enabled = value;
            }

            public long Frequency
            {
                get => timer.Frequency;
                set => timer.Frequency = value;
            }

            public ulong Limit
            {
                get => timer.Limit;
                set => timer.Limit = value;
            }

            public int Divider
            {
                get => timer.Divider;
                set => timer.Divider = value;
            }

            public bool AutoReload
            {
                get => timer.Mode == WorkMode.Periodic;
                set => timer.Mode = value ? WorkMode.Periodic : WorkMode.OneShot;
            }

            private void OnLimitReached()
            {
                if(interruptEnable.Value)
                {
                    interruptPending.Value = true;
                    parent.UpdateInterrupt(index);
                }
            }

            public IFlagRegisterField interruptEnable;
            public IFlagRegisterField interruptPending;

            private readonly Gaisler_GPTimer parent;
            private readonly LimitTimer timer;
            private readonly int index;
        }

        private enum Registers : uint
        {
            ScalerValue = 0x00,
            ScalerReloadValue = 0x04,
            Configuration = 0x08,
            LatchConfiguration = 0x0c,
            Timer1CounterValue = 0x10,
            Timer1ReloadValue = 0x14,
            Timer1Control = 0x18,
            Timer1Latch = 0x1c,
            Timer2CounterValue = 0x20,
            Timer2ReloadValue = 0x24,
            Timer2Control = 0x28,
            Timer2Latch = 0x2c,
            Timer3CounterValue = 0x30,
            Timer3ReloadValue = 0x34,
            Timer3Control = 0x38,
            Timer3Latch = 0x3c,
            Timer4CounterValue = 0x40,
            Timer4ReloadValue = 0x44,
            Timer4Control = 0x48,
            Timer4Latch = 0x4c,
            Timer5CounterValue = 0x50,
            Timer5ReloadValue = 0x54,
            Timer5Control = 0x58,
            Timer5Latch = 0x5c,
            Timer6CounterValue = 0x60,
            Timer6ReloadValue = 0x64,
            Timer6Control = 0x68,
            Timer6Latch = 0x6c,
            Timer7CounterValue = 0x70,
            Timer7ReloadValue = 0x74,
            Timer7Control = 0x78,
            Timer7Latch = 0x7c,
        }
    }
}
