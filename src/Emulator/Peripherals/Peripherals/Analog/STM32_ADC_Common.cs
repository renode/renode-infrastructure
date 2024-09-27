//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Sensors;

namespace Antmicro.Renode.Peripherals.Analog
{
    // Superset of all ADC features found on many STM MPU series.
    //
    // Available features:
    //     watchdogCount ------ Specifies the number of analog watchdogs inside the peripheral between 1 and 3.
    //    *hasCalibration ----- Specifies whether the calibration factor and voltage regulator are available to the software.
    //                          ADCs without this feature will still have the ADCAL flag available to trigger the calibration procedure,
    //                          but not the CALFACT register nor the ADVREGEN field.
    //     channelCount ------- Specifies the amount of available channels.
    //                          Includes both internal sources (like the temperature sensor) as well as external.
    //    *hasPrescaler ------- Specifies whether the ADC contains a prescaler for the external clock input.
    //                          Technically either this property could be made an enum,
    //                          or there could be added a separate property which describes whether the internal clock can be used.
    //                          ex.
    //                            - the STM32F0xx can either use PCLK or the ADC asynchronous clock and has no precaler
    //                            - the STM32WBA only uses the ADC asynchronous clock but has a precaler
    //                          but for now, this feature describes both (i.e. true means has prescaler *and* no internal clock).
    //    *hasVbatPin --------- Specifies whether this ADC provides a pin for monitoring of an external power supply.
    //    *hasChannelSequence - Specifies whether this ADC provides a fully configurable sequencer.
    //                          If not, the ADC can convert a single channel or a sequence of channels,
    //                          but only scanning sequentially either forwards or backwards.
    //    *hasPowerRegister --- Specifies whether this ADC has a separate register for power managment.
    //                          If false, that means the model exposes features like auto-off in one of the configuration registers.
    //    *hasChannelSelect --- Specifies whether this ADC has channel selection register.
    //                          If false, third watchdog threshold configuration register will live under this register's offset.
    //
    // * - Feature is either partially implemented, or not at all.
    public abstract class STM32_ADC_Common : IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral
    {
        public STM32_ADC_Common(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel = 0, IDMA dmaPeripheral = null,
            int? watchdogCount = null, bool? hasCalibration = null, int? channelCount = null, bool? hasPrescaler = null,
            bool? hasVbatPin = null, bool? hasChannelSequence = null, bool? hasPowerRegister = null, bool? hasChannelSelect = null)
        {
            if(!watchdogCount.HasValue || !hasCalibration.HasValue || !channelCount.HasValue || !hasPrescaler.HasValue ||
                !hasVbatPin.HasValue || !hasChannelSequence.HasValue || !hasPowerRegister.HasValue || !hasChannelSelect.HasValue)
            {
                throw new ConstructionException("Missing configuration options");
            }

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

            this.machine = machine;

            bool calibration = hasCalibration.Value;
            bool prescaler = hasPrescaler.Value;
            bool vbatPin = hasVbatPin.Value;
            bool channelSequence = hasChannelSequence.Value;
            bool powerRegister = hasPowerRegister.Value;
            ChannelCount = channelCount.Value;
            WatchdogCount = watchdogCount.Value;
            this.hasChannelSelect = hasChannelSelect.Value;

            if(WatchdogCount < 1 || WatchdogCount > 3)
            {
                throw new ConstructionException("Invalid watchdog count");
            }

            analogWatchdogFlags = new IFlagRegisterField[WatchdogCount];
            analogWatchdogHighValues = new IValueRegisterField[WatchdogCount];
            analogWatchdogLowValues = new IValueRegisterField[WatchdogCount];
            if(WatchdogCount >= 2)
            {
                analogWatchdog2SelectedChannels = new IFlagRegisterField[ChannelCount];
            }
            if(WatchdogCount == 3)
            {
                analogWatchdog3SelectedChannels = new IFlagRegisterField[ChannelCount];
            }

            registers = new DoubleWordRegisterCollection(this, BuildRegistersMap(calibration, prescaler, vbatPin, channelSequence, powerRegister));

            IRQ = new GPIO();
            this.dmaChannel = dmaChannel;
            this.dma = dmaPeripheral;
            this.referenceVoltage = referenceVoltage;
            this.externalEventFrequency = externalEventFrequency;

            samplingThread = machine.ObtainManagedThread(StartSampling, externalEventFrequency);
            channelSelected = new bool[ChannelCount];
            sampleProvider = new SensorSamplesFifo<ScalarSample>[ChannelCount];
            for(var channel = 0; channel < ChannelCount; channel++)
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
            for(var i = 0; i < ChannelCount; i++)
            {
                sampleProvider[i].DefaultSample.Value = valueInmV;
            }
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            for(var i = 0; i < ChannelCount; i++)
            {
                channelSelected[i] = false;
            }
            currentChannel = 0;
            awaitingConversion = false;
            enabled = false;
            externalTrigger = false;
            sequenceInProgress = false;
            sequenceCounter = 0;
            samplingThread.Stop();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get => registers; }
        public long Size => 0x400;
        public GPIO IRQ { get; }

