//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Sensors;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class STM32F0_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F0_ADC(Machine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null) : base(machine)
        {
            if(dmaPeripheral == null)
            {
                if(dmaChannel != 0)
                {
                    throw new ConstructionException($"Unspecified DMA peripheral to use with channel number {dmaChannel}");
                }
            }
            else
            {
                if(dmaChannel <= 0 || dmaChannel > dmaPeripheral.NumberOfChannels)
                {
                    throw new ConstructionException($"Invalid 'dmaChannel' argument value: '{dmaChannel}'. Available channels: 1-{dma.NumberOfChannels}");
                }
            }

            DefineRegisters();

            IRQ = new GPIO();
            this.dmaChannel = dmaChannel;
            this.dma = dmaPeripheral;
            this.referenceVoltage = referenceVoltage;
            this.externalEventFrequency = externalEventFrequency;

            samplingThread = machine.ObtainManagedThread(StartSampling, externalEventFrequency);
            channelSelected = new bool[ChannelsCount];
            sampleProvider = new SensorSamplesFifo<ScalarSample>[ChannelsCount];
            for(var channel = 0; channel < ChannelsCount; channel++)
            {
                sampleProvider[channel] = new SensorSamplesFifo<ScalarSample>();
            }
            Reset();
        }

        public void FeedVoltageSampleToChannel(int channel, string path)
        {
            ValidateChannel(channel);
            sampleProvider[channel].FeedSamplesFromFile(path);
        }

        public void FeedVoltageSampleToChannel(int channel, decimal valueInmV, uint repeat)
        {
            ValidateChannel(channel);
            var sample = new ScalarSample(valueInmV);
            for(var i = 0; i < repeat; i++)
            {
                sampleProvider[channel].FeedSample(sample);
            }
        }

        public void SetDefaultValue(decimal valueInmV, int? channel = null)
        {
            if(channel != null)
            {
                ValidateChannel(channel.Value);
                sampleProvider[channel.Value].DefaultSample.Value = valueInmV;
                return;
            }
            for(var i = 0; i < ChannelsCount; i++)
            {
                sampleProvider[i].DefaultSample.Value = valueInmV;
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < ChannelsCount; i++)
            {
                channelSelected[i] = false;
            }
            currentChannel = 0;
            awaitingConversion = false;
            enabled = false;
            externalTrigger = false;
            sequenceInProgress = false;
            samplingThread.Stop();
        }

        public long Size => 0x400;
        public GPIO IRQ { get; }

        private void ValidateChannel(int channel)
        {
            if(channel >= ChannelsCount || channel < 0)
            {
                throw new RecoverableException($"Invalid argument value: {channel}. This peripheral implements only channels in range 0-{ChannelsCount-1}");
            }
        }

        private void UpdateInterrupts()
        {
            var adcReady = adcReadyFlag.Value && adcReadyInterruptEnable.Value;
            var analogWatchdog = analogWatchdogFlag.Value && analogWatchdogInterruptEnable.Value;
            var endOfSampling = endOfSamplingFlag.Value && endOfSamplingInterruptEnable.Value;
            var endOfConversion = endOfConversionFlag.Value && endOfConversionInterruptEnable.Value;
            var endOfSequence = endOfSequenceFlag.Value && endOfSequenceInterruptEnable.Value;

            IRQ.Set(adcReady || analogWatchdog || endOfSampling || endOfConversion || endOfSequence);
        }

        private void StartSampling()
        {
            if(sequenceInProgress)
            {
                if(waitFlag.Value)
                {
                    awaitingConversion = true;
                    return;
                }
                this.Log(LogLevel.Warning, "Issued a start event before the last sequence finished");
                return;
            }
            currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? 0 : ChannelsCount - 1;
            sequenceInProgress = true;
            startFlag.Value = true;
            SampleNextChannel();
        }

        private void SendDmaRequest()
        {
            if(dma != null)
            {
                dma.RequestTransfer(dmaChannel);
            }
            else
            {
                this.Log(LogLevel.Warning, "Received DMA transfer request, but no DMA is configured for this peripheral.");
            }
        }

        private void SampleNextChannel()
        {
            // Exit when peripheral is not enabled
            if(!enabled)
            {
                currentChannel = 0;
                sequenceInProgress = false;
                return;
            }

            while(currentChannel < ChannelsCount && currentChannel >= 0)
            {
                if(!channelSelected[currentChannel])
                {
                    SwitchToNextChannel();
                    continue;
                }
                else
                {
                    data.Value = GetSampleFromChannel(currentChannel);
                    if(dmaEnabled.Value)
                    {
                        SendDmaRequest();
                    }
                    endOfSamplingFlag.Value = true;

                    var watchdogOnThisChannel = (!analogWatchdogSingleChannel.Value || analogWatchdogChannel.Value == currentChannel);
                    if(analogWatchdogEnable.Value && watchdogOnThisChannel)
                    {
                        if(data.Value > analogWatchdogHighValue.Value || data.Value < analogWatchdogLowValue.Value)
                        {
                            analogWatchdogFlag.Value = true;
                            this.Log(LogLevel.Debug, "Analog watchdog flag raised for value {0} on channel {1}", data.Value, currentChannel);
                        }
                    }
                    endOfConversionFlag.Value = true;
                    UpdateInterrupts();
                    this.Log(LogLevel.Debug, "Sampled channel {0}", currentChannel);
                    SwitchToNextChannel();
                    return;
                }
            }
            this.Log(LogLevel.Debug, "No more channels enabled");
            endOfSequenceFlag.Value = true;
            sequenceInProgress = false;
            UpdateInterrupts();
            startFlag.Value = false;

            if(awaitingConversion)
            {
                awaitingConversion = false;
                StartSampling();
            }
        }

        private void SwitchToNextChannel()
        {
            currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? currentChannel + 1 : currentChannel - 1;
        }

        private uint GetSampleFromChannel(int channelNumber)
        {
            var sample = sampleProvider[channelNumber].Sample;
            sampleProvider[channelNumber].TryDequeueNewSample();
            return MilivoltsToSample((double)sample.Value);
        }

        private uint MilivoltsToSample(double sampleInMilivolts)
        {
            ushort resolutionInBits;
            switch(resolution.Value)
            {
                case Resolution.Bits6:
                    resolutionInBits = 6;
                    break;
                case Resolution.Bits8:
                    resolutionInBits = 8;
                    break;
                case Resolution.Bits10:
                    resolutionInBits = 10;
                    break;
                case Resolution.Bits12:
                    resolutionInBits = 12;
                    break;
                default:
                    throw new Exception("This should never have happend!");
            }
                
            uint referencedValue = (uint)Math.Round((sampleInMilivolts / (referenceVoltage * 1000)) * ((1 << resolutionInBits) - 1));
            if(align.Value == Align.Left)
            {
                referencedValue = referencedValue << (16 - resolutionInBits);
            }
            return referencedValue;
        }

        private void DefineRegisters()
        {
            Register.InterruptAndStatus.Define(this)
                .WithFlag(0, out adcReadyFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "ADRDY")
                .WithFlag(1, out endOfSamplingFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "EOSMP")
                .WithFlag(2, out endOfConversionFlag, FieldMode.Read | FieldMode.WriteOneToClear,  writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            // Clearing the End Of Conversion flag triggers next conversion
                            // This function call must be delayed to avoid deadlock on registers access
                            machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                        }
                    }, name: "EOC")
                .WithFlag(3, out endOfSequenceFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "EOSEQ")
                .WithFlag(4, out adcOverrunFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVR")
                .WithReservedBits(5, 2)
                .WithFlag(7, out analogWatchdogFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "AWD")
                .WithReservedBits(8, 23);
            Register.InterruptEnable.Define(this)
                .WithFlag(0, out adcReadyInterruptEnable, name: "ADRDYIE")
                .WithFlag(1, out endOfSamplingInterruptEnable, name: "EOSMPIE")
                .WithFlag(2, out endOfConversionInterruptEnable, name: "EOCIE")
                .WithFlag(3, out endOfSequenceInterruptEnable, name: "EOSEQIE")
                .WithFlag(4, out adcOverrunInterruptEnable, name: "OVRIE")
                .WithReservedBits(5, 2)
                .WithFlag(7, out analogWatchdogInterruptEnable, name: "AWDIE")
                .WithReservedBits(8, 23)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Register.Control.Define(this)
                .WithFlag(0, valueProviderCallback: _ => enabled, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            enabled = true;
                            adcReadyFlag.Value = true;
                            UpdateInterrupts(); 
                        }
                    }, name: "ADEN")
                // Reading one from below field would mean that command is in progress. This is never the case in this model
                .WithFlag(1, valueProviderCallback: _ => false, writeCallback: (_, val) => { if(val) enabled = false; }, name: "ADDIS")
                // Reading one from this field means that conversion is in progress
                .WithFlag(2, out startFlag, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            if(externalTrigger)
                            {
                                samplingThread.Start();
                            }
                            else
                            {
                                StartSampling();
                            }
                        }
                    },name: "ADSTART")
                .WithReservedBits(3, 1)
                // Reading one from below field would mean that command is in progress. This is never the case in this model
                .WithFlag(4, valueProviderCallback: _ => false,  writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            samplingThread.Stop();
                            sequenceInProgress = false;
                        }
                    }, name: "ADSTP")
                .WithReservedBits(5, 25)
                .WithTaggedFlag("ADCAL", 31);
            Register.Configuration1.Define(this)
                .WithFlag(0, out dmaEnabled, name: "DMAEN")
                .WithTaggedFlag("DMACFG", 1)
                .WithEnumField<DoubleWordRegister, ScanDirection>(2, 1, out scanDirection, name: "SCANDIR")
                .WithEnumField<DoubleWordRegister, Resolution>(3, 2, out resolution, name: "RES")
                .WithEnumField<DoubleWordRegister, Align>(5, 1, out align, name: "ALIGN")
                .WithTag("EXTSEL", 6, 2)
                .WithReservedBits(9, 1)
                .WithValueField(10, 2, writeCallback: (_, val) => 
                    { 
                        // On hardware it is possible to configure on which edge should the trigger fire
                        // This Peripheral mocks external trigger using `externalEventFrequency`, so we only distinguish between manual/external trigger
                        externalTrigger = (val > 0); 
                    }, name: "EXTEN")
                .WithTaggedFlag("OVRMOD", 12)
                .WithTaggedFlag("CONT", 13)
                .WithFlag(14, out waitFlag, name: "WAIT")
                .WithTaggedFlag("AUTOOFF", 15)
                .WithTaggedFlag("DISCEN", 16)
                .WithReservedBits(17, 5)
                .WithFlag(22, out analogWatchdogSingleChannel, name: "AWDSGL")
                .WithFlag(23, out analogWatchdogEnable, name: "AWDEN")
                .WithReservedBits(24, 2)
                .WithValueField(26, 5, out analogWatchdogChannel, name: "AWDCH")
                .WithReservedBits(31, 1);
            Register.Configuration2.Define(this)
                .WithReservedBits(0, 30)
                .WithTag("CKMODE", 30, 2);
            Register.SamplingTime.Define(this)
                .WithTag("SMP", 0, 3)
                .WithReservedBits(3, 29);
            Register.WatchdogThreshold.Define(this)
                .WithValueField(0, 12, out analogWatchdogLowValue, name: "LT")
                .WithReservedBits(12, 4)
                .WithValueField(16, 12, out analogWatchdogHighValue, name: "HT")
                .WithReservedBits(28, 4);
            Register.ChannelSelection.Define(this)
                .WithFlags(0, ChannelsCount,
                       valueProviderCallback: (id, __) => channelSelected[id],
                       writeCallback: (id, _, val) => { this.Log(LogLevel.Debug, "Channel {0} enable set as {1}", id, val); channelSelected[id] = val; })
                .WithReservedBits(ChannelsCount, 32 - ChannelsCount);
            Register.DataRegister.Define(this)
                .WithValueField(0, 16, out data, FieldMode.Read, readCallback: (_, __) =>
                    {
                        endOfConversionFlag.Value = false;
                        // This function call must be delayed to avoid deadlock on registers access
                        if(sequenceInProgress)
                        {
                            machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                        }
                    }, name: "DATA")
                .WithReservedBits(16, 16);
            Register.CommonConfiguration.Define(this)
                .WithReservedBits(0, 22)
                .WithTaggedFlag("VREFEN", 22)
                .WithTaggedFlag("TSEN", 23)
                .WithTaggedFlag("VBATEN", 24)
                .WithReservedBits(25, 7);
        }

        private int currentChannel;
        private bool enabled;
        private bool externalTrigger;
        private bool sequenceInProgress;
        private bool awaitingConversion;
        private bool[] channelSelected;
        
        private IEnumRegisterField<Align> align;
        private IEnumRegisterField<Resolution> resolution;
        private IEnumRegisterField<ScanDirection> scanDirection;
        
        private IFlagRegisterField dmaEnabled;
        private IFlagRegisterField analogWatchdogEnable;
        private IFlagRegisterField startFlag;
        private IFlagRegisterField waitFlag;

        private IFlagRegisterField adcOverrunFlag;
        private IFlagRegisterField adcReadyFlag;
        private IFlagRegisterField analogWatchdogFlag;
        private IFlagRegisterField endOfConversionFlag;
        private IFlagRegisterField endOfSamplingFlag;
        private IFlagRegisterField endOfSequenceFlag;
        private IFlagRegisterField adcOverrunInterruptEnable;
        private IFlagRegisterField adcReadyInterruptEnable;
        private IFlagRegisterField analogWatchdogInterruptEnable;
        private IFlagRegisterField endOfConversionInterruptEnable;
        private IFlagRegisterField endOfSamplingInterruptEnable;
        private IFlagRegisterField endOfSequenceInterruptEnable;
        private IFlagRegisterField analogWatchdogSingleChannel;
        
        private IValueRegisterField data;
        private IValueRegisterField analogWatchdogChannel;
        private IValueRegisterField analogWatchdogHighValue;
        private IValueRegisterField analogWatchdogLowValue;

        private readonly IDMA dma;
        private readonly int dmaChannel;
        private readonly uint externalEventFrequency;
        private readonly double referenceVoltage;
        private readonly IManagedThread samplingThread;
        private readonly SensorSamplesFifo<ScalarSample>[] sampleProvider;
        
        private const int ChannelsCount = 19;

        private enum Resolution
        {
            Bits12 = 0b00,
            Bits10 = 0b01,
            Bits8  = 0b10,
            Bits6  = 0b11,
        }

        private enum ScanDirection
        {
            Ascending  = 0b0,
            Descending = 0b1,
        }

        private enum Align
        {
            Right = 0x0,
            Left  = 0x1,
        }

        private enum Register: long
        {
            InterruptAndStatus  = 0x00, // ADC_ISR
            InterruptEnable     = 0x04, // ADC_IER
            Control             = 0x08, // ADC_CR
            Configuration1      = 0x0C, // ADC_CFGR1
            Configuration2      = 0x10, // ADC_CFGR2
            SamplingTime        = 0x14, // ADC_SMPR
            // Gap intended
            WatchdogThreshold   = 0x20, // ADC_TR
            // Gap intended
            ChannelSelection    = 0x28, // ADC_CHSELR
            // Gap intended
            DataRegister        = 0x40, // ADC_DR
            // Gap intended
            CommonConfiguration = 0x308, // ADC_CCR
        }
    }
}
