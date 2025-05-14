//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System.Text;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF54L_CLOCK : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF54L_CLOCK(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            hfxoStarted = false;
            pllStarted = false;
            lfclkStarted = false;
            hfxoTuneStarted = false;
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void UpdateInterrupts()
        {
            bool irq = HfxoEvent
                    || PllEvent
                    || LfclkEvent
                    || LfrcCalibrationEvent
                    || HfxoTuneEvent
                    || HfxoTuneErrorEvent
                    || HfxoTuneFailedEvent;
            if(irq)
            {
                var sb = new StringBuilder("Triggering interrupt, events state: ");
                sb.Append($"XOSTARTED={HfxoEvent}, ");
                sb.Append($"PLLSTARTED={PllEvent}, ");
                sb.Append($"LFCLKSTARTED={LfclkEvent}, ");
                sb.Append($"DONE={LfrcCalibrationEvent}, ");
                sb.Append($"XOTUNED={HfxoEvent}, ");
                sb.Append($"XOTUNEERROR={HfxoTuneErrorEvent}, ");
                sb.Append($"XOTUNEFAILED={HfxoTuneFailedEvent}.");
                this.Log(LogLevel.Noisy, sb.ToString());
            }
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irq);
            IRQ.Set(irq);
        }

        private void DefineRegisters()
        {
            Registers.StartHFXO.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        hfxoStarted = true;
                        hfxoEventGenerated.Value = true;
                        UpdateInterrupts();
                    }, name: "TASK_HFCLKSTART")
                .WithReservedBits(1, 31);

            Registers.StopHFXO.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        hfxoStarted = false;
                        hfxoEventGenerated.Value = false;
                        UpdateInterrupts();
                    }, name: "TASK_HFCLKSTOP")
                .WithReservedBits(1, 31);

            Registers.StartPLL.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        pllStarted = true;
                        pllEventGenerated.Value = true;
                        UpdateInterrupts();
                    }, name: "TASKS_PLLSTART")
                .WithReservedBits(1, 31);

             Registers.StopPLL.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        pllStarted = false;
                        pllEventGenerated.Value = false;
                        UpdateInterrupts();
                    }, name: "TASKS_PLLSTOP")
                .WithReservedBits(1, 31);

            Registers.StartLFCLK.Define(this)
                .WithFlag(0, FieldMode.Write, 
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        lfclkStarted = true;
                        lfclkEventGenerated.Value = true;
                        lfclkSourceCopy.Value = lfclkSource.Value;
                        UpdateInterrupts();
                    }, name: "TASK_LFCLKSTART")
                .WithReservedBits(1, 31);

            Registers.StopLFCLK.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        lfclkStarted = false;
                        lfclkEventGenerated.Value = false;
                        UpdateInterrupts();
                    }, name: "TASK_LFCLKSTOP")
                .WithReservedBits(1, 31);

            Registers.StartLFRCCallibration.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        lfrcCalibrationEventGenerated.Value = true;
                        UpdateInterrupts();
                    }, name: "TASK_CAL")
                .WithReservedBits(1, 31);

            Registers.StartHFXOTune.Define(this)
                .WithFlag(0, FieldMode.Write, 
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        hfxoTuneStarted = true;
                        hfxoTuneEventGenerated.Value = true;
                        UpdateInterrupts();
                    }, name: "TASKS_XOTUNE")
                .WithReservedBits(1, 31);

            Registers.StopHFXOTune.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        hfxoTuneStarted = false;
                        hfxoTuneEventGenerated.Value = false;
                        UpdateInterrupts();
                    }, name: "TASKS_XOTUNEABORT")
                .WithReservedBits(1, 31);

            Registers.SubscribeForHFXOStart.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForHFXOStop.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForPLLStart.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForPLLStop.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForLFCLKStart.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForLFCLKStop.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.SubscribeForCalibration.Define(this)
                .WithTag("CHIDX", 0, 8)
                .WithReservedBits(8, 23)
                .WithTaggedFlag("EN", 31);

            Registers.EventsHFXOStarted.Define(this)
                .WithFlag(0, out hfxoEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_XOSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EventsPLLStarted.Define(this)
                .WithFlag(0, out pllEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_PLLSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EventsLFCLKStarted.Define(this)
                .WithFlag(0, out lfclkEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_LFCLKSTARTED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.CalibrationOfLFRCCompleted.Define(this)
                .WithFlag(0, out lfrcCalibrationEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_DONE")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EventsHFXOTuned.Define(this)
                .WithFlag(0, out hfxoTuneEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_XOTUNED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EventsHFXOTuneError.Define(this)
                .WithFlag(0, out hfxoTuneErrorEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_XOTUNEERROR")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EventsHFXOTuneFailed.Define(this)
                .WithFlag(0, out hfxoTuneFailedEventGenerated, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EVENTS_XOTUNEFAILED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EnableOrDisableInterrupt.Define(this)
                .WithFlag(0, out hfxoEventEnabled, name: "XOSTARTED")
                .WithFlag(1, out pllEventEnabled, name: "PLLSTARTED")
                .WithFlag(2, out lfclkEventEnabled, name: "LFCLKSTARTED")
                .WithFlag(3, out lfrcCalibrationEventEnabled, name: "DONE")
                .WithFlag(4, out hfxoTuneEventEnabled, name: "XOTUNED")
                .WithFlag(5, out hfxoTuneErrorEventEnabled, name: "XOTUNEERROR")
                .WithFlag(6, out hfxoTuneFailedEventEnabled, name: "XOTUNEFAILED")
                .WithReservedBits(7, 25)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.EnableInterrupt.Define(this)
                .WithFlag(0,
                    writeCallback: (_, value) => hfxoEventEnabled.Value |= value,
                    valueProviderCallback: _ => hfxoEventEnabled.Value,
                    name: "XOSTARTED")
                .WithFlag(1,
                    writeCallback: (_, value) => pllEventEnabled.Value |= value,
                    valueProviderCallback: _ => pllEventEnabled.Value,
                    name: "PLLSTARTED")
                .WithFlag(2,
                    writeCallback: (_, value) => lfclkEventEnabled.Value |= value,
                    valueProviderCallback: _ => lfclkEventEnabled.Value,
                    name: "LFCLKSTARTED")
                .WithFlag(3,
                    writeCallback: (_, value) => lfrcCalibrationEventEnabled.Value |= value,
                    valueProviderCallback: _ => lfrcCalibrationEventEnabled.Value,
                    name: "DONE")
                .WithFlag(4,
                    writeCallback: (_, value) => hfxoTuneEventEnabled.Value |= value,
                    valueProviderCallback: _ => hfxoTuneEventEnabled.Value,
                    name: "XOTUNED")
                .WithFlag(5,
                    writeCallback: (_, value) => hfxoTuneErrorEventEnabled.Value |= value,
                    valueProviderCallback: _ => hfxoTuneErrorEventEnabled.Value,
                    name: "XOTUNEERROR")
                .WithFlag(6,
                    writeCallback: (_, value) => hfxoTuneFailedEventEnabled.Value |= value,
                    valueProviderCallback: _ => hfxoTuneFailedEventEnabled.Value,
                    name: "XOTUNEFAILED")
                .WithReservedBits(7, 25)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.DisableInterrupt.Define(this)
                .WithFlag(0,
                    writeCallback: (_, value) => hfxoEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfxoEventEnabled.Value,
                    name: "XOSTARTED")
                .WithFlag(1,
                    writeCallback: (_, value) => pllEventEnabled.Value &= !value,
                    valueProviderCallback: _ => pllEventEnabled.Value,
                    name: "PLLSTARTED")
                .WithFlag(2,
                    writeCallback: (_, value) => lfclkEventEnabled.Value &= !value,
                    valueProviderCallback: _ => lfclkEventEnabled.Value,
                    name: "LFCLKSTARTED")
                .WithFlag(3,
                    writeCallback: (_, value) => lfrcCalibrationEventEnabled.Value &= !value,
                    valueProviderCallback: _ => lfrcCalibrationEventEnabled.Value,
                    name: "DONE")
                .WithFlag(4,
                    writeCallback: (_, value) => hfxoTuneEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfxoTuneEventEnabled.Value,
                    name: "XOTUNED")
                .WithFlag(5,
                    writeCallback: (_, value) => hfxoTuneErrorEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfxoTuneErrorEventEnabled.Value,
                    name: "XOTUNEERROR")
                .WithFlag(6,
                    writeCallback: (_, value) => hfxoTuneFailedEventEnabled.Value &= !value,
                    valueProviderCallback: _ => hfxoTuneFailedEventEnabled.Value,
                    name: "XOTUNEFAILED")
                .WithReservedBits(7, 25)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.PendingInterrupts.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => HfxoEvent, name: "XOSTARTED")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => PllEvent, name: "PLLSTARTED")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => LfclkEvent, name: "LFCLKSTARTED")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => LfrcCalibrationEvent, name: "DONE")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => HfxoTuneEvent, name: "XOTUNED")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => HfxoTuneErrorEvent, name: "XOTUNEERROR")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => HfxoTuneFailedEvent, name: "XOTUNEFAILED")
                .WithReservedBits(7, 25)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.HFXOStartTriggered.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => hfxoEventGenerated.Value, name: "STATUS")
                .WithReservedBits(1, 31);

            Registers.HFXOStatus.Define(this)
                .WithReservedBits(0, 16)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => hfxoStarted, name: "STATE")
                .WithReservedBits(17, 15);

            Registers.PLLStartTriggered.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => pllEventGenerated.Value, name: "STATUS")
                .WithReservedBits(1, 31);

            Registers.PLLStatus.Define(this)
                .WithReservedBits(0, 16)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => pllStarted, name: "STATE")
                .WithReservedBits(17, 15);

            Registers.LFCLKClockSource.Define(this)
                .WithValueField(0, 2, out lfclkSource, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, name: "BYPASS")
                .WithFlag(17, name: "EXTERNAL")
                .WithReservedBits(18, 14);

            Registers.LFCLKStartTriggered.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => lfclkEventGenerated.Value, name: "LFCLK.RUN")
                .WithReservedBits(1, 31);

            Registers.LFCLKStatus.Define(this)
                .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => lfclkSourceCopy.Value, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => lfclkStarted, name: "STATE")
                .WithReservedBits(17, 15);

            Registers.LFCLKClockSourceCopy.Define(this)
                .WithValueField(0, 2, out lfclkSourceCopy, name: "SRC")
                .WithReservedBits(2, 14)
                .WithFlag(16, name: "BYPASS")
                .WithFlag(17, name: "EXTERNAL")
                .WithReservedBits(18, 14);
        }

        private bool HfxoEvent => hfxoEventGenerated.Value && hfxoEventEnabled.Value;
        private bool PllEvent => pllEventGenerated.Value && pllEventEnabled.Value;
        private bool LfclkEvent => lfclkEventGenerated.Value && lfclkEventEnabled.Value;
        private bool LfrcCalibrationEvent => lfrcCalibrationEventGenerated.Value && lfrcCalibrationEventEnabled.Value;
        private bool HfxoTuneEvent => hfxoTuneEventGenerated.Value && hfxoTuneEventEnabled.Value;
        private bool HfxoTuneErrorEvent => hfxoTuneErrorEventGenerated.Value && hfxoTuneErrorEventEnabled.Value;
        private bool HfxoTuneFailedEvent => hfxoTuneFailedEventGenerated.Value && hfxoTuneFailedEventEnabled.Value;

        private bool hfxoStarted;
        private bool pllStarted;
        private bool lfclkStarted;
        private bool hfxoTuneStarted;

        private IFlagRegisterField hfxoEventGenerated;
        private IFlagRegisterField pllEventGenerated;
        private IFlagRegisterField lfclkEventGenerated;
        private IFlagRegisterField hfxoEventEnabled;
        private IFlagRegisterField pllEventEnabled;
        private IFlagRegisterField lfclkEventEnabled;
        private IFlagRegisterField lfrcCalibrationEventEnabled;
        private IFlagRegisterField hfxoTuneEventEnabled;
        private IFlagRegisterField hfxoTuneErrorEventEnabled;
        private IFlagRegisterField hfxoTuneFailedEventEnabled;
        private IFlagRegisterField lfrcCalibrationEventGenerated;
        private IFlagRegisterField hfxoTuneEventGenerated;
        private IFlagRegisterField hfxoTuneErrorEventGenerated;
        private IFlagRegisterField hfxoTuneFailedEventGenerated;
        private IValueRegisterField lfclkSource;
        private IValueRegisterField lfclkSourceCopy;

        private enum Registers
        {
            // gaps in register addressing are intentional
            StartHFXO = 0x0,
            StopHFXO = 0x4,
            StartPLL = 0x8,
            StopPLL = 0xC,
            StartLFCLK = 0x10,
            StopLFCLK = 0x14,
            StartLFRCCallibration = 0x18,
            StartHFXOTune = 0x1C,
            StopHFXOTune = 0x20,

            SubscribeForHFXOStart = 0x80,
            SubscribeForHFXOStop = 0x84,
            SubscribeForPLLStart = 0x88,
            SubscribeForPLLStop = 0x8C,
            SubscribeForLFCLKStart = 0x90,
            SubscribeForLFCLKStop = 0x94,
            SubscribeForCalibration = 0x98,

            EventsHFXOStarted = 0x100,
            EventsPLLStarted = 0x104,
            EventsLFCLKStarted = 0x108,
            CalibrationOfLFRCCompleted = 0x10C,
            EventsHFXOTuned = 0x110,
            EventsHFXOTuneError = 0x114,
            EventsHFXOTuneFailed = 0x118,

            EnableOrDisableInterrupt = 0x300,
            EnableInterrupt = 0x304,
            DisableInterrupt = 0x308,
            PendingInterrupts = 0x30C,

            HFXOStartTriggered = 0x408,
            HFXOStatus = 0x40C,
            PLLStartTriggered = 0x428,
            PLLStatus = 0x42C,
            LFCLKClockSource = 0x440,
            LFCLKStartTriggered = 0x448,
            LFCLKStatus = 0x44C,
            LFCLKClockSourceCopy = 0x450
        }
    }
}
