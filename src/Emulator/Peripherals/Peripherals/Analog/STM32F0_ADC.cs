//
// Copyright (c) 2010-2021 Antmicro
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
    public class STM32F0_ADC: BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F0_ADC(Machine machine, double referenceVoltage, int externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null) : base(machine)
        {
            DefineRegisters();

            IRQ = new GPIO();
            this.dmaChannel = dmaChannel;
            this.Dma = dmaPeripheral;
            this.referenceVoltage = referenceVoltage;
            this.externalEventFrequency = externalEventFrequency;

            if(Dma == null)
            {
                if(dmaChannel != 0)
                {
                    throw new ConstructionException($"Unspecified DMA peripheral to use with channel number {dmaChannel}");
                }
            }
            else
            {
                if(dmaChannel <= 0 || dmaChannel > Dma.numberOfChannels)
                {
                    throw new ConstructionException($"Invalid 'dmaChannel' argument value: '{dmaChannel}'. Available channels: 1-{Dma.numberOfChannels}");
                }
            }

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
            sampleProvider[channel].FeedSamplesFromFile(path);
        }

        public void FeedVoltageSampleToChannel(int channel, decimal value, uint repeat)
        {
            var sample = new ScalarSample(value);
            for(var i = 0; i < repeat; i++)
            {
                sampleProvider[channel].FeedSample(sample);
            }
        }

        public void SetDefaultValue(decimal value, int? channel = null)
        {
            if(channel != null)
            {
                sampleProvider[(int)channel].DefaultSample.Value = value;
                return;
            }
            for(var i = 0; i < ChannelsCount; i++)
            {
                sampleProvider[i].DefaultSample.Value = value;
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < ChannelsCount; i++)
            {
                channelSelected[i] = false;
            }
            resolutionInBits = 0;
            currentChannel = 0;
            externalTrigger = false;
            samplingThread.Stop();
        }

        public long Size => 0x400;
        public GPIO IRQ { get; private set; }

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
            var firstChannel = (scanDirection.Value == ScanDirection.Ascending) ? 0 : ChannelsCount - 1;
            if(currentChannel > 0 && currentChannel < ChannelsCount)
            {
                if(waitFlag.Value)
                {
                    awaitingConversion = true;
                    return;
                }
                this.Log(LogLevel.Warning, "Issued a start event before the last sequence finished");
            }
            currentChannel = firstChannel;
            SampleNextChannel();
        }

        private void SendDmaRequest()
        {
            if(Dma != null)
            {
                Dma.RequestTransfer(dmaChannel);
            }
            else
            {
                this.Log(LogLevel.Warning, "Received DMA transfer request, but no DMA is configured for this peripheral.");
            }
        }

        private void SampleNextChannel()
        {
            while(currentChannel < ChannelsCount && currentChannel >= 0)
            {
                if(!channelSelected[currentChannel])
                {
                    currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? currentChannel + 1 : currentChannel - 1;
                    continue;
                }
                else
                {
                    data.Value = getSampleFromChannel(currentChannel);
                    if(dmaEnabled.Value)
                    {
                        SendDmaRequest();
                    }
                    endOfSamplingFlag.Value = true;
                    if(analogWatchdogEnable.Value && (!analogWatchdogSingleChannel.Value || analogWatchdogChannel.Value == currentChannel))
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
                    currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? currentChannel + 1 : currentChannel - 1;
                    return;
                }
            }
            this.Log(LogLevel.Debug, "No more channels enabled");
            endOfSequenceFlag.Value = true;
            UpdateInterrupts();
            if(awaitingConversion)
            {
                awaitingConversion = false;
                StartSampling();
            }
        }

        private uint getSampleFromChannel(int channelNumber)
        {
            var sample = sampleProvider[channelNumber].Sample;
            sampleProvider[channelNumber].TryDequeueNewSample();
            return milivoltsToSample((double)sample.Value);
        }

        private uint milivoltsToSample(double sampleInMilivolts)
        {
            uint referencedValue = (uint)Math.Round((sampleInMilivolts / (referenceVoltage* 1000)) * ((1 << resolutionInBits) - 1));
            if(align.Value == Align.Left)
            {
                referencedValue = referencedValue << (16 - resolutionInBits);
            }
            return referencedValue;
        }

        private void DefineRegisters()
        {
            Register.InterruptAndStatus.Define(this)
                .WithFlag(0, out adcReadyFlag, writeCallback: (_, val) => { if(val) adcReadyFlag.Value = false; }, name:"ADRDY")
                .WithFlag(1, out endOfSamplingFlag, writeCallback: (_, val) => { if(val) endOfSamplingFlag.Value = false; }, name: "EOSMP")
                .WithFlag(2, out endOfConversionFlag, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            endOfConversionFlag.Value = false;
                            SampleNextChannel();
                        }
                    }, name: "EOC")
                .WithFlag(3, out endOfSequenceFlag, writeCallback: (_, val) => { if(val) endOfSequenceFlag.Value = false; }, name: "EOSEQ")
                .WithFlag(4, out adcOverrunFlag, writeCallback: (_, val) => { if(val) adcOverrunFlag.Value = false; }, name: "OVR")
                .WithReservedBits(5, 2)
                .WithFlag(7, out analogWatchdogFlag, writeCallback: (_, val) => { if(val) analogWatchdogFlag.Value = false; }, name: "AWD")
                .WithReservedBits(8,23);
            Register.InterruptEnable.Define(this)
                .WithFlag(0, out adcReadyInterruptEnable, name:"ADRDYIE")
                .WithFlag(1, out endOfSamplingInterruptEnable, name: "EOSMPIE")
                .WithFlag(2, out endOfConversionInterruptEnable, name: "EOCIE")
                .WithFlag(3, out endOfSequenceInterruptEnable, name: "EOSEQIE")
                .WithFlag(4, out adcOverrunInterruptEnable, name: "OVRIE")
                .WithReservedBits(5, 2)
                .WithFlag(7, out analogWatchdogInterruptEnable, name:"AWDIE")
                .WithReservedBits(8,23);
            Register.Control.Define(this)
                .WithFlag(0, out enabled, writeCallback: (_, val) => { if(val)  adcReadyFlag.Value = true; UpdateInterrupts(); }, name: "ADEN")
                .WithFlag(1, writeCallback: (_, val) => { if(val) enabled.Value = false; }, name: "ADDIS")
                .WithFlag(2, writeCallback: (_, val) =>
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
                .WithFlag(4, out disabled, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            samplingThread.Stop();
                        }
                    }, name: "ADSTP")
                .WithReservedBits(5,25)
                .WithTaggedFlag("ADCAL", 31);
            Register.Configuration1.Define(this)
                .WithFlag(0, out dmaEnabled, name: "DMAEN")
                .WithEnumField<DoubleWordRegister, DmaMode>(1, 1, out dmaMode, name:  "DMACFG")
                .WithEnumField<DoubleWordRegister, ScanDirection>(2, 1, out scanDirection, name: "SCANDIR")
                .WithValueField(3, 2, writeCallback: (_, val) =>
                    {
                        switch((Resolution)val)
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
                        }
                    }, name: "RES")
                .WithEnumField<DoubleWordRegister, Align>(5, 1,  out align, name:"ALIGN")
                .WithTag("EXTSEL", 6, 2)
                .WithReservedBits(9, 1)
                .WithValueField(10, 2, writeCallback: (_, val) => { externalTrigger = (val>0); }, name: "EXTEN")
                .WithTaggedFlag("OVRMOD", 12)
                .WithTaggedFlag("CONT", 13)
                .WithFlag(14, out waitFlag, name:"WAIT")
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
                       writeCallback: (id, _, val) => { this.Log(LogLevel.Debug, "Channel {0}  set as {1}", id, val); channelSelected[id] = val; })
                .WithReservedBits(ChannelsCount, 32 - ChannelsCount);
            Register.DataRegister.Define(this)
                .WithValueField(0, 16, out data, FieldMode.Read, readCallback: (_, __) =>
                    {
                        endOfConversionFlag.Value = false;
                        machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                    }, name: "DATA")
                .WithReservedBits(16, 16);
            Register.CommonConfiguration.Define(this)
                .WithReservedBits(0, 22)
                .WithTaggedFlag("VREFEN", 22)
                .WithTaggedFlag("TSEN", 23)
                .WithTaggedFlag("VBATEN", 24)
                .WithReservedBits(25, 7);
        }

        private IEnumRegisterField<Align> align;
        private IEnumRegisterField<DmaMode> dmaMode;
        private IEnumRegisterField<ScanDirection> scanDirection;

        private readonly SensorSamplesFifo<ScalarSample>[] sampleProvider;

        private IFlagRegisterField enabled;
        private IFlagRegisterField disabled;
        private IFlagRegisterField dmaEnabled;
        private IFlagRegisterField analogWatchdogEnable;
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

        private IValueRegisterField data;
        private IFlagRegisterField analogWatchdogSingleChannel;
        private IValueRegisterField analogWatchdogChannel;
        private IValueRegisterField analogWatchdogHighValue;
        private IValueRegisterField analogWatchdogLowValue;

        private bool[] channelSelected;
        private bool externalTrigger;
        private bool awaitingConversion;
        private ushort resolutionInBits;
        private int currentChannel;

        private const int ChannelsCount = 19;
        private readonly IManagedThread samplingThread;
        private readonly IDMA Dma;
        private readonly int dmaChannel;
        private readonly double referenceVoltage;
        private readonly int externalEventFrequency;

        private enum DmaMode
        {
            OneShot      = 0x0,
            CircularMode = 0x1,
        }

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
