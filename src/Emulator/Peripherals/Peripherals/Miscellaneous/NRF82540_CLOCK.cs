//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF82540_CLOCK : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF82540_CLOCK(Machine machine) : base(machine)
        {
            // Renode, in general, does not include clock control peripherals.
            // While this is doable, it seldom benefits real software development
            // and is very cumbersome to maintain.
            //
            // To properly support the CLOCK peripheral, we need to add this stub class.
            // It is common in Renode that whenever a register is implemented, it
            // either contains actual logic or tags, indicating not implemented fields.
            //
            // Here, however, we want to fake most of the registers as r/w values.
            // Usually we implemented this logic with Python peripherals.
            //
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            lfclkStarted = false;
            Update();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void Update()
        {
            IRQ.Set(lfclkEventGenerated.Value && lfclkStartedEventEnabled.Value);
        }

        private void DefineRegisters()
        {
            Registers.LFCLKStarted.Define(this)
                .WithFlag(0, out lfclkEventGenerated, name: "EVENTS_LFCLKSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update());

            Registers.StartLFCLK.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                        lfclkStarted = true;
                        lfclkEventGenerated.Value = true;
                        Update();
                }, name: "TASK_LFCLKSTART")
                .WithReservedBits(1, 31);

            Registers.EnableInterrupt.Define(this)
                .WithTaggedFlag("HFCLKSTARTED", 0)
                .WithFlag(1, out lfclkStartedEventEnabled, name: "LFCLKSTARTED")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("DONE", 3)
                .WithTaggedFlag("CTTO", 4)
                .WithReservedBits(5, 5)
                .WithTaggedFlag("CTSTARTED", 10)
                .WithTaggedFlag("CTSTOPPED", 11)
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => Update());

            Registers.DisableInterrupt.Define(this)
                .WithTaggedFlag("HFCLKSTARTED", 0)
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, changeCallback: (_, value) => lfclkStartedEventEnabled.Value = !value, valueProviderCallback: _ => lfclkStartedEventEnabled.Value, name: "LFCLKSTARTED")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("DONE", 3)
                .WithTaggedFlag("CTTO", 4)
                .WithReservedBits(5, 5)
                .WithTaggedFlag("CTSTARTED", 10)
                .WithTaggedFlag("CTSTOPPED", 11)
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => Update());

            Registers.LFCLKClockSource.Define(this)
                .WithValueField(0, 2, out var lfclkSource, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, name: "BYPASS")
                .WithFlag(17, name: "EXTERNAL")
                .WithReservedBits(18, 14);

            Registers.LFCLKStatus.Define(this)
                .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => lfclkSource.Value, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => lfclkStarted, name: "STATE")
                .WithReservedBits(17, 15);
        }

        private bool lfclkStarted;
        private IFlagRegisterField lfclkStartedEventEnabled;
        private IFlagRegisterField lfclkEventGenerated;

        private enum Registers
        {
            StartHFXO = 0x0,
            StopHFXO = 0x4,
            StartLFCLK = 0x8,
            StopLFCLK = 0xC,
            StartLFRCCallibration = 0x10,
            StartCallibrationTimer = 0x14,
            StopCallibrationTimer = 0x18,
            HFXOCrystalOscillatorStarted = 0x100,
            LFCLKStarted = 0x104,
            CalibrationOfLFRCCompleted = 0x10C,
            CalibrationTimerTimeout = 0x110,
            CalibrationTimerStarted = 0x128,
            CalibrationTimerStopped = 0x12C,
            EnableInterrupt = 0x304,
            DisableInterrupt = 0x308,
            HFCLKStartTriggered = 0x408,
            HFCLKStatus = 0x40C,
            LFCLKStartTriggered = 0x414,
            LFCLKStatus = 0x418,
            LFCLKClockSourceCopy = 0x41C,
            LFCLKClockSource = 0x518,
            HFXODebounceTime = 0x528,
            CallibrationTimerInterval = 0x538,
            TraceConfig = 0x55C,
            LFRCModeConfiguration = 0x5B4
        }
    }
}