        private void ValidateChannel(int channel)
        {
            if(channel >= ChannelCount || channel < 0)
            {
                throw new RecoverableException($"Invalid argument value: {channel}. This peripheral implements only channels in range 0-{ChannelCount-1}");
            }
        }

        private void UpdateInterrupts()
        {
            var adcReady = adcReadyFlag.Value && adcReadyInterruptEnable.Value;
            var analogWatchdog = analogWatchdogsInterruptEnable.Zip(analogWatchdogFlags, (enable, flag) =>
            {
                return enable.Value && flag.Value;
            }).Any(flag => flag);
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
            if(hasChannelSelect)
            {
                currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? 0 : ChannelCount - 1;
            }
            else
            {
                sequenceCounter = (scanDirection.Value == ScanDirection.Ascending) ? 0 : (int)regularSequenceLength.Value;
                currentChannel = (int)regularSequence[sequenceCounter].Value;
            }
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

        private bool WatchdogEnabled(int watchdogNumber)
        {
            switch(watchdogNumber)
            {
            case 0:
                var enabledOnAll = !analogWatchdogSingleChannel.Value;
                var enabledOnCurrent = enabledOnAll || (int)analogWatchdogChannel.Value == currentChannel;
                return analogWatchdogEnable.Value && enabledOnCurrent;
            case 1:
                return analogWatchdog2SelectedChannels[currentChannel].Value;
            case 2:
                return analogWatchdog3SelectedChannels[currentChannel].Value;
            }
            throw new Exception("Unreachable, the watchdog count is checked in the constructor");
        }

        private void SampleNextChannel()
        {
            // Exit when peripheral is not enabled
            if(!enabled)
            {
                currentChannel = 0;
                sequenceCounter = 0;
                sequenceInProgress = false;
                return;
            }

            Func<bool> iterationFinished = null;
            if(hasChannelSelect)
            {
                iterationFinished = () => currentChannel >= ChannelCount;
            }
            else
            {
                iterationFinished = () => sequenceCounter > (int)regularSequenceLength.Value;
            }

            while(!iterationFinished() && currentChannel >= 0)
            {
                if(hasChannelSelect && !channelSelected[currentChannel])
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

                    for(int i = 0; i < WatchdogCount; i++)
                    {
                        if(WatchdogEnabled(i))
                        {
                            if(data.Value > analogWatchdogHighValues[i].Value || data.Value < analogWatchdogLowValues[i].Value)
                            {
                                analogWatchdogFlags[i].Value = true;
                                this.Log(LogLevel.Debug, "Analog watchdog {0} flag raised for value {1} on channel {2}", i, data.Value, currentChannel);
                            }
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
            sequenceCounter  = 0;
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
            if(hasChannelSelect)
            {
                currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? currentChannel + 1 : currentChannel - 1;
            }
            else
            {
                sequenceCounter = (scanDirection.Value == ScanDirection.Ascending) ? sequenceCounter + 1 : sequenceCounter - 1;
                // NOTE: Sequence finishes when `sequenceCounter` is either greater than `regularSequenceLength` or less than `0`.
                // In both of those cases, we assume that at this point `currentChannel` will contain invalid value.
                if(sequenceCounter >= 0 && sequenceCounter <= (int)regularSequenceLength.Value)
                {
                    currentChannel = (int)regularSequence[sequenceCounter].Value;
                }
            }
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

        private Dictionary<long, DoubleWordRegister> BuildRegistersMap(bool hasCalibration, bool hasPrescaler, bool hasVbatPin, bool hasChannelSequence, bool hasPowerRegister)
        {
            var isrRegister = new DoubleWordRegister(this)
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
                .WithFlags(7, WatchdogCount, out analogWatchdogFlags, FieldMode.Read | FieldMode.WriteOneToClear, name: "AWD")
                .WithReservedBits(7 + WatchdogCount, 4 - WatchdogCount)
                .WithReservedBits(13, 19);

            var interruptEnableRegister = new DoubleWordRegister(this)
                .WithFlag(0, out adcReadyInterruptEnable, name: "ADRDYIE")
                .WithFlag(1, out endOfSamplingInterruptEnable, name: "EOSMPIE")
                .WithFlag(2, out endOfConversionInterruptEnable, name: "EOCIE")
                .WithFlag(3, out endOfSequenceInterruptEnable, name: "EOSEQIE")
                .WithFlag(4, out adcOverrunInterruptEnable, name: "OVRIE")
                .WithReservedBits(5, 2)
                .WithFlags(7, WatchdogCount, out analogWatchdogsInterruptEnable, name: "AWDIE")
                .WithReservedBits(7 + WatchdogCount, 4 - WatchdogCount)
                .WithReservedBits(13, 19)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            if(hasCalibration)
            {
                isrRegister
                    .WithTaggedFlag("EOCAL", 11)
                    .WithTaggedFlag("LDORDY", 12);
                interruptEnableRegister
                    .WithTaggedFlag("EOCALIE", 11)
                    .WithTaggedFlag("LDORDYIE", 12);
            }
            else
            {
                isrRegister
                    .WithReservedBits(11, 2);
                interruptEnableRegister
                    .WithReservedBits(11, 2);
            }

            var configurationRegister1 = new DoubleWordRegister(this)
                .WithFlag(0, out dmaEnabled, name: "DMAEN")
                .WithTaggedFlag("DMACFG", 1)
                // When fully configurable channel sequencer is available, the SCANDIR and RES fields are swapped
                .WithEnumField<DoubleWordRegister, ScanDirection>(hasChannelSequence ? 4 : 2, 1, out scanDirection, name: "SCANDIR")
                .WithEnumField<DoubleWordRegister, Resolution>(hasChannelSequence ? 2 : 3, 2, out resolution, name: "RES")
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
                .WithTaggedFlag("DISCEN", 16)
                .WithReservedBits(17, 4)
                .WithFlag(22, out analogWatchdogSingleChannel, name: "AWDSGL")
                .WithFlag(23, out analogWatchdogEnable, name: "AWDEN")
                .WithReservedBits(24, 2)
                .WithValueField(26, 5, out analogWatchdogChannel, name: "AWDCH")
                .WithReservedBits(31, 1);

            if(!hasPowerRegister)
            {
                configurationRegister1
                    .WithTaggedFlag("AUTOFF", 15);
            }
            else
            {
                configurationRegister1
                    .WithReservedBits(15, 1);
            }

            var regularSequence1 = new DoubleWordRegister(this);

            if(hasChannelSequence)
            {
                if(hasChannelSelect)
                {
                    configurationRegister1
                        .WithFlag(21, name: "CHSELRMOD"); // no actual logic, but software expects to read the value back
                }
                else
                {
                    regularSequence1
                        .WithValueField(0, 4, out regularSequenceLength)
                        .WithReservedBits(28, 4);

                    for(var i = 0; i < 4; ++i)
                    {
                        var j = i;
                        regularSequence1
                            .WithReservedBits(4 + 6 * i, 2)
                            .WithValueField(6 + 6 * i, 4, out regularSequence[j]);
                    }
                }
            }
            else
            {
                configurationRegister1
                    .WithReservedBits(21, 1);

                regularSequence1
                    .WithReservedBits(0, 32);
            }

            var configurationRegister2 = new DoubleWordRegister(this)
                .WithReservedBits(0, 30)
                .WithTag("CKMODE", 30, 2);

            var commonConfigurationRegister = new DoubleWordRegister(this)
                .WithReservedBits(0, 18)
                .WithTaggedFlag("VREFEN", 22)
                .WithTaggedFlag("TSEN", 23)
                .WithReservedBits(25, 7);

            if(hasPrescaler)
            {
                commonConfigurationRegister
                    .WithValueField(18, 4, name: "PRESC");
            }
            else
            {
                commonConfigurationRegister
                    .WithReservedBits(18, 4);
            }

            if(hasVbatPin)
            {
                commonConfigurationRegister
                    .WithTaggedFlag("VBATEN", 24);
            }
            else
            {
                commonConfigurationRegister
                    .WithReservedBits(24, 1);
            }

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptAndStatus, isrRegister},
                {(long)Registers.InterruptEnable, interruptEnableRegister},
                {(long)Registers.Control, new DoubleWordRegister(this)
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
                    .WithReservedBits(5, 23)
                    .WithFlag(28, name: "ADVREGEN") // no logic implemented, but software expects to read this flag back
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("ADCAL", 31)
                },
                {(long)Registers.Configuration1, configurationRegister1},
                {(long)Registers.Configuration2, configurationRegister2},
                {(long)Registers.SamplingTime, new DoubleWordRegister(this)
                    .WithTag("SMP", 0, 3)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.RegularSequence1, regularSequence1},
                {(long)Registers.DataRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out data, FieldMode.Read, readCallback: (_, __) =>
                        {
                            endOfConversionFlag.Value = false;
                            // This function call must be delayed to avoid deadlock on registers access
                            if(sequenceInProgress)
                            {
                                machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                            }
                        }, name: "DATA")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.CommonConfiguration, commonConfigurationRegister},
            };

            // Optional registers
            if(hasChannelSelect)
            {
                registers.Add((long)Registers.ChannelSelection, new DoubleWordRegister(this)
                    .WithFlags(0, ChannelCount,
                           valueProviderCallback: (id, __) => channelSelected[id],
                           writeCallback: (id, _, val) => { this.Log(LogLevel.Debug, "Channel {0} enable set as {1}", id, val); channelSelected[id] = val; })
                    .WithReservedBits(ChannelCount, 32 - ChannelCount)
                );
            }

            if(WatchdogCount >= 1)
            {
                registers.Add((long)Registers.Watchdog1Threshold, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out analogWatchdogLowValues[0], name: "LT")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 12, out analogWatchdogHighValues[0], name: "HT")
                    .WithReservedBits(28, 4));
            }
            if(WatchdogCount >= 2)
            {
                registers.Add((long)Registers.Watchdog2Threshold, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out analogWatchdogLowValues[1], name: "LT")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 12, out analogWatchdogHighValues[1], name: "HT")
                    .WithReservedBits(28, 4));
                registers.Add((long)Registers.Watchdog2Configuration, new DoubleWordRegister(this)
                    .WithFlags(0, 14, out analogWatchdog2SelectedChannels, name: "AWD2CH")
                    .WithReservedBits(14, 18));
            }
            if(WatchdogCount == 3)
            {
                // NOTE: If given implementation doesn't have channel selection, the third Watchdog Threshold will be under ChannelSelection offset
                registers.Add(hasChannelSelect ? (long)Registers.Watchdog3Threshold : (long)Registers.ChannelSelection, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out analogWatchdogLowValues[2], name: "LT")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 12, out analogWatchdogHighValues[2], name: "HT")
                    .WithReservedBits(28, 4));
                registers.Add((long)Registers.Watchdog3Configuration, new DoubleWordRegister(this)
                    .WithFlags(0, 14, out analogWatchdog3SelectedChannels, name: "AWD3CH")
                    .WithReservedBits(14, 18));
            }

            if(hasCalibration)
            {
                registers.Add((long)Registers.CalibrationFactor, new DoubleWordRegister(this)
                    .WithValueField(0, 7, name: "CALFACT")
                    .WithReservedBits(7, 25));
            }

            if(hasPowerRegister)
            {
                registers.Add((long)Registers.Power, new DoubleWordRegister(this)
                    .WithTaggedFlag("AUTOFF", 0)
                    .WithTaggedFlag("DPD", 1) // Deep-power-down mode
                    .WithReservedBits(2, 30));
            }

            return registers;
        }

        private int currentChannel;
        private int sequenceCounter;
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
        private IFlagRegisterField[] analogWatchdogFlags;
        private IFlagRegisterField endOfConversionFlag;
        private IFlagRegisterField endOfSamplingFlag;
        private IFlagRegisterField endOfSequenceFlag;
        private IFlagRegisterField adcOverrunInterruptEnable;
        private IFlagRegisterField adcReadyInterruptEnable;
        private IFlagRegisterField[] analogWatchdogsInterruptEnable;
        private IFlagRegisterField endOfConversionInterruptEnable;
        private IFlagRegisterField endOfSamplingInterruptEnable;
        private IFlagRegisterField endOfSequenceInterruptEnable;
        private IFlagRegisterField analogWatchdogSingleChannel;

        private IValueRegisterField data;
        // Watchdog 1 either watches all channels or a single channel
        private IValueRegisterField analogWatchdogChannel;
        // While watchdogs 2 and 3 use bitfields for selecting channels to watch
        private IFlagRegisterField[] analogWatchdog2SelectedChannels;
        private IFlagRegisterField[] analogWatchdog3SelectedChannels;
        private IValueRegisterField[] analogWatchdogHighValues;
        private IValueRegisterField[] analogWatchdogLowValues;

        private IValueRegisterField regularSequenceLength;
        private IValueRegisterField[] regularSequence = new IValueRegisterField[MaximumSequenceLength];

        private readonly IDMA dma;
        private readonly int dmaChannel;
        private readonly bool hasChannelSelect;
        private readonly uint externalEventFrequency;
        private readonly double referenceVoltage;
        private readonly IManagedThread samplingThread;
        private readonly SensorSamplesFifo<ScalarSample>[] sampleProvider;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IMachine machine;

        private readonly int ChannelCount;
        private readonly int WatchdogCount;

        private const int MaximumSequenceLength = 16;

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

        private enum Registers
        {
            InterruptAndStatus     = 0x00, // ADC_ISR
            InterruptEnable        = 0x04, // ADC_IER
            Control                = 0x08, // ADC_CR
            Configuration1         = 0x0C, // ADC_CFGR1
            Configuration2         = 0x10, // ADC_CFGR2
            SamplingTime           = 0x14, // ADC_SMPR
            // Gap intended
            Watchdog1Threshold     = 0x20, // ADC_AWD1TR
            Watchdog2Threshold     = 0x24, // ADC_AWD2TR
            ChannelSelection       = 0x28, // ADC_CHSELR
            Watchdog3Threshold     = 0x2C, // ADC_AWD3TR
            RegularSequence1       = 0x30, // ADC_SQR1
            // Gap intended
            DataRegister           = 0x40, // ADC_DR
            // Gap intended
            Power                  = 0x44, // ADC_PWRR
            // Gap intended
            Watchdog2Configuration = 0xA0, // ADC_AWD2CR
            Watchdog3Configuration = 0xA4, // ADC_AWD3CR
            // Gap intended
            CalibrationFactor      = 0xC4, // ADC_CALFACT
            // Gap intended
            CommonConfiguration    = 0x308, // ADC_CCR
        }
    }
}
