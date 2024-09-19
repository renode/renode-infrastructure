//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF_CLOCK : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF_CLOCK(IMachine machine) : base(machine)
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

            // nRF52840 documentation doesn't acknowledge hfclk192, but for USB pullup to happen
            // under Zephyr, those flags and IRQ have to be handled.
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            lfclkStarted = false;
            hfclkStarted = false;
            Update();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void Update()
        {
            bool irq = (lfclkEventGenerated.Value && lfclkStartedEventEnabled.Value)
                    || (hfclkEventGenerated.Value && hfclkStartedEventEnabled.Value)
                    || (hfclk192mStartedEventEnabled.Value && hfclk192mStarted);
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irq);
            IRQ.Set(irq);
        }

        private void DefineRegisters()
        {
            Registers.HFXOCrystalOscillatorStarted.Define(this)
                .WithFlag(0, out hfclkEventGenerated, name: "EVENTS_HFCLKSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update());

            Registers.LFCLKStarted.Define(this)
                .WithFlag(0, out lfclkEventGenerated, name: "EVENTS_LFCLKSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => Update());

            Registers.StartHFXO.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    hfclkStarted = true;
                    hfclkEventGenerated.Value = true;
                    Update();
                }, name: "TASK_HFCLKSTART")
                .WithReservedBits(1, 31);

            Registers.StopHFXO.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    hfclkStarted = false;
                    hfclkEventGenerated.Value = false;
                    Update();
                }, name: "TASK_HFCLKSTOP")
                .WithReservedBits(1, 31);

            Registers.StartLFCLK.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    lfclkStarted = true;
                    lfclkEventGenerated.Value = true;
                    Update();
                }, name: "TASK_LFCLKSTART")
                .WithReservedBits(1, 31);
            
            Registers.StartHfClock192M.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                        hfclk192mStarted = true;
                        Update();
                }, name: "TASKS_HFCLK192MSTART")
                .WithReservedBits(1, 31);

            Registers.PowerUsbRegisterStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "VBUSDETECT")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "OUTPUTRDY")
                .WithReservedBits(2, 30);

            Registers.PowerUSBUsbRemovedEvent.Define(this)
                .WithFlag(0, valueProviderCallback: _ => false, name: "EVENTS_USBDETECTED")
                .WithReservedBits(1, 31);

            Registers.PowerUSBDetectedEvent.Define(this)
                .WithFlag(0, 
                    valueProviderCallback: _ => true, name: "EVENTS_USBREMOVED")
                .WithReservedBits(1, 31);

            Registers.PowerUSBPowerReadyEvent.Define(this)
                .WithFlag(0, valueProviderCallback: _ => true, name: "EVENTS_USBPWRRDY")
                .WithReservedBits(1, 31);

            Registers.StopLFCLK.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    lfclkStarted = false;
                    lfclkEventGenerated.Value = false;
                    Update();
                }, name: "TASK_LFCLKSTOP")
                .WithReservedBits(1, 31);
            Registers.EnableInterrupt.Define(this)
                .WithFlag(0, out hfclkStartedEventEnabled, FieldMode.Read | FieldMode.Set, name: "HFCLKSTARTED")
                .WithFlag(1, out lfclkStartedEventEnabled, FieldMode.Read | FieldMode.Set, name: "LFCLKSTARTED")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("DONE", 3)
                .WithTaggedFlag("CTTO", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("DONE (nRF5340)", 7) // nRF5340, but also nRF52840 USB
                .WithFlag(8, out hfclk192mAudioStartedEventEnabled, FieldMode.Read | FieldMode.Set, name: "HFCLKAUDIOSTARTED") // nRF5340, but also nRF52840 with USB
                .WithFlag(9, out hfclk192mStartedEventEnabled, FieldMode.Read | FieldMode.Set, name: "HFCLK192MSTARTED") // nRF5340, but also nRF52840 with USB
                .WithTaggedFlag("CTSTARTED", 10)
                .WithTaggedFlag("CTSTOPPED", 11)
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => Update());

            Registers.DisableInterrupt.Define(this)
                .WithFlag(0, 
                    writeCallback: (_, value) => hfclkStartedEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfclkStartedEventEnabled.Value, name: "HFCLKSTARTED")
                .WithFlag(1,
                    writeCallback: (_, value) => lfclkStartedEventEnabled.Value &= !value,
                    valueProviderCallback: _ => lfclkStartedEventEnabled.Value, name: "LFCLKSTARTED")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("DONE", 3)
                .WithTaggedFlag("CTTO", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("DONE (nRF540)", 7) // nRF5340, but also nRF52840 USB
                .WithFlag(8,
                    writeCallback: (_, value) => hfclk192mAudioStartedEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfclk192mAudioStartedEventEnabled.Value, name: "HFCLKAUDIOSTARTED") // nRF5340, but also nRF52840 with USB
                .WithFlag(9,
                    writeCallback: (_, value) => hfclk192mStartedEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfclk192mStartedEventEnabled.Value, name: "HFCLK192MSTARTED") // nRF5340, but also nRF52840 with USB
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

            Registers.HFCLKStatus.Define(this)
                // true in the first bit indicates that hfclk is started. Not sure if it's possible to return true in STATE and false in SRC
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => hfclkStarted, name: "SRC")
                .WithReservedBits(1, 15)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => hfclkStarted, name: "STATE")
                .WithReservedBits(17, 15);

            Registers.LFCLKStatus.Define(this)
                .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => lfclkSource.Value, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => lfclkStarted, name: "STATE")
                .WithReservedBits(17, 15);

            Registers.PowerUSBMainRegisterStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "MAINREGSTATUS")
                .WithReservedBits(1, 31);
        }

        private bool lfclkStarted;
        private bool hfclkStarted;
        private bool hfclk192mStarted;
        private IFlagRegisterField lfclkStartedEventEnabled;
        private IFlagRegisterField lfclkEventGenerated;
        private IFlagRegisterField hfclkStartedEventEnabled;
        private IFlagRegisterField hfclkEventGenerated;
        private IFlagRegisterField hfclk192mStartedEventEnabled;
        private IFlagRegisterField hfclk192mAudioStartedEventEnabled;

        private enum Registers
        {
            StartHFXO = 0x0,
            StopHFXO = 0x4,
            StartLFCLK = 0x8,
            StopLFCLK = 0xC,
            StartLFRCCallibration = 0x10,
            StartCallibrationTimer = 0x14,
            StopCallibrationTimer = 0x18,
            StartHfClock192M = 0x20, // nRF5340
            HFXOCrystalOscillatorStarted = 0x100,
            LFCLKStarted = 0x104,
            CalibrationOfLFRCCompleted = 0x10C,
            CalibrationTimerTimeout = 0x110,
            PowerUSBDetectedEvent = 0x11C, // POWER peripheral
            PowerUSBUsbRemovedEvent = 0x120, // POWER peripheral
            PowerUSBPowerReadyEvent = 0x124, // POWER peripheral
            CalibrationTimerStarted = 0x128,
            CalibrationTimerStopped = 0x12C,
            EnableInterrupt = 0x304,
            DisableInterrupt = 0x308,
            HFCLKStartTriggered = 0x408,
            HFCLKStatus = 0x40C,
            LFCLKStartTriggered = 0x414,
            LFCLKStatus = 0x418,
            LFCLKClockSourceCopy = 0x41C,
            PowerUsbRegisterStatus = 0x438,
            LFCLKClockSource = 0x518,
            HFXODebounceTime = 0x528,
            CallibrationTimerInterval = 0x538,
            TraceConfig = 0x55C,
            LFRCModeConfiguration = 0x5B4,
            PowerUSBMainRegisterStatus = 0x640
        }
    }
}
