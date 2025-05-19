//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class RenesasDA14_GPADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasDA14_GPADC(IMachine machine) : base(machine)
        {
            DefineRegisters();
            resdStream = new RESDStream<VoltageSample>[NumberOfChannels];
            samplingTimer = new LimitTimer(
                machine.ClockSource, 1000000, this, "samplingClock",
                eventEnabled: true,
                direction: Direction.Ascending,
                enabled: false,
                autoUpdate: false,
                workMode: WorkMode.OneShot);
            samplingTimer.LimitReached += OnConversionFinished;
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, uint adcChannel, uint resdChannel = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            EnsureChannelIsValid(adcChannel);
            try
            {
                resdStream[adcChannel] = this.CreateRESDStream<VoltageSample>(filePath, resdChannel, sampleOffsetType, sampleOffsetTime);
            }
            catch (RESDException)
            {
                for(var channelId = 0; channelId < NumberOfChannels; channelId++)
                {
                    if(resdStream[channelId] != null)
                    {
                        resdStream[channelId].Dispose();
                    }
                }
                throw new RecoverableException($"Could not load RESD channel {resdChannel} from {filePath}");
            }
        }

        public override void Reset()
        {
            base.Reset();
            samplingTimer.Reset();
            selectNegative = 0;
            interruptPending.Value = false;
            UpdateInterrupts();
        }

        public GPIO IRQ { get; } = new GPIO();
        public long Size => 0x24;

        private uint ParseResult(uint voltage)
        {
            return (voltage << (6 - Math.Min(6, (int)conversionNumber.Value))) & ((1 << 16) - 1);
        }

        private uint HandleConversion(uint channelId)
        {
            EnsureChannelIsValid(channelId);
            if(resdStream[channelId] == null || resdStream[channelId].TryGetCurrentSample(this, (sample) => sample.Voltage / 1000, out var voltage, out _) == RESDStreamStatus.BeforeStream)
            {
                return ParseResult(DefaultChannelVoltage);
            }
            // The attenuator value scales the allowed voltage input.
            // If the voltage is greater than the allowed level, then maxVoltage is returned.
            var maxVoltage = (uint) (0.9 * (attenuator.Value + 1) * 1000);
            if(voltage > maxVoltage)
            {
                this.Log(LogLevel.Warning, "The enabled attenuator allows input voltage up to {0}mV. Provided value: {1}mV", maxVoltage, voltage);
                return ParseResult(maxVoltage);
            }
            return ParseResult(voltage);
        }

        private void EnsureChannelIsValid(uint channelIdx)
        {
            if(channelIdx >= NumberOfChannels)
            {
                throw new RecoverableException($"Invalid argument value: {channelIdx}. This peripheral implements only channels in range 0-{NumberOfChannels - 1}");
            }
        }

        private void StartConversion()
        {
            if(!enabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to start the conversion, but the GP_ADC_EN is not enabled");
            }
            this.Log(LogLevel.Debug, "Starting conversion on channel: {0}", selectPositive.Value);

            // Documentation chapter 21.2.3.1
            // The total conversion time of an AD conversion depends on various settings.
            // Conversion time is especially important for the continuous mode
            samplingTimer.Limit = (ulong) Math.Pow(2, conversionNumber.Value) * (sampleTime.Value == 0 ? 2 : sampleTime.Value * 8) + (samplingTimer.Mode == WorkMode.Periodic ? converionInterval.Value : 0);
            samplingTimer.Enabled = true;
        }

        private void OnConversionFinished()
        {
            this.Log(LogLevel.Debug, "Ending conversion on channel: {0}", selectPositive.Value);

            // Documentation chapter 21.2.4
            // With bit GP_ADC_CTRL_REG[GP_ADC_MUTE] = 1, the input is connected to 0.5 × ADC reference.
            // So, the ideal ADC result should be 511.5. Any deviation from this is the ADC offset
            if(mute.Value)
            {
                result.Value = 0x200 << 6;
            }
            else
            {
                result.Value = HandleConversion((uint) selectPositive.Value) - (singleEnded.Value ? 0 : HandleConversion((uint) selectNegative));
            }
            interruptPending.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            bool value = interruptPending.Value && unmaskedInterrupt.Value;
            IRQ.Set(value);
        }

        private void DefineRegisters()
        {
            GeneralPurposeRegisters.Control.Define(this)
                .WithFlag(0, out enabled, name: "GP_ADC_EN", writeCallback: (_, val) => { if(!val) Reset(); })
                .WithFlag(1, valueProviderCallback: _ => samplingTimer.Enabled , name: "GP_ADC_START", changeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                StartConversion();
                            }
                            else
                            {
                                this.Log(LogLevel.Warning, "It is not allowed to write GP_ADC_START bit while it is not (yet) zero.");
                            }
                        })
                .WithFlag(2, name: "GP_ADC_CONT", writeCallback: (_, value) => { if(value) samplingTimer.Mode = WorkMode.Periodic; })
                .WithFlag(3, out dmaEnabled, name: "GP_ADC_DMA_EN")
                .WithFlag(4, out interruptPending, FieldMode.Read, name: "GP_ADC_INT")
                .WithFlag(5, out unmaskedInterrupt, name: "GP_ADC_MINT", writeCallback: (_, __) => UpdateInterrupts())
                .WithFlag(6, out singleEnded, name: "GP_ADC_SE")
                .WithFlag(7, out mute, name: "GP_ADC_MUTE")
                .WithTaggedFlag("GP_ADC_SIGN", 8)
                .WithTaggedFlag("GP_ADC_CHOP", 9)
                .WithTaggedFlag("GP_ADC_LDO_HOLD", 10)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("DIE_TEMP_EN", 12)
                .WithReservedBits(13, 19);

            GeneralPurposeRegisters.Control2.Define(this, resetValue: 0x00000200)
                .WithValueField(0, 2, out attenuator, name: "GP_ADC_ATTN")
                .WithTaggedFlag("GP_ADC_I20U", 2)
                .WithReservedBits(3, 3)
                .WithValueField(6, 3, out conversionNumber, name: "GP_ADC_CONV_NRS")
                .WithValueField(9, 4, out sampleTime, name: "GP_ADC_SMPL_TIME")
                .WithTag("GP_ADC_STORE_DEL", 13 ,3)
                .WithReservedBits(16, 16);

            GeneralPurposeRegisters.Control3.Define(this, resetValue: 0x00000040)
                .WithTag("GP_ADC_EN_DEL", 0, 8)
                .WithValueField(8, 8, out converionInterval, name: "GP_ADC_INTERVAL")
                .WithReservedBits(16, 16);

            GeneralPurposeRegisters.InputSelection.Define(this)
                .WithValueField(0, 4, name: "GP_ADC_SEL_N", writeCallback: (_, val) =>
                        {
                            if(val <= 3 || val >= 10)
                            {
                                selectNegative = val;
                            }
                            else
                            {
                                this.Log(LogLevel.Warning, "Incorrect GP_ADC_SEL_N value {0}", val);
                            }
                        })
                .WithValueField(4, 4, out selectPositive, name: "GP_ADC_SEL_P")
                .WithReservedBits(8, 24);

            GeneralPurposeRegisters.PositiveOffset.Define(this, resetValue: 0x00000200)
                .WithTag("GP_ADC_OFFP", 0, 10)
                .WithReservedBits(10, 22);

            GeneralPurposeRegisters.NegativeOffset.Define(this)
                .WithTag("GP_ADC_OFFN", 0, 10)
                .WithReservedBits(10, 22);

            GeneralPurposeRegisters.ClearInterrupt.Define(this)
                .WithValueField(0, 16, valueProviderCallback: _ => 0, writeCallback: (_, __) =>
                    {
                        interruptPending.Value = false;
                        UpdateInterrupts();
                    }, name: "GP_ADC_CLR_INT")
                .WithReservedBits(16, 16);

            GeneralPurposeRegisters.ResultRegister.Define(this)
                .WithValueField(0, 16, out result, FieldMode.Read, name: "GP_ADC_VAL")
                .WithReservedBits(16, 16);
        }

        private IFlagRegisterField enabled;
        private IFlagRegisterField dmaEnabled;
        private IFlagRegisterField interruptPending;
        private IFlagRegisterField unmaskedInterrupt;
        private IFlagRegisterField singleEnded;
        private IFlagRegisterField mute;
        private IValueRegisterField attenuator;
        private IValueRegisterField conversionNumber;
        private IValueRegisterField sampleTime;
        private IValueRegisterField converionInterval;
        private IValueRegisterField selectPositive;
        private IValueRegisterField result;

        private const int NumberOfChannels = 14;
        private const uint DefaultChannelVoltage = 100;

        private readonly LimitTimer samplingTimer;
        private readonly RESDStream<VoltageSample>[] resdStream;
        private ulong selectNegative;

        private enum GeneralPurposeRegisters : long
        {
            Control = 0x0,
            Control2 = 0x4,
            Control3 = 0x8,
            InputSelection = 0xC,
            PositiveOffset = 0x10,
            NegativeOffset = 0x14,
            ClearInterrupt = 0x1C,
            ResultRegister = 0x20,
        }
    }
}
