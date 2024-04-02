//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;
using static Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIO;

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public class Timer : ResourceBlock
    {
        public static IReadOnlyList<Timer> BuildRegisters(IResourceBlockOwner owner, int count)
        {
            var statusFlags = Interrupt.BuildRegisters(owner, count, "TimerStatus", Registers.TimerStatus, Registers.TimerInterruptEnable);

            return statusFlags.Select((status, index) => BuildTimer(
                owner,
                index,
                status
            )).ToList().AsReadOnly();
        }

        public static uint EncodeShifterAsTriggerSelect(Shifter shifter)
        {
            return shifter.Identifier * 4 + 1;
        }

        public event Action ConfigurationChanged;
        public event Action ControlChanged;

        public override IEnumerable<Interrupt> Interrupts => new[] { Status };
        public override string Name => $"Timer{Identifier}";

        public Interrupt Status { get; }
        public uint TriggerSelect => (uint)triggerSelect.Value;
        public TimerTriggerPolarity TriggerPolarity => triggerPolarity.Value;
        public TimerTriggerSource TriggerSource => triggerSource.Value;
        public TimerTriggerOneTimeOperation OneTimeOperation => oneTimeOperation.Value;
        public TimerMode Mode => mode.Value;
        public TimerOutput Output => output.Value;
        public TimerDecrement Decrement => decrement.Value;
        public TimerReset ResetMode => resetMode.Value;
        public TimerDisable Disable => disable.Value;
        public TimerEnable Enable => enable.Value;
        public TimerStopBit StopBit => stopBit.Value;
        public TimerStartBit StartBit => startBit.Value;
        public uint Compare
        {
            get => (uint)compare.Value;
            set => compare.Value = value;
        }
        public uint Divider
        {
            get
            {
                switch(Decrement)
                {
                    case TimerDecrement.OnFLEXIOClockDividedBy16:
                        return 16;
                    case TimerDecrement.OnFLEXIOClockDividedBy256:
                        return 256;
                    default:
                        return 1;
                }
            }
        }

        private static Timer BuildTimer(IResourceBlockOwner owner, int index, Interrupt status)
        {
            Timer timer = null;
            var offset = index * 4;

            var controlRegister = (Registers.TimerControl0 + offset).Define(owner)
                .WithReservedBits(30, 2)
                .WithReservedBits(18, 4)
                .WithTag("PINCFG (Timer Pin Configuration)", 16, 2)
                .WithReservedBits(13, 3)
                .WithTag("PINSEL (Timer Pin Select)", 8, 5)
                .WithTaggedFlag("PINPOL (Timer Pin Polarity)", 7)
                .WithTaggedFlag("PIPINS (Timer Pin Input Select)", 6)
                .WithReservedBits(3, 2);

            var triggerSelectField = controlRegister.DefineValueField(24, 6, name: "TRGSEL (Trigger Select)");
            var triggerPolarityField = controlRegister.DefineEnumField<TimerTriggerPolarity>(23, 1, name: "TRGPOL (Trigger Polarity)");
            var triggerSourceField = controlRegister.DefineEnumField<TimerTriggerSource>(22, 1, name: "TRGSRC (Trigger Source)");
            var oneTimeOperationField = controlRegister.DefineEnumField<TimerTriggerOneTimeOperation>(5, 1, name: "ONETIM (Timer One Time Operation)");
            var modeField = controlRegister.DefineEnumField<TimerMode>(0, 3, name: "TIMOD (Timer Mode)");
            controlRegister.WithChangeCallback((_, __) => timer.ControlChanged?.Invoke());

            var configurationRegister = (Registers.TimerConfiguration0 + offset).Define(owner)
                .WithReservedBits(26, 6)
                .WithReservedBits(23, 1)
                .WithReservedBits(19, 1)
                .WithReservedBits(15, 1)
                .WithReservedBits(11, 1)
                .WithReservedBits(6, 2)
                .WithReservedBits(2, 2)
                .WithReservedBits(0, 1);

            var outputField = configurationRegister.DefineEnumField<TimerOutput>(24, 2, name: "TIMOUT (Timer Output)");
            var decrementField = configurationRegister.DefineEnumField<TimerDecrement>(20, 3, name: "TIMDEC (Timer Decrement)");
            var resetField = configurationRegister.DefineEnumField<TimerReset>(16, 3, name: "TIMRST (Timer Reset)");
            var disableField = configurationRegister.DefineEnumField<TimerDisable>(12, 3, name: "TIMDIS (Timer Disable)");
            var enableField = configurationRegister.DefineEnumField<TimerEnable>(8, 3, name: "TIMENA (Timer Enable)");
            var stopBitField = configurationRegister.DefineEnumField<TimerStopBit>(4, 2, name: "TSTOP (Timer Stop Bit)");
            var startBitField = configurationRegister.DefineEnumField<TimerStartBit>(1, 1, name: "TSTART (Timer Start Bit)");
            configurationRegister.WithChangeCallback((_, __) => timer.ConfigurationChanged?.Invoke());

            var compareField = (Registers.TimerCompare0 + offset).Define(owner)
                .WithReservedBits(16, 16)
                .DefineValueField(0, 16, name: "CMP (Timer Compare Value)");

            timer = new Timer(
                owner,
                (uint)index,
                status,
                triggerSelectField,
                triggerPolarityField,
                triggerSourceField,
                oneTimeOperationField,
                modeField,
                outputField,
                decrementField,
                resetField,
                disableField,
                enableField,
                stopBitField,
                startBitField,
                compareField
            );
            return timer;
        }

        private Timer(IResourceBlockOwner owner, uint identifier, Interrupt status,
                IValueRegisterField triggerSelect,
                IEnumRegisterField<TimerTriggerPolarity> triggerPolarity,
                IEnumRegisterField<TimerTriggerSource> triggerSource,
                IEnumRegisterField<TimerTriggerOneTimeOperation> oneTimeOperation,
                IEnumRegisterField<TimerMode> mode,
                IEnumRegisterField<TimerOutput> output,
                IEnumRegisterField<TimerDecrement> decrement,
                IEnumRegisterField<TimerReset> resetMode,
                IEnumRegisterField<TimerDisable> disable,
                IEnumRegisterField<TimerEnable> enable,
                IEnumRegisterField<TimerStopBit> stopBit,
                IEnumRegisterField<TimerStartBit> startBit,
                IValueRegisterField compare
        ) : base(owner, identifier)
        {
            Status = status;
            Status.MaskedFlagChanged += OnInterruptChange;

            this.triggerSelect = triggerSelect;
            this.triggerPolarity = triggerPolarity;
            this.triggerSource = triggerSource;
            this.oneTimeOperation = oneTimeOperation;
            this.mode = mode;
            this.output = output;
            this.decrement = decrement;
            this.resetMode = resetMode;
            this.disable = disable;
            this.enable = enable;
            this.stopBit = stopBit;
            this.startBit = startBit;
            this.compare = compare;
        }

        private readonly IValueRegisterField triggerSelect;
        private readonly IEnumRegisterField<TimerTriggerPolarity> triggerPolarity;
        private readonly IEnumRegisterField<TimerTriggerSource> triggerSource;
        private readonly IEnumRegisterField<TimerTriggerOneTimeOperation> oneTimeOperation;
        private readonly IEnumRegisterField<TimerMode> mode;
        private readonly IEnumRegisterField<TimerOutput> output;
        private readonly IEnumRegisterField<TimerDecrement> decrement;
        private readonly IEnumRegisterField<TimerReset> resetMode;
        private readonly IEnumRegisterField<TimerDisable> disable;
        private readonly IEnumRegisterField<TimerEnable> enable;
        private readonly IEnumRegisterField<TimerStopBit> stopBit;
        private readonly IEnumRegisterField<TimerStartBit> startBit;
        private readonly IValueRegisterField compare;
    }

    public enum TimerTriggerPolarity
    {
        ActiveHigh = 0,
        ActiveLow = 1
    }

    public enum TimerTriggerSource
    {
        External = 0,
        Internal = 1
    }

    public enum TimerTriggerOneTimeOperation
    {
        Normal = 0,
        BlockUntilClear = 1
    }

    public enum TimerMode
    {
        Disabled = 0b000,
        DualBaud = 0b001,
        DualPWMHigh = 0b010,
        Single = 0b011,
        SingleDisable = 0b100,
        DualWord = 0b101,
        DualPWMLow = 0b110,
        SingleInputCapture = 0b111
    }

    public enum TimerOutput
    {
        One = 0b00,
        Zero = 0b01,
        OneOnResetToo = 0b10,
        ZeroOnResetToo = 0b11
    }

    public enum TimerDecrement
    {
        OnFLEXIOClock = 0b000,
        OnTriggerInputBothEdgesClockEqualsOutput = 0b001,
        OnPinInputBothEdges = 0b010,
        OnTriggerInputBothEdgesClockEqualsTrigger = 0b011,
        OnFLEXIOClockDividedBy16 = 0b100,
        OnFLEXIOClockDividedBy256 = 0b101,
        OnPinInputRisingEdge = 0b110,
        OnTriggerInputRisingEdge = 0b111
    }

    public enum TimerReset
    {
        Never = 0b000,
        OnOutputHigh = 0b001,
        OnPinEqualToOutput = 0b010,
        OnTriggerEqualToOutput = 0b011,
        OnPinRisingEdge = 0b100,
        Reserved = 0b101,
        OnTriggerRisingEdge = 0b110,
        OnTriggerBothEdges = 0b111
    }

    public enum TimerDisable
    {
        Never = 0b000,
        OnPreviousTimerDisable = 0b001,
        OnTimerCompare = 0b010,
        OnTimerCompareAndTriggerLow = 0b011,
        OnPinBothEdges = 0b100,
        OnPinBothEdgesProvidedTriggerIsHigh = 0b101,
        OnTriggerFallingEdge = 0b110,
        Reserved = 0b111
    }

    public enum TimerEnable
    {
        Always = 0b000,
        OnPreviousTimerEnable = 0b001,
        OnTriggerHigh = 0b010,
        OnTriggerHighAndPinHigh = 0b011,
        OnPinRisingEdge = 0b100,
        OnPinRisingEdgeAndTriggerHigh = 0b101,
        OnTriggerRisingEdge = 0b110,
        OnTriggerBothEdges = 0b111
    }

    public enum TimerStopBit
    {
        Disabled = 0b00,
        OnTimerCompare = 0b01,
        OnTimerDisable = 0b10,
        OnTimerCompareAndTimerDisable = 0b11
    }

    public enum TimerStartBit
    {
        Disabled = 0b0,
        Always = 0b1
    }
}
