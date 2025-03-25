//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class SAM4S_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM4S_ADC(Machine machine, long baseFrequency = 32768, decimal referenceVoltage = 5m) : base(machine)
        {
            internalTimer = new LimitTimer(machine.ClockSource, baseFrequency, this, "internalTimer", limit: 1, divider: 2, eventEnabled: true, workMode: WorkMode.OneShot);
            internalTimer.LimitReached += ConversionFinished;

            ReferenceVoltage = referenceVoltage;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            foreach(var it in channelStream.Select((Stream, Index) => new { Stream, Index }))
            {
                it.Stream?.Dispose();
                channelStream[it.Index] = null;
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

        public long Size => 0x100;

        public GPIO IRQ { get; } = new GPIO();

        public decimal ReferenceVoltage { get; set; }

        public decimal Temperature { get; set; }

        private void UpdateInterrupts()
        {
            var interrupt = Enumerable.Range(0, NumberOfChannels)
                .Any(index => endOfConversionInterruptEnabled[index].Value && endOfConversionInterruptPending[index].Value);
            interrupt |= dataReadyInterruptEnabled.Value && dataReadyInterruptPending.Value;
            interrupt |= endOfCalibrationInterruptEnabled.Value && endOfCalibrationInterruptPending.Value;

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
                endOfConversionInterruptPending[channelIndex].Value = true;
                channelValue[channelIndex].Value = (ulong)GetChannelValue(channelIndex);
                lastDataConverted.Value = channelValue[channelIndex].Value;
                this.Log(LogLevel.Debug, "Setting channel#{0} to {1:X03}", channelIndex, channelValue[channelIndex].Value);
            }

            this.Log(LogLevel.Debug, "Conversion finished");

            dataReadyInterruptPending.Value |= endOfConversionInterruptPending.Any(interrupt => interrupt.Value);
            UpdateInterrupts();

            if(freerunMode.Value)
            {
                StartConversion();
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

                        endOfCalibrationInterruptPending.Value = true;
                        UpdateInterrupts();
                    })
                .WithReservedBits(4, 28)
            ;

            Registers.Mode.Define(this)
                .WithTaggedFlag("TRGEN", 0)
                .WithTag("TRGSEL", 1, 3)
                .WithTaggedFlag("SLEEP", 5)
                .WithTaggedFlag("FWUP", 6)
                .WithFlag(7, out freerunMode, name: "FREERUN")
                .WithValueField(8, 8, name: "PRESCAL",
                    valueProviderCallback: _ => (byte)((internalTimer.Divider / 2) - 1),
                    changeCallback: (_, value) => internalTimer.Divider = ((int)value + 1) * 2)
                .WithTag("STARTUP", 16, 4)
                .WithTag("SETTLING", 20, 2)
                .WithTaggedFlag("ANACH", 23)
                .WithTag("TRACKTIM", 24, 4)
                .WithTag("TRANSFER", 28, 2)
                .WithTaggedFlag("USEQ", 31)
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
                .WithReservedBits(28, 4)
            ;

            Registers.ChannelEnable.Define(this)
                .WithFlags(0, 16, FieldMode.Set, name: "ADC_CHER",
                    writeCallback: (index, _, value) => { if(value) channelStatus[index].Value = true;  })
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelDisable.Define(this)
                .WithFlags(0, 16, FieldMode.WriteOneToClear, name: "ADC_CHDR",
                    writeCallback: (index, _, value) => { if(value) channelStatus[index].Value = false;  })
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelStatus.Define(this)
                .WithFlags(0, 16, out channelStatus, FieldMode.Read, name: "ADC_CHSR")
                .WithReservedBits(16, 16)
            ;

            Registers.LastConvertedData.Define(this)
                .WithValueField(0, 12, out lastDataConverted, name: "LDATA")
                .WithTag("CHNB", 12, 4)
                .WithReservedBits(16, 16)
                .WithReadCallback((_, __) =>
                {
                    dataReadyInterruptPending.Value = false;
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
                .WithTaggedFlag("ENDRX", 27)
                .WithTaggedFlag("RXBFUF", 28)
                .WithReservedBits(29, 3)
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
                .WithTaggedFlag("ENDRX", 27)
                .WithTaggedFlag("RXBFUF", 28)
                .WithReservedBits(29, 3)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlags(0, 16, out endOfConversionInterruptEnabled, FieldMode.Read, name: "EOC")
                .WithReservedBits(16, 7)
                .WithFlag(23, out endOfCalibrationInterruptEnabled, FieldMode.Read, name: "EOCAL")
                .WithFlag(24, out dataReadyInterruptEnabled, FieldMode.Read, name: "DRDY")
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithTaggedFlag("ENDRX", 27)
                .WithTaggedFlag("RXBFUF", 28)
                .WithReservedBits(29, 3)
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 16, out endOfConversionInterruptPending, FieldMode.Read, name: "EOC")
                .WithReservedBits(16, 7)
                // NOTE: We are not doing the actual calibration, so we are just clearing the
                //       flag on the first access.
                .WithFlag(23, out endOfCalibrationInterruptPending, FieldMode.ReadToClear, name: "EOCAL")
                .WithFlag(24, out dataReadyInterruptPending, FieldMode.Read, name: "DRDY")
                .WithTaggedFlag("GOVRE", 25)
                .WithTaggedFlag("COMPE", 26)
                .WithTaggedFlag("ENDRX", 27)
                .WithTaggedFlag("RXBFUF", 28)
                .WithReservedBits(29, 3)
            ;

            Registers.OverrunStatus.Define(this)
                .WithTaggedFlag("OVRE0", 0)
                .WithTaggedFlag("OVRE1", 1)
                .WithTaggedFlag("OVRE2", 2)
                .WithTaggedFlag("OVRE3", 3)
                .WithTaggedFlag("OVRE4", 4)
                .WithTaggedFlag("OVRE5", 5)
                .WithTaggedFlag("OVRE6", 6)
                .WithTaggedFlag("OVRE7", 7)
                .WithTaggedFlag("OVRE8", 8)
                .WithTaggedFlag("OVRE9", 9)
                .WithTaggedFlag("OVRE10", 10)
                .WithTaggedFlag("OVRE11", 11)
                .WithTaggedFlag("OVRE12", 12)
                .WithTaggedFlag("OVRE13", 13)
                .WithTaggedFlag("OVRE14", 14)
                .WithTaggedFlag("OVRE15", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.ExtendedMode.Define(this)
                .WithTag("CMPMODE", 0, 2)
                .WithReservedBits(2, 2)
                .WithTag("CMPSEL", 4, 4)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("CMPALL", 9)
                .WithReservedBits(10, 14)
                .WithTaggedFlag("TAG", 24)
                .WithReservedBits(25, 7)
            ;

            Registers.CompareWindow.Define(this)
                .WithTag("LOWTHRES", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("HIGHTHRES", 16, 12)
                .WithReservedBits(28, 4)
            ;

            Registers.ChannelGain.Define(this)
                .WithTaggedFlag("GAIN0", 0)
                .WithTaggedFlag("GAIN1", 1)
                .WithTaggedFlag("GAIN2", 2)
                .WithTaggedFlag("GAIN3", 3)
                .WithTaggedFlag("GAIN4", 4)
                .WithTaggedFlag("GAIN5", 5)
                .WithTaggedFlag("GAIN6", 6)
                .WithTaggedFlag("GAIN7", 7)
                .WithTaggedFlag("GAIN8", 8)
                .WithTaggedFlag("GAIN9", 9)
                .WithTaggedFlag("GAIN10", 10)
                .WithTaggedFlag("GAIN11", 11)
                .WithTaggedFlag("GAIN12", 12)
                .WithTaggedFlag("GAIN13", 13)
                .WithTaggedFlag("GAIN14", 14)
                .WithTaggedFlag("GAIN15", 15)
            ;

            Registers.ChannelOffset.Define(this)
                .WithTaggedFlag("OFF0", 0)
                .WithTaggedFlag("OFF1", 1)
                .WithTaggedFlag("OFF2", 2)
                .WithTaggedFlag("OFF3", 3)
                .WithTaggedFlag("OFF4", 4)
                .WithTaggedFlag("OFF5", 5)
                .WithTaggedFlag("OFF6", 6)
                .WithTaggedFlag("OFF7", 7)
                .WithTaggedFlag("OFF8", 8)
                .WithTaggedFlag("OFF9", 9)
                .WithTaggedFlag("OFF10", 10)
                .WithTaggedFlag("OFF11", 11)
                .WithTaggedFlag("OFF12", 12)
                .WithTaggedFlag("OFF13", 13)
                .WithTaggedFlag("OFF14", 14)
                .WithTaggedFlag("OFF15", 15)
                .WithTaggedFlag("DIFF0", 16)
                .WithTaggedFlag("DIFF1", 17)
                .WithTaggedFlag("DIFF2", 18)
                .WithTaggedFlag("DIFF3", 19)
                .WithTaggedFlag("DIFF4", 20)
                .WithTaggedFlag("DIFF5", 21)
                .WithTaggedFlag("DIFF6", 22)
                .WithTaggedFlag("DIFF7", 23)
                .WithTaggedFlag("DIFF8", 24)
                .WithTaggedFlag("DIFF9", 25)
                .WithTaggedFlag("DIFF10", 26)
                .WithTaggedFlag("DIFF11", 27)
                .WithTaggedFlag("DIFF12", 28)
                .WithTaggedFlag("DIFF13", 29)
                .WithTaggedFlag("DIFF14", 30)
                .WithTaggedFlag("DIFF15", 31)
            ;

            Registers.ChannelData0.DefineMany(this, NumberOfChannels, (register, index) =>
                register
                    .WithValueField(0, 12, out channelValue[index], name: $"ADC_CDR{index}")
                    .WithReservedBits(12, 20)
                    .WithReadCallback((_, __) =>
                    {
                        endOfConversionInterruptPending[index].Value = false;
                        UpdateInterrupts();
                    }))
            ;

            Registers.AnalogControl.Define(this)
                .WithReservedBits(0, 4)
                .WithFlag(4, out temperatureSensorEnabled, name: "TSON")
                .WithReservedBits(5, 3)
                .WithTag("IBCTL", 8, 24)
            ;

            Registers.WriteProtectMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;
        }

        private IFlagRegisterField freerunMode;

        private IFlagRegisterField[] channelStatus;

        private IFlagRegisterField[] endOfConversionInterruptEnabled;
        private IFlagRegisterField[] endOfConversionInterruptPending;

        private IFlagRegisterField endOfCalibrationInterruptEnabled;
        private IFlagRegisterField endOfCalibrationInterruptPending;

        private IFlagRegisterField dataReadyInterruptEnabled;
        private IFlagRegisterField dataReadyInterruptPending;

        private IValueRegisterField lastDataConverted;
        private IFlagRegisterField temperatureSensorEnabled;

        private readonly IValueRegisterField[] channelValue = new IValueRegisterField[NumberOfChannels];
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
            Sequence1 = 0x08,
            Sequence2 = 0x0C,
            ChannelEnable = 0x10,
            ChannelDisable = 0x14,
            ChannelStatus = 0x18,
            LastConvertedData = 0x20,
            InterruptEnable = 0x24,
            InterruptDisable = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            OverrunStatus = 0x3C,
            ExtendedMode = 0x40,
            CompareWindow = 0x44,
            ChannelGain = 0x48,
            ChannelOffset = 0x4C,
            // NOTE: This is offset of the first register;
            //       there are `NumberOfChannels` registers, one for each channel
            ChannelData0 = 0x50,
            AnalogControl = 0x94,
            WriteProtectMode = 0xE4,
            WriteProtectStatus = 0xE8,
            // NOTE: Those registers are used by PDC block which isn't currently supported
            //       by this peripheral
            ReceivePointer = 0x100,
            ReceiveCounter = 0x104,
            ReceiveNextPointer = 0x110,
            ReceiveNextCounter = 0x114,
            TransferControl = 0x120,
            TransferStatus = 0x124
        }
    }
}
