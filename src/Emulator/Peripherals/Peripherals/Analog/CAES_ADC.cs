//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class CAES_ADC : BasicDoubleWordPeripheral, IADC, IKnownSize
    {
        public CAES_ADC(IMachine machine, uint frequency = 50000000) : base(machine)
        {
            this.frequency = frequency;
            DefineRegisters();
            rawVoltage = Enumerable.Repeat(DefaultChannelVoltage, ADCChannelCount).ToArray();
            resdStream = new RESDStream<VoltageSample>[NumberOfDataChannels];
            samplingTimer = new LimitTimer(
                machine.ClockSource, frequency, this, "samplingClock",
                eventEnabled: true,
                direction: Direction.Ascending,
                enabled: false,
                autoUpdate: false,
                workMode: WorkMode.OneShot);
            samplingTimer.LimitReached += OnConversionFinished;
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, uint adcChannel, uint resdChannel = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.CurrentVirtualTime, long sampleOffsetTime = 0)
        {
            EnsureChannelIsValid(adcChannel);
            try
            {
                resdStream[adcChannel] = this.CreateRESDStream<VoltageSample>(filePath, resdChannel, sampleOffsetType, sampleOffsetTime);
            }
            catch(RESDException)
            {
                for(var channelId = 0; channelId < NumberOfDataChannels; channelId++)
                {
                    resdStream[channelId]?.Dispose();
                }
                throw new RecoverableException($"Could not load RESD channel {resdChannel} from {filePath}");
            }
        }

        public override void Reset()
        {
            base.Reset();
            samplingTimer.Reset();
            IRQ.Unset();
        }

        public void SetADCValue(int adcChannel, uint value)
        {
            EnsureChannelIsValid((uint)adcChannel);
            rawVoltage[adcChannel] = value / VoltageSampleDivisor;
        }

        public uint GetADCValue(int adcChannel)
        {
            EnsureChannelIsValid((uint)adcChannel);
            return rawVoltage[adcChannel] * VoltageSampleDivisor;
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x1000;

        public int ADCChannelCount => (int)NumberOfDataChannels;

        private void UpdateInterrupts()
        {
            bool value = adcInterruptEnabled.Value && conversionCompleteInterruptEnabled.Value && channelInterruptPending.Value != 0;
            this.Log(LogLevel.Debug, "Setting IRQ to {0}", value);
            IRQ.Set(value);
        }

        private uint GetChannelVoltage(uint dataChannelId)
        {
            if(resdStream[dataChannelId] == null || resdStream[dataChannelId].TryGetCurrentSample(this, (sample) => sample.Voltage / VoltageSampleDivisor, out var voltage, out _) != RESDStreamStatus.OK)
            {
                voltage = rawVoltage[dataChannelId];
            }
            else
            {
                rawVoltage[dataChannelId] = voltage;
            }

            voltage = (uint)((float)voltage * GetGain(channelGain[DataToConfigChannel(dataChannelId)].Value));

            if(voltage > MaxVoltage)
            {
                this.Log(LogLevel.Warning, "The maximum allowed input voltage is {0}mV. Provided value: {1}mV", MaxVoltage, voltage);
                return MaxVoltage;
            }

            return voltage;
        }

        private void EnsureChannelIsValid(uint channelIdx)
        {
            if(channelIdx >= NumberOfDataChannels)
            {
                throw new RecoverableException($"Invalid argument value: {channelIdx}. This peripheral implements only channels in range 0-{NumberOfDataChannels - 1}");
            }
        }

        private uint GetSingleEndedValue(uint voltage)
        {
            // The most significant bit needs to be flipped in single-ended
            // conversion due to the fact that the positive voltage range is
            // mapped to a signed integer format
            return (uint)(voltage * MaxValue / MaxVoltage) ^ 0x800;
        }

        private uint GetDifferentialValue(uint voltagePositive, uint voltageNegative)
        {
            return (uint)((voltagePositive - voltageNegative + MaxVoltage) * MaxValue / (2 * MaxVoltage));
        }

        private void StartConversion()
        {
            var enabledChannelsCount = 0;

            if(!adcEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to start the conversion, but the ADC_EN bit is not set");
                return;
            }

            this.Log(LogLevel.Debug, "Starting conversion");

            for(var i = 0; i < NumberOfConfigChannels; i++)
            {
                if(channelEnable[i].Value)
                {
                    enabledChannelsCount++;
                }
            }

            // Values below are derived from the "Calculating Channel Conversion Time" section in the ADC chapter of the UT32M0R500 Functional Manual
            samplingTimer.Frequency = GetClockFrequency(oscillatorDivider.Value);
            samplingTimer.Limit = (ulong) enabledChannelsCount * (sequenceDelay.Value * SequenceDelayDuration + oversamplingRate.Value);
            samplingTimer.Enabled = true;
        }

        private void OnConversionFinished()
        {
            this.Log(LogLevel.Debug, "Ending conversion");

            if(!adcInterruptEnabled.Value || !conversionCompleteInterruptEnabled.Value)
            {
                return;
            }

            for(var i = 0; i < NumberOfConfigChannels; i++)
            {
                if(!channelEnable[i].Value)
                {
                    continue;
                }

                var dataChannelIdx = ConfigToDataChannel((uint)i);
                this.Log(LogLevel.Debug, "Channel {0} enabled ({1}), data channel index: {2}", i, i < DifferentialChannelsOffset ? "single-ended" : "differential", dataChannelIdx);
                channelInterruptPending.SetBit((byte)i, true);

                if(i < DifferentialChannelsOffset)
                {
                    channelData[dataChannelIdx].Value = GetSingleEndedValue(GetChannelVoltage(dataChannelIdx));
                }
                else
                {
                    channelData[dataChannelIdx].Value = GetDifferentialValue(GetChannelVoltage(dataChannelIdx), GetChannelVoltage(dataChannelIdx + 1));
                }

                conversionCompleteCombined.Value = true;
            }

            UpdateInterrupts();
        }

        private float GetGain(Gain gainSetting)
        {
            float gain = 1.0F;
            if(gainAmplifierEnabled.Value)
            {
                switch(gainSetting)
                {
                    case Gain.DivideBy2:
                        gain = 0.5F;
                        break;
                    case Gain.NoGain:
                        gain = 1.0F;
                        break;
                    case Gain.MultiplyBy2:
                        gain = 2.0F;
                        break;
                    case Gain.MultiplyBy4:
                        gain = 4.0F;
                        break;
                    case Gain.MultiplyBy8:
                        gain = 8.0F;
                        break;
                    case Gain.MultiplyBy16:
                    case Gain.MultiplyBy16Alt1:
                    case Gain.MultiplyBy16Alt2:
                        gain = 16.0F;
                        break;
                    default:
                        this.Log(LogLevel.Warning, "Unsupported value of the gain setting.");
                        break;
                }
            }
            return gain;
        }

        private uint GetClockFrequency(OscillatorDivider oscillatorDividerSetting)
        {
            uint clockFrequency;
            var oscillatorDivider = 0;

            switch(oscillatorDividerSetting)
            {
                case OscillatorDivider.DivideBy2:
                    oscillatorDivider = 2;
                    break;
                case OscillatorDivider.DivideBy4:
                    oscillatorDivider = 4;
                    break;
                case OscillatorDivider.DivideBy8:
                    oscillatorDivider = 8;
                    break;
                case OscillatorDivider.DivideBy16:
                    oscillatorDivider = 16;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported value of the oscillator divider setting.");
                    break;
            }

            clockFrequency = oscillatorDivider == 0 ? 0 : frequency / (uint)oscillatorDivider;

            return clockFrequency;
        }

        private uint DataToConfigChannel(uint dataChannelIdx)
        {
            uint configChannelIdx = 0;

            // Even data channels can contain either differential or
            // single-ended conversion. If both are enabled, differential
            // conversion takes precedence.
            //
            // Temperature channel is unique in a way that it only has a
            // single configuration register which applies to both single-ended
            // and differential channels.
            if(channelEnable[DifferentialChannelsOffset + (dataChannelIdx / 2)].Value || dataChannelIdx >= TemperatureDataChannelsOffset)
            {
                configChannelIdx = (uint)(DifferentialChannelsOffset + (dataChannelIdx / 2));
            }
            else
            {
                configChannelIdx = dataChannelIdx;
            }

            return configChannelIdx;
        }

        private uint ConfigToDataChannel(uint configChannelIdx)
        {
            uint dataChannelIdx = 0;

            if(configChannelIdx < DifferentialChannelsOffset)
            {
                dataChannelIdx = configChannelIdx;
            }
            else
            {
                dataChannelIdx = (configChannelIdx - DifferentialChannelsOffset) * 2;
            }

            return dataChannelIdx;
        }

        private void DefineRegisters()
        {
            Registers.SPBConfiguration0.Define(this)
                .WithFlag(0, out adcEnabled, name: "ADC_EN")
                .WithFlag(1, out gainAmplifierEnabled, name: "EN_PGA")
                .WithTaggedFlag("EN_DSM", 2)
                .WithTaggedFlag("EN_AAF", 3)
                .WithTaggedFlag("DDF2_CLK_EN", 4)
                .WithTaggedFlag("EN_REFP", 5)
                .WithReservedBits(6,1)
                .WithTaggedFlag("EN_REFC", 7)
                .WithReservedBits(8,1)
                .WithTaggedFlag("EN_BIASGEN", 9)
                .WithTaggedFlag("EN_CLKGEN", 10)
                .WithReservedBits(11,2)
                .WithTaggedFlag("ADC_SINGLESWEEP", 13)
                .WithTag("ODB", 14, 2)
                .WithReservedBits(16,8)
                .WithFlag(24, out adcInterruptEnabled, name: "ADC_INTR_EN",
                    writeCallback: (_, val) =>
                    {
                        UpdateInterrupts();
                    })
                .WithReservedBits(25,2)
                .WithTaggedFlag("COI_OVER_IEN", 27)
                .WithTaggedFlag("SINC4_OVER_IEN", 28)
                .WithTaggedFlag("DSM_OVL_IEN", 29)
                .WithTaggedFlag("TRIG_UNDER_IEN", 30)
                .WithFlag(31, out conversionCompleteInterruptEnabled, name: "CONV_COMPL_IEN",
                    writeCallback: (_, val) =>
                    {
                        UpdateInterrupts();
                    })
            ;

            Registers.SPBConfiguration1.Define(this)
                .WithFlag(0, name: "ADC_TRIGGER",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            StartConversion();
                        }
                    })
                .WithReservedBits(1,6)
                .WithTaggedFlag("ADC_RST_CCONV", 7)
                .WithReservedBits(8,1)
                .WithTaggedFlag("ADC_READYFLAG", 9)
                .WithReservedBits(10,20)
                .WithFlag(31, name: "ADC_REGDEF",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            // Reset all registers to default state
                            RegistersCollection.Reset();
                        }
                    })
            ;

            Registers.SingleEndedChannelConfiguration0.DefineMany(this, NumberOfSingleEndedChannels,
                (register, idx) =>
                {
                    register
                        .WithFlag(0, out channelEnable[SingleEndedChannelsOffset + idx], name: "EN")
                        .WithReservedBits(1,2)
                        .WithTaggedFlag("DDF2", 3)
                        .WithEnumField(4, 3, out channelGain[SingleEndedChannelsOffset + idx], name: "GAIN")
                        .WithReservedBits(7,25)
                    ;
                }, resetValue: 0x10);

            Registers.DifferentialChannelConfiguration0.DefineMany(this, NumberOfDifferentialChannels,
                (register, idx) =>
                {
                    register
                        .WithFlag(0, out channelEnable[DifferentialChannelsOffset + idx], name: "EN")
                        .WithReservedBits(1,2)
                        .WithTaggedFlag("DDF2", 3)
                        .WithEnumField(4, 3, out channelGain[DifferentialChannelsOffset + idx], name: "GAIN")
                        .WithReservedBits(7,25)
                    ;
                }, resetValue: 0x10);

            Registers.TemperatureChannelConfiguration.Define(this, resetValue: 0x10)
                .WithFlag(0, out channelEnable[TemperatureChannelsOffset], name: "EN")
                .WithReservedBits(1,2)
                .WithTaggedFlag("DDF2", 3)
                .WithEnumField(4, 3, out channelGain[TemperatureChannelsOffset], name: "GAIN")
                .WithReservedBits(7,25)
            ;

            Registers.TimingControl.Define(this, resetValue: 0x64)
                .WithValueField(0, 8, out oversamplingRate, name: "ADC_DSMOSR")
                .WithEnumField(8, 2, out oscillatorDivider, name: "ADC_OSCDIV")
                .WithReservedBits(10,22)
            ;

            Registers.SequenceControl.Define(this)
                .WithValueField(0, 6, out sequenceDelay, name: "ADC_SEQDLY")
                .WithReservedBits(6,26)
            ;

            Registers.DSMDigitalStabilityControl.Define(this, resetValue: 0x81e)
                .WithTag("DSM_OVL_CNT", 0, 7)
                .WithReservedBits(7,1)
                .WithTag("DSM_OVL_RST", 8, 5)
                .WithReservedBits(13,3)
                .WithTaggedFlag("DSM_OVL_FLAG", 16)
                .WithReservedBits(17,15)
            ;

            Registers.InterruptStatus.Define(this)
                // SET_CHNL_INTR_PEND, DIFF_CHNL_INTR_PEND and TEMP_CHNL_INTR_PEND combined
                .WithValueField(0, 25, out channelInterruptPending, FieldMode.ReadToClear, name: "CHNL_INTR_PEND")
                .WithReservedBits(25,2)
                .WithTaggedFlag("COI_OVER_COMB", 27)
                .WithTaggedFlag("SINC4_OVER_COMB", 28)
                .WithTaggedFlag("DSM_OVL_FLAG_COMB", 29)
                .WithTaggedFlag("TRIG_UNDER_COMB", 30)
                .WithFlag(31, out conversionCompleteCombined, FieldMode.ReadToClear, name: "CONV_COMPL_COMB")
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Registers.DataOutputWord0.DefineMany(this, NumberOfDataOutputChannels, (register, idx) =>
            {
                register
                    .WithValueField(0, 12, out channelData[idx], FieldMode.Read, name: "DATA_OUT")
                    .WithReservedBits(12,2)
                    .WithTaggedFlag("DATA_ERROR", 14)
                    .WithReservedBits(15,1)
                    .WithTaggedFlag("CHNL_EN", 16)
                    .WithReservedBits(17,2)
                    .WithTaggedFlag("DD", 19)
                    .WithTag("GAIN", 20, 3)
                    .WithTaggedFlag("COI_OVER", 23)
                    .WithTaggedFlag("SINC4_OVER", 24)
                    .WithTaggedFlag("DSM_OVL_FLAG", 25)
                    .WithTaggedFlag("TRIG_UNDER", 26)
                    .WithReservedBits(27,1)
                    .WithTag("DATA_OUT_LSB", 28, 4)
                    .WithReadCallback((_, __) =>
                    {
                        channelInterruptPending.SetBit((byte)DataToConfigChannel((uint)idx), false);
                        UpdateInterrupts();
                    })
                ;
            });
        }

        private IFlagRegisterField adcEnabled;
        private IFlagRegisterField adcInterruptEnabled;
        private IFlagRegisterField conversionCompleteInterruptEnabled;
        private IFlagRegisterField conversionCompleteCombined;
        private IValueRegisterField channelInterruptPending;
        private IFlagRegisterField gainAmplifierEnabled;
        private IFlagRegisterField[] channelEnable = new IFlagRegisterField[NumberOfConfigChannels];
        private IValueRegisterField[] channelData = new IValueRegisterField[NumberOfDataChannels];
        private IEnumRegisterField<Gain>[] channelGain = new IEnumRegisterField<Gain>[NumberOfConfigChannels];
        private IEnumRegisterField<OscillatorDivider> oscillatorDivider;

        private IValueRegisterField oversamplingRate;
        private IValueRegisterField sequenceDelay;

        private uint[] rawVoltage;

        private const uint MaxValue = 0xFFF;
        private const uint MaxVoltage = 1500;
        private const uint SequenceDelayDuration = 25;
        private const uint NumberOfDataChannels = 18;
        private const uint NumberOfConfigChannels = 25;
        private const uint NumberOfSingleEndedChannels = 16;
        private const uint NumberOfDifferentialChannels = 8;
        private const uint NumberOfDataOutputChannels = 18;
        private const uint SingleEndedChannelsOffset = 0;
        private const uint DifferentialChannelsOffset = 16;
        private const uint TemperatureChannelsOffset = 24;
        private const uint TemperatureDataChannelsOffset = 16;
        private const uint DefaultChannelVoltage = 0;

        private readonly LimitTimer samplingTimer;
        private readonly RESDStream<VoltageSample>[] resdStream;
        private readonly uint frequency;

        private const uint VoltageSampleDivisor = 1000;

        private enum OscillatorDivider
        {
            DivideBy4,
            DivideBy8,
            DivideBy16,
            DivideBy2
        }

        private enum Gain
        {
            DivideBy2,
            NoGain,
            MultiplyBy2,
            MultiplyBy4,
            MultiplyBy8,
            MultiplyBy16,
            MultiplyBy16Alt1,
            MultiplyBy16Alt2,
        }

        private enum Registers : long
        {
            SPBConfiguration0 = 0x00,
            SPBConfiguration1 = 0x04,
            SingleEndedChannelConfiguration0 = 0x10, // 16 SingleEndedChannelConfiguration registers, 0x10–0x4C
            DifferentialChannelConfiguration0 = 0x50, // 8 DifferentialChannelConfiguration registers, 0x50–0x6C
            TemperatureChannelConfiguration = 0x70,
            TimingControl = 0x74,
            SequenceControl = 0x78,
            DSMDigitalStabilityControl = 0x84,
            InterruptStatus = 0x8C,
            DataOutputWord0 = 0x90 // 18 DataOutputWord registers, 0x90–0xD4
        }
    }
}
