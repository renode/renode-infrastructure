//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    // Superset of all ADC features found on many STM MPU series.
    //
    // Available features:
    //     watchdogCount ------ Specifies the number of analog watchdogs inside the peripheral between 1 and 3.
    //    *hasCalibration ----- Specifies whether the calibration factor and voltage regulator are available to the software.
    //                          ADCs without this feature will still have the ADCAL flag available to trigger the calibration procedure,
    //                          but not the CALFACT register.
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
    //    *hasOffset ---------- Specifies whether this ADC has offset registers. These registers are tagged but not used by the model.
    //    *hasDifferentialMode  Specifies whether this has differential mode. The differential mode register is tagged but its
    //                          value is not used by the model.
    //    *samplingTime ------- Specifies from the SamplingTime enum how the sampling time registers are defined. These registers
    //                          are tagged but their value are not used by the model.
    //    *dualMode ----------- Indicates if there is a secondary ADC that can work in dual mode.
    //    hasLinearityCalibration - Specifies whether the ADC supports linear calibration procedure.
    //    *injectedChannels --- Specifies whether injected channels are supported (auto-injection, external trigger, queuing, JQOVF not implemented).
    //    hasSeparateThresholdRegisters - Specifies whether watchdog threshold values are stored in ADC_AWDnTR registers or ADC_LTRn and ADC_HTRn registers.
    //    resolutionRange -- Specifies bit resolution range this peripheral supports.
    //
    // * - Feature is either partially implemented, or not at all.
    public abstract class STM32_ADC_Common : IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IWordPeripheral, IADC
    {
        public STM32_ADC_Common(IMachine machine, double referenceVoltage, uint externalEventFrequency, int dmaChannel, IDMA dmaPeripheral,
            int watchdogCount, bool hasCalibration, int channelCount, bool hasPrescaler,
            bool hasVbatPin, bool hasChannelSequence, bool hasPowerRegister, bool hasChannelSelect,
            bool hasOffset, bool hasDifferentialMode, SamplingTime samplingTime, bool dualMode, bool hasLinearityCalibration, bool hasChannelInjection, bool hasSeparateThresholdRegisters, ResolutionRange resolutionRange)
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
                    throw new ConstructionException($"Invalid 'dmaChannel' argument value: '{dmaChannel}'. Available channels: 1-{dmaPeripheral.NumberOfChannels}");
                }
            }

            this.machine = machine;
            ADCContainer = new SimpleContainerHelper<IRESDSampleSource<VoltageSample>>(machine, this);

            ADCChannelCount = channelCount;
            WatchdogCount = watchdogCount;
            this.hasChannelSelect = hasChannelSelect;
            this.hasChannelInjection = hasChannelInjection;
            this.resolutionRange = resolutionRange;

            if(WatchdogCount < 1 || WatchdogCount > 3)
            {
                throw new ConstructionException("Invalid watchdog count");
            }
            if(hasSeparateThresholdRegisters && watchdogCount == 0)
            {
                throw new ConstructionException("Invalid Watchdog configuration");
            }

            registers = new DoubleWordRegisterCollection(this, BuildRegistersMap(hasCalibration,
                                                                                 hasPrescaler,
                                                                                 hasVbatPin,
                                                                                 hasChannelSequence,
                                                                                 hasPowerRegister,
                                                                                 hasOffset,
                                                                                 hasDifferentialMode,
                                                                                 samplingTime,
                                                                                 dualMode,
                                                                                 hasLinearityCalibration,
                                                                                 hasChannelInjection,
                                                                                 hasSeparateThresholdRegisters));

            IRQ = new GPIO();
            this.dmaChannel = dmaChannel;
            this.dma = dmaPeripheral;
            this.referenceVoltage = referenceVoltage;
            this.externalEventFrequency = externalEventFrequency;

            samplingThread = machine.ObtainManagedThread(StartSampling, externalEventFrequency);
            channelSelected = new bool[ADCChannelCount];
            Reset();

            machine.PeripheralsChanged += (machine, ev) =>
            {
                /* We need to create default children as soon as this ADC peripheral exists.
                 * However, the channel name must be unique at the machine level so the ADC name is
                 * prefixed. The creation driver first register the ADC device and then sets its
                 * local name. So the default children are created on the
                 * PeripheralChangeType.NamedChanged event instead of PeripheralChangeType.Addition.
                 */
                if(ev.Peripheral == this && ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.NameChanged)
                {
                    RegisterDefaultChildren(machine);
                }
            };
        }

        public void SetADCValue(int channel, uint valueMicroVolts)
        {
            IRESDSampleSource<VoltageSample> sampleSource;

            this.AssertChannel(channel);

            if(ADCContainer.TryGetByAddress(channel, out sampleSource) && sampleSource is ADCChannelSource channelSource)
            {
                this.WarningLog("This API is deprecated in favor of setting values from ADC sources");
                channelSource.Sample = new VoltageSample(valueMicroVolts);
            }
            else
            {
                this.ErrorLog("Cannot set value for channel {0}, use ADC sources API", channel);
            }
        }

        public uint GetADCValue(int channel)
        {
            IRESDSampleSource<VoltageSample> sampleSource;

            this.AssertChannel(channel);

            if(ADCContainer.TryGetByAddress(channel, out sampleSource))
            {
                this.WarningLog("This API is deprecated in favor of getting values from ADC sources");
                return sampleSource.Sample.Voltage;
            }
            else
            {
                // This should not happen as at least a default children is registered.
                this.ErrorLog("Cannot get value for channel {0}", channel);
                return 0;
            }
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            for(var i = 0; i < ADCChannelCount; i++)
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

        public ushort ReadWord(long offset)
        {
            return (ushort)RegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            RegistersCollection.Write(offset, value);
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Register(IRESDSampleSource<VoltageSample> peripheral, NumberRegistrationPoint<int> channel)
        {
            IRESDSampleSource<VoltageSample> sampleSource;

            this.AssertChannel(channel.Address);

            // Allow to register a new source over the default child.
            if(ADCContainer.TryGetByAddress(channel.Address, out sampleSource) && sampleSource is ADCDefaultChannelSource)
            {
                ADCContainer.Unregister(sampleSource);
            }

            ADCContainer.Register(peripheral, channel);
            peripheral.NewSample += newValue => WarnOnTooBigValue(channel.Address, (double)newValue.Voltage / 1e3); // µV to mV
        }

        public DoubleWordRegisterCollection RegistersCollection { get => registers; }

        public long Size => 0x400;

        public GPIO IRQ { get; }

        public int ADCChannelCount { get; }

        public SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; }

        private void WarnOnTooBigValue(int channel, double mv)
        {
            var maxValue = (1 << data.Width) - 1;

            if(MillivoltsToSample(mv, Resolution.Min) > maxValue)
            {
                this.Log(LogLevel.Warning, "Channel {0}: {1}mV is too big in any ADC configuration", channel, mv);
            }
            else if(MillivoltsToSample(mv) > maxValue)
            {
                this.Log(LogLevel.Warning, "Channel {0}: {1}mV is too big for current ADC resolution", channel, mv);
            }
            else if(MillivoltsToSample(mv, Resolution.Max) > maxValue)
            {
                this.Log(LogLevel.Debug,
                         "Channel {0}: {1}mV will be too big for some ADC resolution other than the current one",
                         channel, mv);
            }
        }

        private void UpdateInterrupts()
        {
            var irq = false;

            irq |= adcReadyFlag.Value && adcReadyInterruptEnable.Value;
            irq |= analogWatchdogsInterruptEnable.Zip(analogWatchdogFlags, (enable, flag) =>
            {
                return enable.Value && flag.Value;
            }).Any(flag => flag);
            irq |= endOfSamplingFlag.Value && endOfSamplingInterruptEnable.Value;
            irq |= endOfConversionFlag.Value && endOfConversionInterruptEnable.Value;
            irq |= endOfSequenceFlag.Value && endOfSequenceInterruptEnable.Value;
            irq |= adcOverrunFlag.Value && adcOverrunInterruptEnable.Value;
            if(hasChannelInjection)
            {
                irq |= endOfConversionInjectedFlag.Value && endOfConversionInjectedInterruptEnable.Value;
                irq |= endOfSequenceInjectedFlag.Value && endOfSequenceInjectedInterruptEnable.Value;
            }
            IRQ.Set(irq);
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
                currentChannel = (scanDirection.Value == ScanDirection.Ascending) ? 0 : ADCChannelCount - 1;
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

        private void StartInjectedSampling()
        {
            injectedSequenceCounter = 0;
            SampleNextInjectedChannel();
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
            default:
                return analogWatchdogSelectedChannels[watchdogNumber][currentChannel].Value;
            }
        }

        private ulong ClampSample(uint sample, int width)
        {
            if(sample < (1u << width))
            {
                return sample;
            }
            else
            {
                var clampedSample = (ulong)(1 << width) - 1;
                this.Log(LogLevel.Warning, "Sample value {0} is too big for ADC data register, clamping it to {1}",
                         sample, clampedSample);
                return clampedSample;
            }
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
                iterationFinished = () => currentChannel >= ADCChannelCount || currentChannel < 0;
            }
            else
            {
                iterationFinished = () => sequenceCounter > (int)regularSequenceLength.Value || currentChannel < 0;
            }

            // Skip disabled channels
            while(hasChannelSelect && !iterationFinished() && !channelSelected[currentChannel])
            {
                SwitchToNextChannel();
            }

            if(!iterationFinished())
            {
                uint sample = GetSampleFromChannel(currentChannel);
                if(!adcOverrunFlag.Value || overrunMode.Value)
                {
                    data.Value = ClampSample(sample, data.Width);
                }
                endOfSamplingFlag.Value = true;

                for(int i = 0; i < WatchdogCount; i++)
                {
                    if(WatchdogEnabled(i))
                    {
                        if(sample > analogWatchdogHighValues[i].Value || sample < analogWatchdogLowValues[i].Value)
                        {
                            analogWatchdogFlags[i].Value = true;
                            this.Log(LogLevel.Debug, "Analog watchdog {0} flag raised for value {1} on channel {2}", i, data.Value, currentChannel);
                        }
                    }
                }
                if(endOfConversionFlag.Value)
                {
                    adcOverrunFlag.Value = true;
                }
                endOfConversionFlag.Value = true;
                this.Log(LogLevel.Debug, "Sampled channel {0}", currentChannel);
                SwitchToNextChannel();
            }

            var didIterationFinish = iterationFinished();
            if(didIterationFinish)
            {
                this.Log(LogLevel.Debug, "No more channels enabled");
                endOfSequenceFlag.Value = true;
                sequenceInProgress = false;
                sequenceCounter = 0;
                startFlag.Value = false;
            }
            if(dmaEnabled.Value && !adcOverrunFlag.Value)
            {
                SendDmaRequest();
            }
            if(didIterationFinish && awaitingConversion)
            {
                awaitingConversion = false;
                StartSampling();
            }
            UpdateInterrupts();
        }

        private void SampleNextInjectedChannel()
        {
            // Exit when peripheral is not enabled
            if(!enabled)
            {
                injectedSequenceCounter = 0;
                return;
            }

            Func<bool> iterationFinished = () => injectedSequenceCounter > (int)injectedSequenceLength.Value;

            if(!iterationFinished())
            {
                int currentInjectedChannel = (int)injectedSequence[injectedSequenceCounter].Value;

                uint sample = GetSampleFromChannel(currentInjectedChannel);
                if(!adcOverrunFlag.Value || overrunMode.Value)
                {
                    var register = injectedData[injectedSequenceCounter];
                    register.Value = ClampSample(sample, register.Width);
                }

                endOfConversionInjectedFlag.Value = true;
                this.Log(LogLevel.Debug, "Sampled injected channel {0}", currentInjectedChannel);
                injectedSequenceCounter++;
            }

            if(iterationFinished())
            {
                this.Log(LogLevel.Debug, "No more injected channels enabled");
                startInjectionFlag.Value = false;
                endOfSequenceInjectedFlag.Value = true;
            }
            UpdateInterrupts();
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
            IRESDSampleSource<VoltageSample> sampleSource;
            var milliVolts = 0.0;

            if(ADCContainer.TryGetByAddress(channelNumber, out sampleSource))
            {
                milliVolts = sampleSource.Sample.Voltage / 1000;
            }
            return MillivoltsToSample(milliVolts);
        }

        private uint MillivoltsToSample(double sampleInMillivolts)
        {
            return MillivoltsToSample(sampleInMillivolts, resolution.Value);
        }

        private uint MillivoltsToSample(double sampleInMillivolts, Resolution sampleResolution)
        {
            ushort resolutionInBits = ResolutionToBits(sampleResolution);
            uint referencedValue = (uint)Math.Round((sampleInMillivolts / (referenceVoltage * 1000)) * ((1 << resolutionInBits) - 1));
            if(align.Value == Align.Left)
            {
                referencedValue = referencedValue << (16 - resolutionInBits);
            }
            return referencedValue;
        }

        private Dictionary<long, DoubleWordRegister> BuildRegistersMap(bool hasCalibration, bool hasPrescaler, bool hasVbatPin, bool hasChannelSequence, bool hasPowerRegister, bool hasOffset, bool hasDifferentialMode, SamplingTime samplingTime, bool dualMode, bool hasLinearityCalibration, bool hasChannelInjection, bool hasSeparateThresholdRegisters)
        {
            var isrRegister = new DoubleWordRegister(this)
                .WithFlag(0, out adcReadyFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "ADRDY")
                .WithFlag(1, out endOfSamplingFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "EOSMP")
                .WithFlag(2, out endOfConversionFlag, FieldMode.Read | FieldMode.WriteOneToClear,  writeCallback: (_, val) =>
                    {
                        if(val && sequenceInProgress)
                        {
                            // Clearing the End Of Conversion flag triggers next conversion
                            // This function call must be delayed to avoid deadlock on registers access
                            machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                        }
                    }, name: "EOC")
                .WithFlag(3, out endOfSequenceFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "EOSEQ")
                .WithFlag(4, out adcOverrunFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVR")
                .WithFlags(7, WatchdogCount, out analogWatchdogFlags, FieldMode.Read | FieldMode.WriteOneToClear, name: "AWD")
                .WithReservedBits(7 + WatchdogCount, 3 - WatchdogCount)
                .WithReservedBits(13, 19)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            var interruptEnableRegister = new DoubleWordRegister(this)
                .WithFlag(0, out adcReadyInterruptEnable, name: "ADRDYIE")
                .WithFlag(1, out endOfSamplingInterruptEnable, name: "EOSMPIE")
                .WithFlag(2, out endOfConversionInterruptEnable, name: "EOCIE")
                .WithFlag(3, out endOfSequenceInterruptEnable, name: "EOSEQIE")
                .WithFlag(4, out adcOverrunInterruptEnable, name: "OVRIE")
                .WithFlags(7, WatchdogCount, out analogWatchdogsInterruptEnable, name: "AWDIE")
                .WithReservedBits(7 + WatchdogCount, 3 - WatchdogCount)
                .WithReservedBits(13, 19)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            if(hasCalibration)
            {
                isrRegister
                    .WithTaggedFlag("EOCAL", 11)
                    // Simplified logic - hardware delays LDORDY until voltage regulator settles.
                    .WithFlag(12, valueProviderCallback: _ => adcRegulatorEnable.Value, name: "LDORDY");
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

            if(hasChannelInjection)
            {
                isrRegister
                    .WithFlag(5, out endOfConversionInjectedFlag, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                        {
                            if(val && startInjectionFlag.Value)
                            {
                                machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextInjectedChannel());
                            }
                        },
                        name: "JEOC")
                    .WithFlag(6, out endOfSequenceInjectedFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "JEOS")
                    .WithTaggedFlag("JQOVF", 10);

                interruptEnableRegister
                    .WithFlag(5, out endOfConversionInjectedInterruptEnable, name: "JEOCIE")
                    .WithFlag(6, out endOfSequenceInjectedInterruptEnable, name: "JEOSIE")
                    .WithTaggedFlag("JQOVFIE", 10);
            }
            else
            {
                isrRegister
                    .WithReservedBits(5, 2)
                    .WithReservedBits(10, 1);
                interruptEnableRegister
                    .WithReservedBits(5, 2)
                    .WithReservedBits(10, 1);
            }

            var configurationRegister1 = new DoubleWordRegister(this)
                .WithFlag(0, out dmaEnabled, name: "DMAEN")
                .WithFlag(1, writeCallback: (_, val) =>
                    {
                        if(!val && dmaEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "DMA One Shot mode not supported");
                        }
                    }, name: "DMACFG")
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
                .WithFlag(12, out overrunMode, name: "OVRMOD")
                .WithFlag(13, out continuous, writeCallback: (prevVal, val) =>
                    {
                        if(!val)
                        {
                            samplingThread.Stop();
                            sequenceInProgress = false;
                        }
                        else if(startFlag.Value && !prevVal)
                        {
                            this.Log(LogLevel.Warning, "Can set continuous mode only when ADSTART is 0");
                            continuous.Value = false;
                        }
                    }, name: "CONT")
                .WithFlag(14, out waitFlag, name: "WAIT")
                .WithTaggedFlag("DISCEN", 16)
                .WithTag("DISCNUM", 17, 3)
                .WithFlag(22, out analogWatchdogSingleChannel, name: "AWDSGL")
                .WithFlag(23, out analogWatchdogEnable, name: "AWDEN")
                .WithValueField(26, 5, out analogWatchdogChannel, name: "AWDCH");

            if(hasChannelInjection)
            {
                configurationRegister1
                    .WithTaggedFlag("JDISCEN", 20)
                    .WithTaggedFlag("JQM", 21)
                    .WithTaggedFlag("JAWD1EN", 24)
                    .WithTaggedFlag("JAUTO", 25)
                    .WithTaggedFlag("JQDIS", 31);
            }
            else
            {
                configurationRegister1
                    .WithReservedBits(20, 1)
                    .WithReservedBits(24, 2)
                    .WithReservedBits(31, 1);
            }

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

            if(hasChannelSequence)
            {
                if(!hasChannelInjection)
                {
                    configurationRegister1
                        .WithFlag(21, name: "CHSELRMOD"); // no actual logic, but software expects to read the value back
                }
            }
            else
            {
                configurationRegister1
                    .WithReservedBits(21, 1);
            }

            var configurationRegister2 = new DoubleWordRegister(this)
                .WithReservedBits(0, 30)
                .WithTag("CKMODE", 30, 2);

            var commonConfigurationRegister = new DoubleWordRegister(this)
                .WithReservedBits(0, 16)
                .WithValueField(16, 2, name: "CKMODE") // no actual logic, since we do not handle clock in this model
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

            var controlRegister = new DoubleWordRegister(this)
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
                                if(externalTrigger || continuous.Value)
                                {
                                    samplingThread.Start();
                                }
                                else
                                {
                                    StartSampling();
                                }
                            }
                        },name: "ADSTART")
                    // Reading one from below field would mean that command is in progress. This is never the case in this model
                    .WithFlag(4, valueProviderCallback: _ => false,  writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                samplingThread.Stop();
                                sequenceInProgress = false;
                            }
                        }, name: "ADSTP")
                    .WithFlag(28, out adcRegulatorEnable, name: "ADVREGEN")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("ADCAL", 31);

            if(hasLinearityCalibration)
            {
                controlRegister
                    .WithReservedBits(6, 2)
                    .WithFlags(8, 2, name: "BOOST")
                    .WithReservedBits(10, 6)
                    .WithFlag(16, name: "ADCALLIN")
                    .WithFlag(22, valueProviderCallback: _ => true, name: "LINCALRDYW1")
                    .WithFlag(23, valueProviderCallback: _ => true, name: "LINCALRDYW2")
                    .WithFlag(24, valueProviderCallback: _ => true, name: "LINCALRDYW3")
                    .WithFlag(25, valueProviderCallback: _ => true, name: "LINCALRDYW4")
                    .WithFlag(26, valueProviderCallback: _ => true, name: "LINCALRDYW5")
                    .WithFlag(27, valueProviderCallback: _ => true, name: "LINCALRDYW6");
            }
            else
            {
                controlRegister
                    .WithReservedBits(6, 10)
                    .WithReservedBits(16, 11);
            }

            if(hasChannelInjection)
            {
                controlRegister
                    .WithFlag(3, out startInjectionFlag, changeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                StartInjectedSampling();
                            }
                        }, name: "JADSTART")
                    .WithFlag(5, valueProviderCallback: _ => false, writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                startInjectionFlag.Value = false;
                            }
                        }, name: "JADSTP");
            }
            else
            {
                controlRegister
                    .WithReservedBits(3, 1)
                    .WithReservedBits(5, 1);
            }

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptAndStatus, isrRegister},
                {(long)Registers.InterruptEnable, interruptEnableRegister},
                {(long)Registers.Control, controlRegister},
                {(long)Registers.Configuration1, configurationRegister1},
                {(long)Registers.Configuration2, configurationRegister2},
                {(long)Registers.DataRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out data, FieldMode.Read, readCallback: (_, __) =>
                        {
                            endOfConversionFlag.Value = false;
                            // This function call must be delayed to avoid deadlock on registers access
                            if(sequenceInProgress)
                            {
                                machine.LocalTimeSource.ExecuteInNearestSyncedState((___) => SampleNextChannel());
                            }
                            UpdateInterrupts();
                        }, name: "DATA")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.CommonConfiguration, commonConfigurationRegister},
            };

            if(hasChannelSequence)
            {
                BuildRegularSequenceRegisters(registers, MaximumSequenceLength);
            }

            BuildSampingTimeRegisters(registers, samplingTime);

            // Optional registers
            if(hasChannelSelect)
            {
                registers.Add((long)Registers.ChannelSelection, new DoubleWordRegister(this)
                    .WithFlags(0, ADCChannelCount,
                           valueProviderCallback: (id, __) => channelSelected[id],
                           writeCallback: (id, _, val) => { this.Log(LogLevel.Debug, "Channel {0} enable set as {1}", id, val); channelSelected[id] = val; })
                    .WithReservedBits(ADCChannelCount, 32 - ADCChannelCount)
                );
            }

            BuildWatchdogRegisters(registers, hasSeparateThresholdRegisters);

            if(hasCalibration)
            {
                registers.Add((long)Registers.CalibrationFactor, new DoubleWordRegister(this)
                    .WithValueField(0, 7, name: "CALFACT")
                    .WithReservedBits(7, 25));
            }

            if(hasLinearityCalibration)
            {
                // Also present on U5 family, which does not have linearity calibration
                registers.Add((long)Registers.CalibrationFactor2, new DoubleWordRegister(this)
                    .WithValueField(0, 7, name: "CALFACT2")
                    .WithReservedBits(7, 25));
            }

            if(hasPowerRegister)
            {
                registers.Add((long)Registers.Power, new DoubleWordRegister(this)
                    .WithTaggedFlag("AUTOFF", 0)
                    .WithTaggedFlag("DPD", 1) // Deep-power-down mode
                    .WithReservedBits(2, 30));
            }

            if(hasChannelInjection)
            {
                registers.Add((long)Registers.InjectedSequence, new DoubleWordRegister(this)
                        .WithValueField(0, 2, out injectedSequenceLength, name: "JL")
                        .WithTag("JEXTSEL", 2, 4)
                        .WithTag("JEXTEN", 7, 2)
                        .WithValueField(9, 4, out injectedSequence[0], name: "JSQ1")
                        .WithReservedBits(14, 1)
                        .WithValueField(15, 4, out injectedSequence[1], name: "JSQ2")
                        .WithReservedBits(20, 1)
                        .WithValueField(21, 4, out injectedSequence[2], name: "JSQ3")
                        .WithReservedBits(26, 1)
                        .WithValueField(27, 4, out injectedSequence[3], name: "JSQ4"));

                registers.Add((long)Registers.InjectedChannel1, new DoubleWordRegister(this)
                        .WithValueField(0, 32, out injectedData[0], name: "JDATA1"));
                registers.Add((long)Registers.InjectedChannel2, new DoubleWordRegister(this)
                        .WithValueField(0, 32, out injectedData[1], name: "JDATA2"));
                registers.Add((long)Registers.InjectedChannel3, new DoubleWordRegister(this)
                        .WithValueField(0, 32, out injectedData[2], name: "JDATA3"));
                registers.Add((long)Registers.InjectedChannel4, new DoubleWordRegister(this)
                        .WithValueField(0, 32, out injectedData[3], name: "JDATA4"));
            }

            if(hasOffset)
            {
                for(uint i = 0; i < 4; i++)
                {
                    registers.Add((long)Registers.OffsetRegister1 + 4 * i, new DoubleWordRegister(this)
                        .WithTag("OFFSET", 0, 12)
                        .WithReservedBits(12, 14)
                        .WithTag("OFFSET_CH", 26, 5)
                        .WithTaggedFlag("OFFSET_EN", 31)
                    );
                }
            }

            if(hasDifferentialMode)
            {
                var register = hasSeparateThresholdRegisters ? Registers.DifferentialMode2 : Registers.DifferentialMode;
                registers.Add((long)register, new DoubleWordRegister(this)
                    .WithTag("DIFSEL", 0, 19)
                    .WithReservedBits(19, 13)
                );
            }

            if(dualMode)
            {
                /* dualMode is not really supported, let's mock ADEN and ADDIS so software can
                 * disable the ADC2 and checks that it is disabled.
                 */
                registers.Add((long)Registers.Control + 0x100, new DoubleWordRegister(this)
                    .WithTaggedFlag("ADEN", 0)
                    .WithFlag(1, valueProviderCallback: _ => false, name: "ADDIS")
                );
            }

            return registers;
        }

        private void BuildWatchdogRegisters(Dictionary<long, DoubleWordRegister> registers, bool hasSeparateThresholdRegisters)
        {
            Registers GetLowThresholdRegister(int i) => i switch
            {
                0 => Registers.WatchdogLowThreshold1,
                1 => Registers.WatchdogLowThreshold2,
                2 => Registers.WatchdogLowThreshold3,
                _ => throw new ConstructionException($"ADC_LT{i + 1} does not exist")
            };

            Registers GetHighThresholdRegister(int i) => i switch
            {
                0 => Registers.WatchdogHighThreshold1,
                1 => Registers.WatchdogHighThreshold2,
                2 => Registers.WatchdogHighThreshold3,
                _ => throw new ConstructionException($"ADC_HT{i + 1} does not exist")
            };

            // NOTE: If given implementation doesn't have channel selection, the third Watchdog Threshold will be under ChannelSelection offset
            Registers GetThresholdRegister(int i) => i switch
            {
                0 => Registers.Watchdog1Threshold,
                1 => Registers.Watchdog2Threshold,
                2 => hasChannelSelect ? Registers.Watchdog3Threshold : Registers.ChannelSelection,
                _ => throw new ConstructionException($"ADC_TR{i + 1} does not exist")
            };

            Registers GetConfigurationRegister(int i) => i switch
            {
                1 => Registers.Watchdog2Configuration,
                2 => Registers.Watchdog3Configuration,
                _ => throw new ConstructionException($"ADC_AWD{i + 1}CH does not exist")
            };

            analogWatchdogHighValues = new IValueRegisterField[WatchdogCount];
            analogWatchdogLowValues = new IValueRegisterField[WatchdogCount];
            analogWatchdogSelectedChannels = new Dictionary<int, IFlagRegisterField[]>();

            for(var i = 0; i < WatchdogCount; i++)
            {
                if(hasSeparateThresholdRegisters)
                {
                    registers.Add((long)GetLowThresholdRegister(i), new DoubleWordRegister(this)
                        .WithValueField(0, 26, out analogWatchdogLowValues[i], name: $"LT{i + 1}")
                        .WithReservedBits(26, 6));

                    registers.Add((long)GetHighThresholdRegister(i), new DoubleWordRegister(this)
                        .WithValueField(0, 26, out analogWatchdogHighValues[i], name: $"HT{i + 1}")
                        .WithReservedBits(26, 6));
                }
                else
                {
                    registers.Add((long)GetThresholdRegister(i), new DoubleWordRegister(this)
                        .WithValueField(0, 12, out analogWatchdogLowValues[i], name: $"LT{i + 1}")
                        .WithReservedBits(12, 4)
                        .WithValueField(16, 12, out analogWatchdogHighValues[i], name: $"HT{i + 1}")
                        .WithReservedBits(28, 4));
                }
                if(i > 0)
                {
                    registers.Add((long)GetConfigurationRegister(i), new DoubleWordRegister(this)
                        .WithFlags(0, ADCChannelCount, out var selectedChannels, name: $"AWD{i + 1}CH")
                        .WithReservedBits(ADCChannelCount, 31 - ADCChannelCount));
                    analogWatchdogSelectedChannels.Add(i, selectedChannels);
                }
            }
        }

        private void BuildRegularSequenceRegisters(Dictionary<long, DoubleWordRegister> registers, int sequenceCount)
        {
            DoubleWordRegister BuildRegularSequenceRegister(int offset, int sequenceCount, bool containsSequenceLength)
            {
                var register = new DoubleWordRegister(this);
                var sequenceOffset = 0;

                if(containsSequenceLength)
                {
                    register.WithValueField(0, 4, out regularSequenceLength, name: "L")
                        .WithReservedBits(4, 2);
                    sequenceOffset = 6;
                }

                for(var i = 0; i < sequenceCount; i++)
                {
                    var sequenceFieldWidth = 5;
                    var sequenceIndex = offset + i;

                    register
                        .WithValueField(sequenceOffset, sequenceFieldWidth, out regularSequence[sequenceIndex], name: $"SQ{sequenceIndex + 1}")
                        .WithReservedBits(sequenceOffset + sequenceFieldWidth, 1);
                    sequenceOffset += sequenceFieldWidth + 1;
                }
                register.WithReservedBits(sequenceOffset, register.RegisterWidth - sequenceOffset);
                return register;
            }

            Registers GetSequenceRegister(int i) => i switch
            {
                0 => Registers.RegularSequence1,
                1 => Registers.RegularSequence2,
                2 => Registers.RegularSequence3,
                3 => Registers.RegularSequence4,
                _ => throw new ConstructionException($"ADC_SQR{i} does not exist")
            };

            var sequenceOffset = 0;
            for(var i = 0; i < 4; i++)
            {
                var sequencesPerRegister = i == 0 ? 4 : 5;
                var sequencesInRegister = Math.Min(sequencesPerRegister, sequenceCount - sequenceOffset);

                var register = BuildRegularSequenceRegister(sequenceOffset, sequencesInRegister, i == 0);

                registers.Add((long)GetSequenceRegister(i), register);
                sequenceOffset += sequencesPerRegister;
            }
        }

        private void BuildSampingTimeRegisters(Dictionary<long, DoubleWordRegister> registers, SamplingTime samplingTime)
        {
            if(samplingTime == SamplingTime.OneForAll)
            {
                registers.Add((long)Registers.SamplingTime, new DoubleWordRegister(this)
                    .WithTag("SMP", 0, 3)
                    .WithReservedBits(3, 29)
                );
            }
            else if(samplingTime == SamplingTime.TwoSelections)
            {
                /* SMP1 and SMP2 defined in 0-2 and 4-6, other bits from 8 to 8 + channelCount are
                 * to select SMP1 or SMP2.
                 */
                var smpr = new DoubleWordRegister(this)
                    .WithTag("SMP1", 0, 3)
                    .WithReservedBits(3, 1)
                    .WithTag("SMP2", 4, 3)
                    .WithReservedBits(7, 1);
                for(int i = 0; i < ADCChannelCount; i++)
                {
                    smpr.Tag($"SMPSEL{i}", 8 + i, 1);
                }
                smpr.Reserved(32 - (24 - ADCChannelCount), 24 - ADCChannelCount);
                registers.Add((long)Registers.SamplingTime, smpr);
            }
            else if(samplingTime == SamplingTime.PerChannel)
            {
                /* 3 bits per channel, spread over 2 registers if needed. */
                var smpr1 = new DoubleWordRegister(this);
                var smpr2 = new DoubleWordRegister(this);

                for(int i = 0; i < ADCChannelCount && i < 10; i++)
                {
                    smpr1.Tag($"SMP{i}", 3 * i, 3);
                }
                var reservedBitsEntries = ADCChannelCount > 10 ? 0 : (10 - ADCChannelCount);
                var reservedBitsWidth = 2 + reservedBitsEntries * 3;
                smpr1.Reserved(32 - reservedBitsWidth, reservedBitsWidth);
                registers.Add((long)Registers.SamplingTime, smpr1);

                for(int i = 10; i < ADCChannelCount; i++)
                {
                    smpr2.Tag($"SMP{i}", 3 * (i - 10), 3);
                }
                reservedBitsEntries = ADCChannelCount > 20 ? 0 : (20 - ADCChannelCount);
                reservedBitsWidth = 2 + reservedBitsEntries * 3;
                smpr2.Reserved(32 - reservedBitsWidth, reservedBitsWidth);
                registers.Add((long)Registers.SamplingTime2, smpr2);
            }
        }

        private void RegisterDefaultChildren(IMachine machine)
        {
            var adcName = "";
            machine.TryGetLocalName(this, out adcName);

            for(var i = 0; i < ADCChannelCount; i++)
            {
                IRESDSampleSource<VoltageSample> channelSource = new ADCDefaultChannelSource();
                ((IADC)this).Register(channelSource, new NumberRegistrationPoint<int>(i));
                machine.SetLocalName(channelSource, $"{adcName}-channel{i}");
            }
        }

        private ushort ResolutionToBits(Resolution resolution)
        {
            if(resolutionRange == ResolutionRange.Bits8_16)
            {
                switch(resolution)
                {
                case Resolution.Bits12_16: return 16;
                case Resolution.Bits10_14: return 14;
                case Resolution.Bits8_12: return 12;
                case Resolution.Bits6_8: return 8;
                }
            }
            else if(resolutionRange == ResolutionRange.Bits6_12)
            {
                switch(resolution)
                {
                case Resolution.Bits12_16: return 12;
                case Resolution.Bits10_14: return 10;
                case Resolution.Bits8_12: return 8;
                case Resolution.Bits6_8: return 6;
                }
            }
            throw new NotImplementedException($"Missing {resolutionRange} bit support");
        }

        private IEnumRegisterField<Align> align;
        // While watchdogs 2 and 3 use bitfields for selecting channels to watch
        private IDictionary<int, IFlagRegisterField[]> analogWatchdogSelectedChannels;
        // Watchdog 1 either watches all channels or a single channel
        private IValueRegisterField analogWatchdogChannel;

        private IValueRegisterField data;
        private IFlagRegisterField analogWatchdogSingleChannel;
        private IFlagRegisterField endOfSequenceInterruptEnable;
        private IFlagRegisterField endOfSamplingInterruptEnable;
        private IFlagRegisterField endOfConversionInterruptEnable;
        private IFlagRegisterField[] analogWatchdogsInterruptEnable;
        private IFlagRegisterField adcReadyInterruptEnable;
        private IFlagRegisterField adcOverrunInterruptEnable;
        private IFlagRegisterField endOfSequenceFlag;
        private IFlagRegisterField endOfConversionFlag;
        private IFlagRegisterField[] analogWatchdogFlags;
        private IFlagRegisterField adcReadyFlag;
        private IFlagRegisterField adcRegulatorEnable;

        private IFlagRegisterField adcOverrunFlag;
        private IFlagRegisterField overrunMode;
        private IFlagRegisterField continuous;
        private IFlagRegisterField waitFlag;
        private IFlagRegisterField startFlag;
        private IFlagRegisterField analogWatchdogEnable;

        private IFlagRegisterField endOfConversionInjectedFlag;
        private IFlagRegisterField endOfSequenceInjectedFlag;
        private IFlagRegisterField endOfSequenceInjectedInterruptEnable;
        private IFlagRegisterField endOfConversionInjectedInterruptEnable;

        private IFlagRegisterField dmaEnabled;
        private IEnumRegisterField<ScanDirection> scanDirection;
        private IEnumRegisterField<Resolution> resolution;
        private IFlagRegisterField endOfSamplingFlag;

        private IValueRegisterField regularSequenceLength;

        private int currentChannel;
        private int sequenceCounter;
        private bool enabled;
        private bool externalTrigger;
        private bool sequenceInProgress;
        private bool awaitingConversion;
        private IFlagRegisterField startInjectionFlag;
        private IValueRegisterField injectedSequenceLength;
        private int injectedSequenceCounter;
        private IValueRegisterField[] analogWatchdogHighValues;
        private IValueRegisterField[] analogWatchdogLowValues;
        private readonly bool[] channelSelected;
        private readonly IValueRegisterField[] regularSequence = new IValueRegisterField[MaximumSequenceLength];
        private readonly IValueRegisterField[] injectedSequence = new IValueRegisterField[MaximumInjectedSequenceLength];
        private readonly IValueRegisterField[] injectedData = new IValueRegisterField[MaximumInjectedSequenceLength];

        private readonly IDMA dma;
        private readonly int dmaChannel;
        private readonly bool hasChannelSelect;
        private readonly ResolutionRange resolutionRange;
        private readonly uint externalEventFrequency;
        private readonly double referenceVoltage;
        private readonly IManagedThread samplingThread;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IMachine machine;

        private readonly int WatchdogCount;
        private readonly bool hasChannelInjection;
        private const int MaximumSequenceLength = 16;
        private const int MaximumInjectedSequenceLength = 4;

        public enum SamplingTime
        {
            OneForAll,
            TwoSelections,
            PerChannel,
        }

        public enum ResolutionRange
        {
            Bits8_16,
            Bits6_12,
        }

        private enum Resolution
        {
            Bits12_16 = 0b00,
            Max       = 0b00, // Keep alias in second place to display Bits12_16 name when dumping Resolution value
            Bits10_14 = 0b01,
            Bits8_12  = 0b10,
            Bits6_8   = 0b11,
            Min       = 0b11, // Keep alias in second place to display Bits6_8 name when dumping Resolution value
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
            SamplingTime           = 0x14, // ADC_SMPR/ADC_SMPR1
            SamplingTime2          = 0x18, // ADC_SMPR2
            Watchdog1Threshold     = 0x20, // ADC_AWD1TR
            WatchdogLowThreshold1  = 0x20, // ADC_LTR1
            Watchdog2Threshold     = 0x24, // ADC_AWD2TR
            WatchdogHighThreshold1 = 0x24, // ADC_HTR1
            ChannelSelection       = 0x28, // ADC_CHSELR
            Watchdog3Threshold     = 0x2C, // ADC_AWD3TR
            RegularSequence1       = 0x30, // ADC_SQR1
            RegularSequence2       = 0x34, // ADC_SQR2
            RegularSequence3       = 0x38, // ADC_SQR3
            RegularSequence4       = 0x3C, // ADC_SQR4
            DataRegister           = 0x40, // ADC_DR
            Power                  = 0x44, // ADC_PWRR
            InjectedSequence       = 0x4C, // ADC_JSQR
            // Gap intended
            OffsetRegister1        = 0x60, // ADC_OFR1
            OffsetRegister2        = 0x64, // ADC_OFR2
            OffsetRegister3        = 0x68, // ADC_OFR3
            OffsetRegister4        = 0x6C, // ADC_OFR4
            // Gap intended
            InjectedChannel1       = 0x80, // ADC_JDR1
            InjectedChannel2       = 0x84, // ADC_JDR2
            InjectedChannel3       = 0x88, // ADC_JDR3
            InjectedChannel4       = 0x8C, // ADC_JDR4
            // Gap intended
            Watchdog2Configuration = 0xA0, // ADC_AWD2CR
            Watchdog3Configuration = 0xA4, // ADC_AWD3CR
            // Gap intended
            DifferentialMode       = 0xB0, // ADC_DIFSEL
            WatchdogLowThreshold2  = 0xB0, // ADC_LTR2
            WatchdogHighThreshold2 = 0xB4, // ADC_HTR2
            WatchdogLowThreshold3  = 0xB8, // ADC_LTR3
            WatchdogHighThreshold3 = 0xBC, // ADC_HTR3
            // Gap intended
            DifferentialMode2      = 0xC0, // ADC_DIFSEL
            CalibrationFactor      = 0xC4, // ADC_CALFACT
            CalibrationFactor2     = 0xC8, // ADC_CALFACT2
            // Gap intended
            CommonConfiguration    = 0x308, // ADC_CCR
        }
    }
}
