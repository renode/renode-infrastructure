//
// Copyright (c) 2025 Menlo Systems GmbH
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    // SAM4E Analog Front-End Controller (AFEC).
    //
    // Key difference from SAM4S ADC: channel data is accessed via a
    // channel selection register (CSELR at 0x64) — write the channel
    // number, then read CDR (0x68) or write COCR (0x6C) for that channel.
    public class SAM4E_AFEC : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM4E_AFEC(Machine machine, ulong baseFrequency = 32768, decimal referenceVoltage = 3.3m) : base(machine)
        {
            internalTimer = new LimitTimer(machine.ClockSource, baseFrequency, this, "internalTimer", limit: 1, divider: 2, eventEnabled: true, workMode: WorkMode.Periodic);
            internalTimer.LimitReached += ConversionFinished;

            ReferenceVoltage = referenceVoltage;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            selectedChannel = 0;

            foreach(var it in channelStream.Select((stream, index) => new { stream, index }))
            {
                it.stream?.Dispose();
                channelStream[it.index] = null;
            }
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, int channelIndex, uint? resdChannelOverride = null,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            AssertChannelIndex(channelIndex);

            var resdChannel = resdChannelOverride ?? (uint)channelIndex;
            channelStream[channelIndex] = this.CreateRESDStream<VoltageSample>(filePath, resdChannel, sampleOffsetType, sampleOffsetTime);
        }

        public decimal DefaultChannelVoltage(int channelIndex)
        {
            AssertChannelIndex(channelIndex);
            return defaultChannelValue[channelIndex];
        }

        public void DefaultChannelVoltage(int channelIndex, decimal newVoltage)
        {
            AssertChannelIndex(channelIndex);
            defaultChannelValue[channelIndex] = newVoltage;
        }

        public ushort GetChannelValue(int channelIndex)
        {
            AssertChannelIndex(channelIndex);

            var voltage = defaultChannelValue[channelIndex];
            if(temperatureSensorEnabled.Value && channelIndex == TemperatureChannelIndex)
            {
                voltage = BaseTemperatureVoltage + (Temperature - BaseTemperature) * TemperatureProportionalCoefficient;
            }
            else if(channelStream[channelIndex] != null)
            {
                var streamStatus = channelStream[channelIndex].TryGetCurrentSample(this, out var sample, out _);
                if(streamStatus == RESDStreamStatus.OK)
                {
                    voltage = sample.Voltage / 1000m;
                }
                else if(streamStatus == RESDStreamStatus.AfterStream)
                {
                    channelStream[channelIndex].Dispose();
                    channelStream[channelIndex] = null;
                }
            }

            voltage = voltage.Clamp(0, ReferenceVoltage);
            return (ushort)(voltage * MaximumChannelValue / ReferenceVoltage);
        }

        public long Size => 0x128;

        public GPIO IRQ { get; } = new GPIO();

        public decimal ReferenceVoltage { get; set; }

        public decimal Temperature { get; set; }

        private void UpdateInterrupts()
        {
            var interrupt = Enumerable.Range(0, NumberOfChannels)
                .Any(index => endOfConversionInterruptEnabled[index].Value && endOfConversionInterruptPending[index]);
            interrupt |= dataReadyInterruptEnabled.Value && dataReadyInterruptPending;
            interrupt |= endOfCalibrationInterruptEnabled.Value && endOfCalibrationInterruptPending;

            this.Log(LogLevel.Debug, "IRQ set to {0}", interrupt);
            IRQ.Set(interrupt);
        }

        private void StartConversion()
        {
            internalTimer.ResetValue();
            internalTimer.Enabled = true;
            this.Log(LogLevel.Debug, "Started conversion");
        }

        private void ConversionFinished()
        {
            foreach(var channelIndex in Enumerable.Range(0, NumberOfChannels).Where(index => channelStatus[index].Value))
            {
                endOfConversionInterruptPending[channelIndex] = true;
                channelValue[channelIndex] = GetChannelValue(channelIndex);
                lastDataConverted.Value = channelValue[channelIndex];
                lastDataChannel.Value = (ulong)channelIndex;
                this.Log(LogLevel.Debug, "Setting channel#{0} to {1:X03}", channelIndex, channelValue[channelIndex]);
            }

            this.Log(LogLevel.Debug, "Conversion finished");

            dataReadyInterruptPending |= endOfConversionInterruptPending.Any(p => p);
            UpdateInterrupts();

            if(!freerunMode.Value)
            {
                internalTimer.Enabled = false;
            }
        }

        private void AssertChannelIndex(int channelIndex)
        {
            if(channelIndex < 0 || channelIndex >= NumberOfChannels)
            {
                throw new RecoverableException($"'{nameof(channelIndex)}' should be between 0 and {NumberOfChannels - 1}");
            }
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.Write, name: "SWRST",
                    writeCallback: (_, value) => { if(value) Reset(); })
                .WithFlag(1, FieldMode.Write, name: "START",
                    writeCallback: (_, value) => { if(value) StartConversion(); })
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Write, name: "AUTOCAL",
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        endOfCalibrationInterruptPending = true;
                        UpdateInterrupts();
                    })
                .WithReservedBits(4, 28)
            ;

            Registers.Mode.Define(this)
                .WithTaggedFlag("TRGEN", 0)
                .WithTag("TRGSEL", 1, 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("SLEEP", 5)
                .WithTaggedFlag("FWUP", 6)
                .WithFlag(7, out freerunMode, name: "FREERUN")
                .WithValueField(8, 8, name: "PRESCAL",
                    valueProviderCallback: _ => (byte)((internalTimer.Divider / 2) - 1),
                    changeCallback: (_, value) => internalTimer.Divider = (value + 1) * 2)
                .WithTag("STARTUP", 16, 4)
                .WithReservedBits(20, 3)
                .WithTaggedFlag("ONE", 23)
                .WithTag("TRACKTIM", 24, 4)
                .WithTag("TRANSFER", 28, 2)
                .WithTaggedFlag("USEQ", 31)
            ;

            Registers.ExtendedMode.Define(this)
                .WithTag("CMPMODE", 0, 2)
                .WithReservedBits(2, 1)
                .WithTag("CMPSEL", 3, 5)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("CMPALL", 9)
                .WithReservedBits(10, 2)
                .WithTag("CMPFILTER", 12, 2)
                .WithReservedBits(14, 2)
                .WithTag("RES", 16, 3)
                .WithReservedBits(19, 5)
                .WithTaggedFlag("TAG", 24)
                .WithTaggedFlag("STM", 25)
                .WithReservedBits(26, 6)
            ;

            Registers.Sequence1.Define(this)
                .WithTag("USCH1", 0, 4)
                .WithTag("USCH2", 4, 4)
                .WithTag("USCH3", 8, 4)
                .WithTag("USCH4", 12, 4)
                .WithTag("USCH5", 16, 4)
                .WithTag("USCH6", 20, 4)
                .WithTag("USCH7", 24, 4)
                .WithTag("USCH8", 28, 4)
            ;

            Registers.Sequence2.Define(this)
                .WithTag("USCH9", 0, 4)
                .WithTag("USCH10", 4, 4)
                .WithTag("USCH11", 8, 4)
                .WithTag("USCH12", 12, 4)
                .WithTag("USCH13", 16, 4)
                .WithTag("USCH14", 20, 4)
                .WithTag("USCH15", 24, 4)
                .WithTag("USCH16", 28, 4)
            ;

            Registers.ChannelEnable.Define(this)
                .WithFlags(0, 16, FieldMode.Set, name: "AFEC_CHER",
                    writeCallback: (index, _, value) => { if(value) channelStatus[index].Value = true; })
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelDisable.Define(this)
                .WithFlags(0, 16, FieldMode.WriteOneToClear, name: "AFEC_CHDR",
                    writeCallback: (index, _, value) => { if(value) channelStatus[index].Value = false; })
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelStatus.Define(this)
                .WithFlags(0, 16, out channelStatus, FieldMode.Read, name: "AFEC_CHSR")
                .WithReservedBits(16, 16)
            ;

            Registers.LastConvertedData.Define(this)
                .WithValueField(0, 12, out lastDataConverted, name: "LDATA")
                .WithValueField(12, 4, out lastDataChannel, FieldMode.Read, name: "CHNB")
                .WithReservedBits(16, 16)
                .WithReadCallback((_, __) =>
                {
                    dataReadyInterruptPending = false;
                    UpdateInterrupts();
                })
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 16, FieldMode.Set, name: "EOC",
                    writeCallback: (index, _, value) => { if(value) endOfConversionInterruptEnabled[index].Value = true; })
                .WithReservedBits(16, 7)
                .WithFlag(23, FieldMode.Set, name: "EOCAL",
                    writeCallback: (_, value) => { if(value) endOfCalibrationInterruptEnabled.Value = true; })
                .WithFlag(24, FieldMode.Set, name: "DRDY",
                    writeCallback: (_, value) => { if(value) dataReadyInterruptEnabled.Value = true; })
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithReservedBits(27, 2)
                .WithTaggedFlag("TEMPCHG", 29)
                .WithReservedBits(30, 2)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlags(0, 16, FieldMode.Set, name: "EOC",
                    writeCallback: (index, _, value) => { if(value) endOfConversionInterruptEnabled[index].Value = false; })
                .WithReservedBits(16, 7)
                .WithFlag(23, FieldMode.Set, name: "EOCAL",
                    writeCallback: (_, value) => { if(value) endOfCalibrationInterruptEnabled.Value = false; })
                .WithFlag(24, FieldMode.Set, name: "DRDY",
                    writeCallback: (_, value) => { if(value) dataReadyInterruptEnabled.Value = false; })
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithReservedBits(27, 2)
                .WithTaggedFlag("TEMPCHG", 29)
                .WithReservedBits(30, 2)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlags(0, 16, out endOfConversionInterruptEnabled, FieldMode.Read, name: "EOC")
                .WithReservedBits(16, 7)
                .WithFlag(23, out endOfCalibrationInterruptEnabled, FieldMode.Read, name: "EOCAL")
                .WithFlag(24, out dataReadyInterruptEnabled, FieldMode.Read, name: "DRDY")
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithReservedBits(27, 2)
                .WithTaggedFlag("TEMPCHG", 29)
                .WithReservedBits(30, 2)
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 16, FieldMode.Read, name: "EOC",
                    valueProviderCallback: (index, _) =>
                    {
                        var val = endOfConversionInterruptPending[index];
                        endOfConversionInterruptPending[index] = false;
                        return val;
                    })
                .WithReservedBits(16, 7)
                .WithFlag(23, FieldMode.Read, name: "EOCAL",
                    valueProviderCallback: _ =>
                    {
                        var val = endOfCalibrationInterruptPending;
                        endOfCalibrationInterruptPending = false;
                        return val;
                    })
                .WithFlag(24, FieldMode.Read, name: "DRDY",
                    valueProviderCallback: _ =>
                    {
                        var val = dataReadyInterruptPending;
                        dataReadyInterruptPending = false;
                        return val;
                    })
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithReservedBits(27, 2)
                .WithTaggedFlag("TEMPCHG", 29)
                .WithReservedBits(30, 2)
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Registers.OverrunStatus.Define(this)
                .WithFlags(0, 16, FieldMode.Read, name: "OVRE")
                .WithReservedBits(16, 16)
            ;

            Registers.CompareWindow.Define(this)
                .WithTag("LOWTHRES", 0, 16)
                .WithTag("HIGHTHRES", 16, 16)
            ;

            Registers.ChannelGain.Define(this)
                .WithTag("GAIN0", 0, 2)
                .WithTag("GAIN1", 2, 2)
                .WithTag("GAIN2", 4, 2)
                .WithTag("GAIN3", 6, 2)
                .WithTag("GAIN4", 8, 2)
                .WithTag("GAIN5", 10, 2)
                .WithTag("GAIN6", 12, 2)
                .WithTag("GAIN7", 14, 2)
                .WithTag("GAIN8", 16, 2)
                .WithTag("GAIN9", 18, 2)
                .WithTag("GAIN10", 20, 2)
                .WithTag("GAIN11", 22, 2)
                .WithTag("GAIN12", 24, 2)
                .WithTag("GAIN13", 26, 2)
                .WithTag("GAIN14", 28, 2)
                .WithTag("GAIN15", 30, 2)
            ;

            Registers.ChannelCalibrationDCOffset.Define(this)
                .WithFlags(0, 16, name: "AOFF")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelDifferential.Define(this)
                .WithFlags(0, 16, name: "DIFF")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelSelection.Define(this)
                .WithValueField(0, 4, name: "CSEL",
                    writeCallback: (_, value) =>
                    {
                        selectedChannel = (int)value;
                        this.Log(LogLevel.Debug, "Selected channel {0}", selectedChannel);
                    },
                    valueProviderCallback: _ => (ulong)selectedChannel)
                .WithReservedBits(4, 28)
            ;

            Registers.ChannelData.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "DATA",
                    valueProviderCallback: _ =>
                    {
                        if(selectedChannel >= 0 && selectedChannel < NumberOfChannels)
                        {
                            endOfConversionInterruptPending[selectedChannel] = false;
                            UpdateInterrupts();
                            return channelValue[selectedChannel];
                        }
                        return 0;
                    })
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelOffsetCompensation.Define(this)
                .WithValueField(0, 10, name: "AOFF",
                    writeCallback: (_, value) =>
                    {
                        if(selectedChannel >= 0 && selectedChannel < NumberOfChannels)
                        {
                            channelOffset[selectedChannel] = (ushort)value;
                        }
                    },
                    valueProviderCallback: _ =>
                    {
                        if(selectedChannel >= 0 && selectedChannel < NumberOfChannels)
                        {
                            return channelOffset[selectedChannel];
                        }
                        return 0;
                    })
                .WithReservedBits(10, 22)
            ;

            Registers.TemperatureSensorMode.Define(this)
                .WithTaggedFlag("RTCT", 0)
                .WithReservedBits(1, 3)
                .WithTag("TEMPCMPMOD", 4, 2)
                .WithReservedBits(6, 26)
            ;

            Registers.TemperatureCompareWindow.Define(this)
                .WithTag("TLOWTHRES", 0, 16)
                .WithTag("THIGHTHRES", 16, 16)
            ;

            Registers.AnalogControl.Define(this)
                .WithReservedBits(0, 4)
                .WithFlag(4, out temperatureSensorEnabled, name: "TSON")
                .WithReservedBits(5, 3)
                .WithTag("IBCTL", 8, 2)
                .WithReservedBits(10, 22)
            ;

            Registers.WriteProtectMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;

            Registers.WriteProtectStatus.Define(this)
                .WithTaggedFlag("WPVS", 0)
                .WithTag("WPVSRC", 8, 16)
                .WithReservedBits(24, 8)
            ;
        }

        private IFlagRegisterField freerunMode;

        private IFlagRegisterField[] channelStatus;

        private IFlagRegisterField[] endOfConversionInterruptEnabled;
        private bool[] endOfConversionInterruptPending = new bool[NumberOfChannels];

        private IFlagRegisterField endOfCalibrationInterruptEnabled;
        private bool endOfCalibrationInterruptPending;

        private IFlagRegisterField dataReadyInterruptEnabled;
        private bool dataReadyInterruptPending;

        private IValueRegisterField lastDataConverted;
        private IValueRegisterField lastDataChannel;
        private IFlagRegisterField temperatureSensorEnabled;

        private int selectedChannel;
        private readonly ushort[] channelValue = new ushort[NumberOfChannels];
        private readonly ushort[] channelOffset = new ushort[NumberOfChannels];
        private readonly RESDStream<VoltageSample>[] channelStream = new RESDStream<VoltageSample>[NumberOfChannels];

        private readonly decimal[] defaultChannelValue = new decimal[NumberOfChannels];
        private readonly LimitTimer internalTimer;

        private const int NumberOfChannels = 16;
        private const ushort MaximumChannelValue = 0xFFF;

        private const decimal BaseTemperature = 27m;
        private const decimal BaseTemperatureVoltage = 1.44m;
        private const decimal TemperatureProportionalCoefficient = 4.7m / 1000m;
        private const int TemperatureChannelIndex = 15;

        private enum Registers : uint
        {
            Control = 0x00,
            Mode = 0x04,
            ExtendedMode = 0x08,
            Sequence1 = 0x0C,
            Sequence2 = 0x10,
            ChannelEnable = 0x14,
            ChannelDisable = 0x18,
            ChannelStatus = 0x1C,
            LastConvertedData = 0x20,
            InterruptEnable = 0x24,
            InterruptDisable = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            OverrunStatus = 0x4C,
            CompareWindow = 0x50,
            ChannelGain = 0x54,
            ChannelCalibrationDCOffset = 0x5C,
            ChannelDifferential = 0x60,
            ChannelSelection = 0x64,
            ChannelData = 0x68,
            ChannelOffsetCompensation = 0x6C,
            TemperatureSensorMode = 0x70,
            TemperatureCompareWindow = 0x74,
            AnalogControl = 0x94,
            WriteProtectMode = 0xE4,
            WriteProtectStatus = 0xE8,
        }
    }
}
