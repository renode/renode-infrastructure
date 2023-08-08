//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K_LPIT : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public S32K_LPIT(IMachine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();
            TimerOutput0 = new GPIO();
            TimerOutput1 = new GPIO();
            TimerOutput2 = new GPIO();
            TimerOutput3 = new GPIO();

            for(var channel = 0; channel < ChannelCount; channel++)
            {
                // The frequency typically depends on PCC_LPIT register.
                timers[channel] = new LPITTimer(machine.ClockSource, frequency, this, $"ch{channel}");
            }
            // Timers have to be initialized before calling 'DefineRegisters'.
            DefineRegisters();
        }

        public string PrintTimerInformation(int channel)
        {
            return $"{timers[channel]}";
        }

        public void OnGPIO(int number, bool value)
        {
            // For external triggers.
            // If and which external trigger each LPIT timer uses depends on its TRG_SRC and TRG_SEL fields.
            // These should be connected via TRGMUX_LPIT0 register.
            this.Log(LogLevel.Error, "External triggers aren't currently supported!");
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(!IsRegisterAccessValid(offset))
            {
                this.Log(LogLevel.Error, "Reading from the {0} register with module disabled is forbidden!", (Registers)offset);
                // "generate a transfer error"
                return 0;
            }
            return base.ReadDoubleWord(offset);
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                timers[channel].Reset();
                GetTimerOutput(channel).Unset();
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(!IsRegisterAccessValid(offset))
            {
                this.Log(LogLevel.Error, "Writing to the {0} register with module disabled is forbidden! Value: {1:X}", (Registers)offset, value);
                // "generate a transfer error"
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public GPIO TimerOutput0 { get; }
        public GPIO TimerOutput1 { get; }
        public GPIO TimerOutput2 { get; }
        public GPIO TimerOutput3 { get; }

        private void DefineChannelRegisters(int channel, Registers valueRegister, Registers currentValueRegister, Registers controlRegister)
        {
            valueRegister.Define(this)
                .WithValueField(0, 32, name: "TMR_VAL - Timer Value",
                    changeCallback: (x, value) => timers[channel].ValueSet = (uint)value,
                    valueProviderCallback: (value) => timers[channel].ValueSet);

            currentValueRegister.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TMR_CUR_VAL - Current Timer Value",
                    valueProviderCallback: (value) => timers[channel].Enabled ? (uint)timers[channel].Value : uint.MaxValue);

            controlRegister.Define(this)
                .WithFlag(0, name: "T_EN - Timer Enable",
                    changeCallback: (x, value) => timers[channel].SetTimerEnabled(value),
                    valueProviderCallback: (value) => timers[channel].Enabled)
                .WithTaggedFlag("CHAIN - Chain Channel", 1)
                .WithEnumField<DoubleWordRegister, OperationModes>(2, 2, name: "MODE - Timer Operation Mode",
                    changeCallback: (x, value) => timers[channel].OperationMode = value,
                    valueProviderCallback: (value) => timers[channel].OperationMode)
                .WithReservedBits(4, 12)
                .WithTaggedFlag("TSOT - Timer Start On Trigger", 16)
                .WithFlag(17, name: "TSOI - Timer Stop On Interrupt",
                    changeCallback: (x, value) => timers[channel].Mode = value ? WorkMode.OneShot : WorkMode.Periodic,
                    valueProviderCallback: (value) => timers[channel].Mode == WorkMode.OneShot)
                .WithTaggedFlag("TROT - Timer Reload On Trigger", 18)
                .WithReservedBits(19, 4)
                .WithEnumField<DoubleWordRegister, TriggerModes>(23, 1, name: "TRG_SRC - Trigger Source",
                    changeCallback: (x, value) => timers[channel].TriggerMode = value,
                    valueProviderCallback: (value) => timers[channel].TriggerMode)
                .WithTag("TRG_SEL - Trigger Select", 24, 4)
                .WithReservedBits(28, 4);
        }

        private void DefineRegisters()
        {
            Registers.VersionID.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "FEATURE - Feature Number", valueProviderCallback: (x) => 0u)
                .WithValueField(16, 8, FieldMode.Read, name: "MINOR - Minor Version Number", valueProviderCallback: (x) => 0u)
                .WithValueField(24, 8, FieldMode.Read, name: "MAJOR - Major Version Number", valueProviderCallback: (x) => 1u);

            Registers.Parameter.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "CHANNEL - Number of Timer Channels", valueProviderCallback: (x) => ChannelCount)
                .WithValueField(8, 8, FieldMode.Read, name: "EXT_TRIG - Number of External Trigger Inputs", valueProviderCallback: (x) => ChannelCount)
                .WithReservedBits(16, 16);

            Registers.ModuleControl.Define(this)
                .WithFlag(0, out moduleEnabled, name: "M_CEN - Module Clock Enable", changeCallback: (x, value) => UpdateInterrupts())
                // TODO: Software Reset shouldn't reset the Module Control register.
                .WithFlag(1, name: "SW_RST - Software Reset", changeCallback: (x, value) => { if(value) Reset(); })
                .WithTaggedFlag("DOZE_EN - DOZE Mode Enable", 2)
                .WithTaggedFlag("DBG_EN - Debug Enable", 3)
                .WithReservedBits(4, 28);

            Registers.ModuleStatus.Define(this)
                .WithFlags(0, 4, name: "TIFn - Channel n Timer Interrupt Flag",
                    writeCallback: (channel, x, value) => { if(value) timers[channel].ClearInterrupt(); UpdateInterrupts(); },
                    valueProviderCallback: (channel, value) => timers[channel].RawInterrupt)
                .WithReservedBits(4, 28);

            Registers.ModuleInterruptEnable.Define(this)
                .WithFlags(0, 4, name: "TIEn - Channel n Timer Interrupt Enable",
                    changeCallback: (channel, x, value) => { timers[channel].EventEnabled = value; UpdateInterrupts(); },
                    valueProviderCallback: (channel, value) => timers[channel].EventEnabled)
                .WithReservedBits(4, 28);

            Registers.SetTimerEnable.Define(this)
                .WithFlags(0, 4, name: "SET_T_EN_n - Set Timer n Enable",
                    // Writing 0 has no effect.
                    writeCallback: (channel, x, value) => { if(value) timers[channel].SetTimerEnabled(true); },
                    valueProviderCallback: (channel, x) => timers[channel].Enabled)
                .WithReservedBits(4, 28);

            Registers.ClearTimerEnable.Define(this)
                // TODO: Does disabling a timer clears its interrupt flag?
                .WithFlags(0, 4, FieldMode.Write, name: "CLR_T_EN_n - Clear Timer n Enable",
                    // Writing 0 has no effect.
                    writeCallback: (channel, x, value) => { if(value) timers[channel].SetTimerEnabled(false); })
                .WithReservedBits(4, 28);

            DefineChannelRegisters(0, Registers.TimerValue0, Registers.CurrentTimerValue0, Registers.TimerControl0);
            DefineChannelRegisters(1, Registers.TimerValue1, Registers.CurrentTimerValue1, Registers.TimerControl1);
            DefineChannelRegisters(2, Registers.TimerValue2, Registers.CurrentTimerValue2, Registers.TimerControl2);
            DefineChannelRegisters(3, Registers.TimerValue3, Registers.CurrentTimerValue3, Registers.TimerControl3);
        }

        private GPIO GetTimerOutput(int channel)
        {
            switch(channel)
            {
                case 0:
                    return TimerOutput0;
                case 1:
                    return TimerOutput1;
                case 2:
                    return TimerOutput2;
                case 3:
                    return TimerOutput3;
            }
            throw new System.ArgumentException($"No such channel: {channel}! LPIT has only {ChannelCount} channels!");
        }

        private bool IsRegisterAccessValid(long register)
        {
            switch((Registers)register)
            {
                case Registers.ModuleStatus:
                case Registers.SetTimerEnable:
                case Registers.ClearTimerEnable:
                case Registers.TimerValue0:
                case Registers.TimerValue1:
                case Registers.TimerValue2:
                case Registers.TimerValue3:
                case Registers.TimerControl0:
                case Registers.TimerControl1:
                case Registers.TimerControl2:
                case Registers.TimerControl3:
                    return moduleEnabled.Value;
            }
            return true;
        }

        private void UpdateInterrupts()
        {
            // None of these should be set if Module Enabled field is cleared.
            IRQ.Set(moduleEnabled.Value && timers.Any((timer) => timer.Interrupt));
            for(int channel = 0; channel < ChannelCount; channel++)
            {
                // For output it doesn't matter whether interrupts are enabled (hence RawInterrupt).
                GetTimerOutput(channel).Set(moduleEnabled.Value && timers[channel].RawInterrupt);
            }
        }

        private IFlagRegisterField moduleEnabled;
        private readonly LPITTimer[] timers = new LPITTimer[ChannelCount];

        private const uint ChannelCount = 4;

        private class LPITTimer : LimitTimer
        {
            public LPITTimer(IClockSource clockSource, long frequency, S32K_LPIT owner, string name) : base(clockSource, frequency, owner, name,
                    limit: uint.MaxValue, direction: Direction.Descending, enabled: false, workMode: WorkMode.Periodic, eventEnabled: false, autoUpdate: true)
            {
                lpit = owner;
                this.name = name;
                LimitReached += LimitReachedHandle;

                Reset();
            }

            public override string ToString()
            {
                return $"{name}: L {Limit} (VS={valueSet}) /V {Value} /E {Enabled} /EE {EventEnabled} /M {OperationMode} /RI {RawInterrupt} /I {Interrupt}";
            }

            public override void Reset()
            {
                base.Reset();
                operationMode = 0;
                TriggerMode = TriggerModes.External;
                valueSet = uint.MaxValue;  // Has to match the initial limit value.
            }

            public void SetTimerEnabled(bool value)
            {
                DebugLog("Setting Timer Enabled: {0}", value);
                if(value && TriggerMode == TriggerModes.External)
                {
                    Log(LogLevel.Error, "External trigger sources aren't currently supported, treating as internal!");
                }

                Enabled = value;
                if(!value)
                {
                    if(Limit == valueSet)
                    {
                        ResetValue();
                    }
                    else
                    {
                        Limit = valueSet;
                    }
                }
            }

            public OperationModes OperationMode
            {
                get => operationMode;
                set
                {
                    DebugLog("Setting Operation Mode: {0}", value);
                    switch(value)
                    {
                        case OperationModes.DualPeriodicCounter:
                        case OperationModes.TriggerAccumulator:
                        case OperationModes.TriggerInputCapture:
                            Log(LogLevel.Error, "{0}: The {1} mode isn't currently supported, still operating as {2}!", name, value);
                            return;
                    }
                    operationMode = value;
                }
            }

            public uint ValueSet
            {
                // In capture mode "the inverse of the counter value" should be returned.
                get => valueSet;
                set
                {
                    DebugLog("Setting Value: {0:X}", value);
                    if(IsInCompareMode())
                    {
                        if(value < 2)
                        {
                            Log(LogLevel.Error, "Invalid load value in compare mode: {1}", value);
                            return;
                        }

                        valueSet = value;
                        if(!Enabled)
                        {
                            DebugLog("Timer disabled, setting value.");
                            Limit = value;
                        }
                        else
                        {
                            DebugLog("Timer enabled, value will be set after the timer is disabled or 0 is reached.");
                        }
                    }
                }
            }

            public TriggerModes TriggerMode { get; set; }

            private void LimitReachedHandle()
            {
                this.DebugLog("Limit reached!");
                // TODO: Reaching Limit should generate Pre-Trigger and Trigger outputs.
                if(IsInCompareMode())
                {
                    lpit.UpdateInterrupts();
                    if(Limit != valueSet)
                    {
                        Limit = valueSet;
                    }
                }
            }

            private bool IsInCompareMode()
            {
                return operationMode == OperationModes.PeriodicCounter || operationMode == OperationModes.DualPeriodicCounter;
            }

            // Convenience functions to prepend timer name
            private void DebugLog(string message, params object[] args)
            {
                Log(LogLevel.Debug, message, args);
            }

            private void Log(LogLevel level, string message, params object[] args)
            {
                lpit.Log(level, $"{name}: {message}", args);
            }

            private readonly S32K_LPIT lpit;
            private readonly string name;
            private OperationModes operationMode;
            private uint valueSet;
        }

        private enum OperationModes
        {
            PeriodicCounter,
            DualPeriodicCounter,
            TriggerAccumulator,
            TriggerInputCapture,
        }

        private enum Registers
        {
            VersionID = 0x0,
            Parameter = 0x4,
            ModuleControl = 0x8,
            ModuleStatus = 0xC,
            ModuleInterruptEnable = 0x10,
            SetTimerEnable = 0x14,
            ClearTimerEnable = 0x18,
            TimerValue0 = 0x20,
            CurrentTimerValue0 = 0x24,
            TimerControl0 = 0x28,
            TimerValue1 = 0x30,
            CurrentTimerValue1 = 0x34,
            TimerControl1 = 0x38,
            TimerValue2 = 0x40,
            CurrentTimerValue2 = 0x44,
            TimerControl2 = 0x48,
            TimerValue3 = 0x50,
            CurrentTimerValue3 = 0x54,
            TimerControl3 = 0x58,
        }

        private enum TriggerModes
        {
            External,
            Internal,
        }
    }
}
