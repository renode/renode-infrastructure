//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MAX86171 : ISPIPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public MAX86171(IMachine machine)
        {
            this.machine = machine;
            this.resdFrequencyMultiplier = 1;

            Interrupt1 = new GPIO();
            Interrupt2 = new GPIO();

            UpdateDefaultMeasurements();

            circularFifo = new AFESampleFIFO(this);
            measurementEnabled = new bool[MeasurementRegisterCount];

            measurementLEDASource = new byte[MeasurementRegisterCount];
            measurementLEDBSource = new byte[MeasurementRegisterCount];
            measurementLEDCSource = new byte[MeasurementRegisterCount];

            measurementLEDACurrent = new IValueRegisterField[MeasurementRegisterCount];
            measurementLEDBCurrent = new IValueRegisterField[MeasurementRegisterCount];
            measurementLEDCCurrent = new IValueRegisterField[MeasurementRegisterCount];

            measurementPDARange = new uint[MeasurementRegisterCount];
            measurementPDBRange = new uint[MeasurementRegisterCount];

            measurementPDAOffset = new ushort[MeasurementRegisterCount];
            measurementPDBOffset = new ushort[MeasurementRegisterCount];

            ledRange = new IValueRegisterField[MeasurementRegisterCount];

            RegistersCollection = new ByteRegisterCollection(this, BuildRegisterMap());
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, uint channelId = 0, ulong startTimestamp = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            lock(feederThreadLock)
            {
                feedingSamplesFromFile = true;
                feederThread?.Stop();
                resdStream?.Dispose();
                resdStream = this.CreateRESDStream<MAX86171_AFESample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
                resdStream.MetadataChanged += () =>
                {
                    staleConfiguration = true;
                };

                // The initial value is set to `RESDFrequencyMultiplier - 1`
                // in order to load a sample at the first callback instead of waiting for `RESDFrequencyMultiplier` of them
                var count = RESDFrequencyMultiplier - 1;
                feederThread = resdStream.StartSampleFeedThread(this, RESDFrequencyMultiplier * CalculateCurrentFrequency(), (sample, ts, status) =>
                {
                    count++;

                    if(status == RESDStreamStatus.AfterStream)
                    {
                        feedingSamplesFromFile = false;
                        TryFeedDefaultSample();
                        return;
                    }

                    if(count != RESDFrequencyMultiplier)
                    {
                        return;
                    }
                    count = 0;

                    if(status == RESDStreamStatus.OK)
                    {
                        circularFifo.EnqueueFrame(new AFESampleFrame(sample.Frame.Select(v => new AFESample(v)).ToArray()));
                    }
                    else
                    {
                        circularFifo.EnqueueFrame(defaultMeasurements);
                    }
                }, startTime: startTimestamp);
                this.Log(LogLevel.Info, "Started feeding samples from RESD file at {0}Hz", CalculateCurrentFrequency());
            }
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void FinishTransmission()
        {
            chosenRegister = null;
            state = null;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            chosenRegister = null;
            state = null;
            circularFifo.Reset();
            previousFifoTresholdReached = false;
            staleConfiguration = true;

            for(var i = 0; i < MeasurementRegisterCount; ++i)
            {
                measurementEnabled[i] = false;

                measurementLEDASource[i] = 0;
                measurementLEDBSource[i] = 0;
                measurementLEDCSource[i] = 0;

                measurementPDARange[i] = 0;
                measurementPDBRange[i] = 0;

                measurementPDAOffset[i] = 0;
                measurementPDBOffset[i] = 0;
            }
            UpdateInterrupts();

            feederThread?.Stop();
            feederThread = null;
            feedingSamplesFromFile = false;
        }

        public byte Transmit(byte data)
        {
            byte output = 0x00;
            if(chosenRegister == null)
            {
                // In first byte, we are choosing register to read or write
                chosenRegister = (Registers)data;
                return output;
            }

            if(state == null)
            {
                // In second byte, we are choosing transaction type
                state = (States)data;
                return output;
            }

            switch(state.Value)
            {
                case States.Write:
                    WriteByte((long)chosenRegister.Value, data);
                    break;
                case States.Read:
                    output = ReadByte((long)chosenRegister.Value);
                    break;
                default:
                    throw new Exception("unreachable code");
            }

            return output;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO Interrupt1 { get; }
        public GPIO Interrupt2 { get; }

        public int Measurement1ADCValue
        {
            get => measurement1ADCValue;
            set
            {
                measurement1ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement2ADCValue
        {
            get => measurement2ADCValue;
            set
            {
                measurement2ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement3ADCValue
        {
            get => measurement3ADCValue;
            set
            {
                measurement3ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement4ADCValue
        {
            get => measurement4ADCValue;
            set
            {
                measurement4ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement5ADCValue
        {
            get => measurement5ADCValue;
            set
            {
                measurement5ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement6ADCValue
        {
            get => measurement6ADCValue;
            set
            {
                measurement6ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement7ADCValue
        {
            get => measurement7ADCValue;
            set
            {
                measurement7ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement8ADCValue
        {
            get => measurement8ADCValue;
            set
            {
                measurement8ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        public int Measurement9ADCValue
        {
            get => measurement9ADCValue;
            set
            {
                measurement9ADCValue = value;
                UpdateDefaultMeasurements();
            }
        }

        // This propery allows RESD data to be sampled at N times frequency from the input file,
        // but still loading them every N'th sample into the FIFO (effectively keeping the initial frequency).
        // This mechanism may help with synchronization in some specific cases, especially when data from multiple sensors needs to be read precisely.
        // In general there is no need to change the multiplier from the default value of 1, though.
        public uint RESDFrequencyMultiplier
        {
            get => resdFrequencyMultiplier;
            set
            {
                if(feedingSamplesFromFile)
                {
                    throw new RecoverableException("Cannot change the RESD frequency multiplier while samples are being fed from a file");
                }

                if(value < 1)
                {
                    throw new RecoverableException($"Attempted to set RESD frequency multiplier to {value}. The multiplier has to be greater or equal to 1");
                }

                resdFrequencyMultiplier = value;
            }
        }

        public event Action OnFifoFull;

        private void UpdateStatus()
        {
            statusFifoFull.Value |= FifoThresholdReached && (!fifoAssertThresholdOnce.Value || (previousFifoTresholdReached != FifoThresholdReached));
            previousFifoTresholdReached = FifoThresholdReached;

            if(statusFifoFull.Value)
            {
                OnFifoFull?.Invoke();
            }
        }

        private void UpdateInterrupts()
        {
            // Currently, only A_FULL interrupt is supported for both INT1 and INT2
            // GPIO ports.

            var interrupt1 = false;
            interrupt1 = interrupt1FullEnabled.Value && statusFifoFull.Value;;

            var interrupt2 = false;
            interrupt2 = interrupt2FullEnabled.Value && statusFifoFull.Value;

            Interrupt1.Set(ApplyInterruptPolarity(interrupt1, polarityInterrupt1.Value));
            Interrupt2.Set(ApplyInterruptPolarity(interrupt2, polarityInterrupt2.Value));
        }

        private bool CheckIfConfigurationMatches()
        {
            if(!staleConfiguration)
            {
                return true;
            }

            var currentSample = resdStream?.CurrentSample;
            if(currentSample == null)
            {
                return true;
            }

            staleConfiguration = false;

            var metadataMatches = true;
            foreach(var channel in ActiveChannels.Cast<int>())
            {
                var channelId = channel + 1;
                var nonMatchingMetadata = new List<String>();
                if(currentSample.ConfigLedAExposure[channelId].HasValue && CalculateExposure((uint)measurementLEDACurrent[channel].Value, (uint)ledRange[channel].Value, out var ledAExposure) != currentSample.ConfigLedAExposure[channelId])
                {
                    nonMatchingMetadata.Add($"LED A exposure (expected: {currentSample.ConfigLedAExposure[channelId]}, got: {ledAExposure})");
                }
                if(currentSample.ConfigLedBExposure[channelId].HasValue && CalculateExposure((uint)measurementLEDBCurrent[channel].Value, (uint)ledRange[channel].Value, out var ledBExposure) != currentSample.ConfigLedBExposure[channelId])
                {
                    nonMatchingMetadata.Add($"LED B exposure (expected: {currentSample.ConfigLedBExposure[channelId]}, got: {ledBExposure})");
                }
                if(currentSample.ConfigLedCExposure[channelId].HasValue && CalculateExposure((uint)measurementLEDCCurrent[channel].Value, (uint)ledRange[channel].Value, out var ledCExposure) != currentSample.ConfigLedCExposure[channelId])
                {
                    nonMatchingMetadata.Add($"LED C exposure (expected: {currentSample.ConfigLedCExposure[channelId]}, got: {ledCExposure})");
                }
                if(currentSample.ConfigLedASource[channelId].HasValue && measurementLEDASource[channel] != currentSample.ConfigLedASource[channelId])
                {
                    nonMatchingMetadata.Add($"LED A source (expected: {measurementLEDASource[channel]}, got: {currentSample.ConfigLedASource[channelId]})");
                }
                if(currentSample.ConfigLedBSource[channelId].HasValue && measurementLEDBSource[channel] != currentSample.ConfigLedBSource[channelId])
                {
                    nonMatchingMetadata.Add($"LED B source (expected: {measurementLEDBSource[channel]}, got: {currentSample.ConfigLedBSource[channelId]})");
                }
                if(currentSample.ConfigLedCSource[channelId].HasValue && measurementLEDCSource[channel] != currentSample.ConfigLedCSource[channelId])
                {
                    nonMatchingMetadata.Add($"LED C source (expected: {measurementLEDCSource[channel]}, got: {currentSample.ConfigLedCSource[channelId]})");
                }
                if(currentSample.ConfigPD1ADCRange[channelId].HasValue && measurementPDARange[channel] != currentSample.ConfigPD1ADCRange[channelId])
                {
                    nonMatchingMetadata.Add($"PD A ADC range (expected: {measurementPDARange[channel]}, got: {currentSample.ConfigPD1ADCRange[channel]})");
                }
                if(currentSample.ConfigPD2ADCRange[channelId].HasValue && measurementPDBRange[channel] != currentSample.ConfigPD2ADCRange[channelId])
                {
                    nonMatchingMetadata.Add($"PD B ADC range (expected: {measurementPDBRange[channel]}, got: {currentSample.ConfigPD2ADCRange[channelId]})");
                }
                if(currentSample.ConfigPD1DACOffset[channelId].HasValue && measurementPDAOffset[channel] != currentSample.ConfigPD1DACOffset[channelId])
                {
                    nonMatchingMetadata.Add($"PD A DAC offset (expected: {measurementPDAOffset[channel]}, got: {currentSample.ConfigPD1DACOffset[channelId]})");
                }
                if(currentSample.ConfigPD2DACOffset[channelId].HasValue && measurementPDBOffset[channel] != currentSample.ConfigPD2DACOffset[channelId])
                {
                    nonMatchingMetadata.Add($"PD B DAC offset (expected: {measurementPDBOffset[channel]}, got: {currentSample.ConfigPD2DACOffset[channelId]})");
                }

                if(nonMatchingMetadata.Count > 0)
                {
                    metadataMatches = false;
                    this.Log(LogLevel.Warning, "Measurement {0} has non-matching configuration, found differences in: {1}",
                        channel + 1,
                        string.Join(", ", nonMatchingMetadata));
                }
            }

            return metadataMatches;
        }

        private uint CalculateExposure(uint driveCurrent, uint ledRange, out uint exposure)
        {
            uint multiplier;
            switch(ledRange)
            {
                case 0:
                    multiplier = 125;
                    break;
                case 1:
                    multiplier = 250;
                    break;
                case 2:
                    multiplier = 375;
                    break;
                case 3:
                    multiplier = 500;
                    break;
                default:
                    throw new Exception("unreachable code");
            }
            exposure = driveCurrent * multiplier;
            return exposure;
        }

        private bool ApplyInterruptPolarity(bool value, OutputPinPolarity polarity)
        {
            switch(polarity)
            {
                case OutputPinPolarity.OpenDrainActiveLow:
                case OutputPinPolarity.ActiveLow:
                    return !value;
                case OutputPinPolarity.ActiveHigh:
                    return value;
                default:
                    return value;
            }
        }

        private int GetDefaultValueForSampleSource(SampleSource ss)
        {
            switch(ss)
            {
                case SampleSource.PPGMeasurement1:
                    return Measurement1ADCValue;
                case SampleSource.PPGMeasurement2:
                    return Measurement2ADCValue;
                case SampleSource.PPGMeasurement3:
                    return Measurement3ADCValue;
                case SampleSource.PPGMeasurement4:
                    return Measurement4ADCValue;
                case SampleSource.PPGMeasurement5:
                    return Measurement5ADCValue;
                case SampleSource.PPGMeasurement6:
                    return Measurement6ADCValue;
                case SampleSource.PPGMeasurement7:
                    return Measurement7ADCValue;
                case SampleSource.PPGMeasurement8:
                    return Measurement8ADCValue;
                case SampleSource.PPGMeasurement9:
                    return Measurement9ADCValue;
                default:
                    this.Log(LogLevel.Warning, "No default value specified for {0}, returning 0");
                    return 0;
            }
        }

        private ByteRegister CreateDummyRegister(string name, byte defaultValue = 0x00, FieldMode fieldMode = FieldMode.Read | FieldMode.Write, bool verbose = true)
        {
            // As software could potentially want to check if writes to given register were successful,
            // we will be using this method to mock unimplemented registers to allow for persistent
            // writes and reads
            if(verbose)
            {
                return new ByteRegister(this, defaultValue)
                    .WithValueField(0, 8, fieldMode, name: name,
                        valueProviderCallback: value =>
                        {
                            this.Log(LogLevel.Warning, "Unhandled read from {0}; returning 0x{1:X02}", name, value);
                            return value;
                        },
                        writeCallback: (_, value) =>
                        {
                            this.Log(LogLevel.Warning, "Unhandled write to {0}; written 0x{1:X02}", name, value);
                        });
            }
            else
            {
                return new ByteRegister(this, defaultValue)
                    .WithValueField(0, 8, fieldMode, name: name);
            }
        }

        private Dictionary<long, ByteRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, ByteRegister>()
            {
                {(long)Registers.Status1, new ByteRegister(this)
                    .WithFlag(0, out statusFifoFull, FieldMode.ReadToClear, name: "STATUS1.a_full")
                    .WithTaggedFlag("STATUS1.frame_rdy", 1)
                    .WithTaggedFlag("STATUS1.fifo_data_rdy", 2)
                    .WithTaggedFlag("STATUS1.alc_ovf", 3)
                    .WithTaggedFlag("STATUS1.exp_ovf", 4)
                    .WithTaggedFlag("STATUS1.thresh2_hilo", 5)
                    .WithTaggedFlag("STATUS1.thresh1_hilo", 6)
                    .WithTaggedFlag("STATUS1.pwr_rdy", 7)
                },
                // Maked as non-verbose to limit the amount of log messages
                {(long)Registers.Status2, CreateDummyRegister("STATUS2.data", verbose: false)},
                // Maked as non-verbose to limit the amount of log messages
                {(long)Registers.Status3, CreateDummyRegister("STATUS3.data", verbose: false)},
                {(long)Registers.FIFOWritePointer, CreateDummyRegister("FIFO_WR_PTR.data", fieldMode: FieldMode.Read)},
                {(long)Registers.FIFOReadPointer, CreateDummyRegister("FIFO_RD_PTR.data", fieldMode: FieldMode.Read)},
                {(long)Registers.FIFOCounter1, new ByteRegister(this)
                    .WithValueField(0, 7, FieldMode.Read, name: "FIFO_CNT1.OVF_COUNTER",
                        valueProviderCallback: _ => 0)
                    .WithFlag(7, FieldMode.Read, name: "FIFO_CNT1.FIFO_DATA_COUNT_MSB",
                        valueProviderCallback: _ => (circularFifo.Count & 0x100) != 0)
                },
                {(long)Registers.FIFOCounter2, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "FIFO_CNT2.fifo_data_count_lsb",
                        valueProviderCallback: _ => circularFifo.Count)
                },
                {(long)Registers.FIFOData, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "FIFO_DATA.data",
                        valueProviderCallback: _ =>
                        {
                            CheckIfConfigurationMatches();
                            var output = circularFifo.DequeueByte();
                            if(clearFlagsOnRead.Value)
                            {
                                statusFifoFull.Value = false;
                                UpdateInterrupts();
                            }
                            return output;
                        })
                },
                {(long)Registers.FIFOConfiguration1, new ByteRegister(this)
                    .WithValueField(0, 8, out fifoFullThreshold, name: "FIFO_CONF1.fifo_a_full")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FIFOConfiguration2, new ByteRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, name: "FIFO_CONF2.fifo_ro",
                        valueProviderCallback: _ => circularFifo.Rollover,
                        writeCallback: (_, value) => circularFifo.Rollover = value)
                    .WithFlag(2, out fifoAssertThresholdOnce, name: "FIFO_CONF2.a_full_type")
                    .WithFlag(3, out clearFlagsOnRead, name: "FIFO_CONF2.fifo_stat_clr")
                    .WithFlag(4, FieldMode.WriteOneToClear | FieldMode.Read, name: "FIFO_CONF2.flush_fifo",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                circularFifo.Clear();

                                statusFifoFull.Value = false;
                                UpdateInterrupts();
                            }
                        })
                    .WithReservedBits(5, 3)
                },
                {(long)Registers.SystemConfiguration1, new ByteRegister(this)
                    .WithFlag(0, name: "SYSTEM_CONF1.reset",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, value) => { if(value) Reset(); })
                    .WithFlag(1, name: "SYSTEM_CONF1.shdn",
                        // While completely disabling clocks used for feeding samples to FIFO
                        // would be more in line with what actual software is doing, disabling writes
                        // to FIFO is easier to implement while also being agnostic to samples source
                        valueProviderCallback: _ => !circularFifo.Enabled,
                        writeCallback: (_, value) => circularFifo.Enabled = !value)
                    // Using Flag instead of TaggedFlag for persistancy of data
                    // written by software
                    .WithFlag(2, name: "SYSTEM_CONF1.ppg1_pwrdn")
                    .WithFlag(3, name: "SYSTEM_CONF1.ppg2_pwrdn")
                    .WithValueField(4, 2, name: "SYSTEM_CONF1.sync_mode")
                    .WithFlag(6, name: "SYSTEM_CONF1.sw_force_sync")
                    .WithFlag(7, name: "SYSTEM_CONF1.meas9_en",
                        valueProviderCallback: _ => measurementEnabled[(int)Channel.Measurement9],
                        changeCallback: (_, value) =>
                        {
                            measurementEnabled[(int)Channel.Measurement9] = value;
                            TryFeedDefaultSample();
                        })
                },
                {(long)Registers.SystemConfiguration2, new ByteRegister(this)
                    .WithFlags(0, 8, name: "SYSTEM_CONF2.MEASX_EN",
                        valueProviderCallback: (idx, _) => measurementEnabled[idx],
                        changeCallback: (idx, _, value) =>
                        {
                            measurementEnabled[idx] = value;
                            TryFeedDefaultSample();
                        })
                },
                {(long)Registers.SystemConfiguration3, CreateDummyRegister("SYSTEM_CONF3.data")},
                {(long)Registers.PhotodiodeBias, CreateDummyRegister("PHOTO_BIAS.data")},
                {(long)Registers.PinFunctionalConfiguration, CreateDummyRegister("PIN_FUNC_CONF.data")},
                {(long)Registers.OutputPinConfiguration, new ByteRegister(this)
                    .WithReservedBits(0, 1)
                    .WithEnumField<ByteRegister, OutputPinPolarity>(1, 2, out polarityInterrupt1, name: "OUT_PIN_CONF.int1_ocfg")
                    .WithEnumField<ByteRegister, OutputPinPolarity>(3, 2, out polarityInterrupt2,  name: "OUT_PIN_CONF.int2_ocfg")
                    .WithReservedBits(5, 3)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FrameRateClockFrequency, new ByteRegister(this, 0x20)
                    .WithTag("FR_CLK.fine_tune", 0, 5)
                    .WithFlag(5, out clockSelect, name: "FR_CLK.sel")
                    .WithReservedBits(6, 2)
                    .WithChangeCallback((_, __) => UpdateFrequency())
                },
                {(long)Registers.FrameRateClockDividerMSB, new ByteRegister(this, 0x1)
                    .WithValueField(0, 7, out clockDividerHigh, name: "FR_CLK.div_h")
                    .WithReservedBits(7, 1)
                    .WithChangeCallback((_, __) => UpdateFrequency())
                },
                {(long)Registers.FrameRateClockDividerLSB, new ByteRegister(this)
                    .WithValueField(0, 8, out clockDividerLow, name: "FR_CLK.div_l")
                    .WithChangeCallback((_, __) => UpdateFrequency())
                },
                {(long)Registers.ThresholdMeasurementSelect, CreateDummyRegister("THRESH_MEAS_SEL.data")},
                {(long)Registers.ThresholdHysteresis, CreateDummyRegister("THRESH_HYST.data")},
                {(long)Registers.PPGHiThreshold1, CreateDummyRegister("PPG_HI_THRESH1.data")},
                {(long)Registers.PPGLoThreshold1, CreateDummyRegister("PPG_LO_THRESH1.data")},
                {(long)Registers.PPGHiThreshold2, CreateDummyRegister("PPG_HI_THRESH2.data")},
                {(long)Registers.PPGLoThreshold2, CreateDummyRegister("PPG_LO_THRESH2.data")},
                {(long)Registers.PicketFenceMeasurementSelect, CreateDummyRegister("PICKET_FENCE_MEAS_SEL.data")},
                {(long)Registers.PicketFenceConfiguration, CreateDummyRegister("PICKET_FENCE_CONF.data")},
                {(long)Registers.Interrupt1Enable1, new ByteRegister(this)
                    // Using Flag instead of TaggedFlag for persistancy of data
                    // written by software
                    .WithFlag(0, name: "INT1_ENABLE1.led_tx_en")
                    .WithFlag(1, name: "INT1_ENABLE1.thresh1_hilo_en")
                    .WithFlag(2, name: "INT1_ENABLE1.thresh2_hilo_en")
                    .WithFlag(3, name: "INT1_ENABLE1.exp_ovf_en")
                    .WithFlag(4, name: "INT1_ENABLE1.alc_ovf_en")
                    .WithFlag(5, name: "INT1_ENABLE1.fifo_data_rdy_en")
                    .WithFlag(6, name: "INT1_ENABLE1.framerdy_en")
                    .WithFlag(7, out interrupt1FullEnabled, name: "INT1_ENABLE1.a_full_en")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Interrupt1Enable2, CreateDummyRegister("INT1_ENABLE2.data")},
                {(long)Registers.Interrupt1Enable3, CreateDummyRegister("INT1_ENABLE3.data")},
                {(long)Registers.Interrupt2Enable1, new ByteRegister(this)
                    // Using Flag instead of TaggedFlag for persistancy of data
                    // written by software
                    .WithFlag(0, name: "INT2_ENABLE1.led_tx_en")
                    .WithFlag(1, name: "INT2_ENABLE1.thresh1_hilo_en")
                    .WithFlag(2, name: "INT2_ENABLE1.thresh2_hilo_en")
                    .WithFlag(3, name: "INT2_ENABLE1.exp_ovf_en")
                    .WithFlag(4, name: "INT2_ENABLE1.alc_ovf_en")
                    .WithFlag(5, name: "INT2_ENABLE1.fifo_data_rdy_en")
                    .WithFlag(6, name: "INT2_ENABLE1.framerdy_en")
                    .WithFlag(7, out interrupt2FullEnabled, name: "INT2_ENABLE1.a_full_en")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Interrupt2Enable2, CreateDummyRegister("INT2_ENABLE2.data")},
                {(long)Registers.Interrupt2Enable3, CreateDummyRegister("INT2_ENABLE3.data")},
                {(long)Registers.PartID, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "PART_ID.part_id",
                        valueProviderCallback: _ => 0x2C)
                }
            };

            for(var i = 0; i < MeasurementRegisterCount; ++i)
            {
                var offset = i * 0x8;
                var j = i;

                registerMap.Add((long)Registers.Measurement1Select + offset, new ByteRegister(this)
                    .WithValueField(0, 2, name: $"MEAS{i+1}_SELECTS.meas{i+1}_drva",
                        writeCallback: (_, value) =>
                        {
                            // Map register value to LED source index
                            switch(value)
                            {
                                case 0:
                                    measurementLEDASource[j] = 1;
                                    break;
                                case 1:
                                    measurementLEDASource[j] = 2;
                                    break;
                                case 2:
                                    measurementLEDASource[j] = 4;
                                    break;
                                case 3:
                                    measurementLEDASource[j] = 7;
                                    break;
                                default:
                                    throw new Exception("unreachable code");
                            }
                        })
                    .WithValueField(2, 2, name: $"MEAS{i+1}_SELECTS.meas{i+1}_drvb",
                        writeCallback: (_, value) =>
                        {
                            // Map register value to LED source index
                            switch(value)
                            {
                                case 0:
                                    measurementLEDBSource[j] = 2;
                                    break;
                                case 1:
                                    measurementLEDBSource[j] = 3;
                                    break;
                                case 2:
                                    measurementLEDBSource[j] = 5;
                                    break;
                                case 3:
                                    measurementLEDBSource[j] = 8;
                                    break;
                                default:
                                    throw new Exception("unreachable code");
                            }
                        })
                    .WithValueField(4, 2, name: $"MEAS{i+1}_SELECTS.meas{i+1}_drvc",
                        writeCallback: (_, value) =>
                        {
                            // Map register value to LED source index
                            switch(value)
                            {
                                case 0:
                                    measurementLEDCSource[j] = 1;
                                    break;
                                case 1:
                                    measurementLEDCSource[j] = 3;
                                    break;
                                case 2:
                                    measurementLEDCSource[j] = 6;
                                    break;
                                case 3:
                                    measurementLEDCSource[j] = 9;
                                    break;
                                default:
                                    throw new Exception("unreachable code");
                            }
                        })
                    .WithFlag(6, name: $"MEAS{i+1}_SELECTS.meas{i+1}_amb")
                    .WithReservedBits(7, 1)
                    .WithChangeCallback((_, __) => staleConfiguration = true));
                registerMap.Add((long)Registers.Measurement1Configuration1 + offset, CreateDummyRegister($"MEAS{i+1}_CONF1.data"));
                registerMap.Add((long)Registers.Measurement1Configuration2 + offset, new ByteRegister(this, 0x3A)
                    .WithValueField(0, 2, name: $"MEAS{i+1}_CONF2.meas{i+1}_ppg1_adc_rge",
                        writeCallback: (_, value) => measurementPDARange[j] = (uint)(4 << (int)value))
                    .WithValueField(2, 2, name: $"MEAS{i+1}_CONF2.meas{i+1}_ppg2_adc_rge",
                        writeCallback: (_, value) => measurementPDBRange[j] = (uint)(4 << (int)value))
                    .WithValueField(4, 2, out ledRange[i], name: $"MEAS{i+1}_CONF2.meas{i+1}_led_rge")
                    .WithFlag(6, name: $"MEAS{i+1}_CONF2.meas{i+1}_filt_sel")
                    .WithFlag(7, name: $"MEAS{i+1}_CONF2.meas{i+1}_sinc3_sel")
                    .WithChangeCallback((_, __) => staleConfiguration = true));
                registerMap.Add((long)Registers.Measurement1Configuration3 + offset, new ByteRegister(this, 0x50)
                    .WithValueField(0, 2, name: $"MEAS{i+1}_CONF3.meas{i+1}_ppg1_dacoff",
                        writeCallback: (_, value) => measurementPDAOffset[j] = (ushort)(8 * value))
                    .WithValueField(2, 2, name: $"MEAS{i+1}_CONF3.meas{i+1}_ppg2_dacoff",
                        writeCallback: (_, value) => measurementPDBOffset[j] = (ushort)(8 * value))
                    .WithValueField(4, 2, name: $"MEAS{i+1}_CONF3.meas{i+1}_led_setlng")
                    .WithValueField(6, 2, name: $"MEAS{i+1}_CONF3.meas{i+1}_pd_setlng")
                    .WithChangeCallback((_, __) => staleConfiguration = true));
                registerMap.Add((long)Registers.Measurement1DriverACurrent + offset, new ByteRegister(this)
                    .WithValueField(0, 8, out measurementLEDACurrent[i], name: $"MEAS{i+1}_DRVA_CURRENT.data")
                    .WithChangeCallback((_, __) => staleConfiguration = true));
                registerMap.Add((long)Registers.Measurement1DriverBCurrent + offset, new ByteRegister(this)
                    .WithValueField(0, 8, out measurementLEDBCurrent[i], name: $"MEAS{i+1}_DRVB_CURRENT.data")
                    .WithChangeCallback((_, __) => staleConfiguration = true));
                registerMap.Add((long)Registers.Measurement1DriverCCurrent + offset, new ByteRegister(this)
                    .WithValueField(0, 8, out measurementLEDCCurrent[i], name: $"MEAS{i+1}_DRVC_CURRENT.data")
                    .WithChangeCallback((_, __) => staleConfiguration = true));
            }

            return registerMap;
        }

        private bool TryFeedDefaultSample()
        {
            lock(feederThreadLock)
            {
                if(feedingSamplesFromFile)
                {
                    return false;
                }

                feederThread?.Stop();
                if(measurementEnabled.Any(x => x))
                {
                    var freq = CalculateCurrentFrequency();
                    this.Log(LogLevel.Info, "Starting the default sample feeding at {0}Hz", freq);

                    Action feedSample = () =>
                    {
                        circularFifo.EnqueueFrame(defaultMeasurements);
                    };

                    Func<bool> stopCondition = () =>
                    {
                        return feedingSamplesFromFile;
                    };

                    feederThread = machine.ObtainManagedThread(feedSample, freq, "default_sample_afe", this, stopCondition);
                    feederThread.Start();

                    return true;
                }

                return false;
            }
        }

        private void UpdateDefaultMeasurements()
        {
            var ch1 = new AFESample(SampleSource.PPGMeasurement1, Measurement1ADCValue);
            var ch2 = new AFESample(SampleSource.PPGMeasurement2, Measurement2ADCValue);
            var ch3 = new AFESample(SampleSource.PPGMeasurement3, Measurement3ADCValue);
            var ch4 = new AFESample(SampleSource.PPGMeasurement4, Measurement4ADCValue);
            var ch5 = new AFESample(SampleSource.PPGMeasurement5, Measurement5ADCValue);
            var ch6 = new AFESample(SampleSource.PPGMeasurement6, Measurement6ADCValue);
            var ch7 = new AFESample(SampleSource.PPGMeasurement7, Measurement7ADCValue);
            var ch8 = new AFESample(SampleSource.PPGMeasurement8, Measurement8ADCValue);
            var ch9 = new AFESample(SampleSource.PPGMeasurement9, Measurement9ADCValue);

            // fifo requires two samples per channel
            defaultMeasurements = new AFESampleFrame(new []
            {
                ch1, ch1,
                ch2, ch2,
                ch3, ch3,
                ch4, ch4,
                ch5, ch5,
                ch6, ch6,
                ch7, ch7,
                ch8, ch8,
                ch9, ch9
            });
        }

        private void UpdateFrequency()
        {
            if(feederThread != null)
            {
                feederThread.Frequency = CalculateCurrentFrequency();
            }
        }

        public uint CalculateCurrentFrequency()
        {
            var clockBaseFrequency = clockSelect.Value
                ? 32768u
                : 32000u;
            return (uint)(clockBaseFrequency / ((clockDividerHigh.Value << 8) + clockDividerLow.Value));
        }

        private IEnumerable<Channel> ActiveChannels =>
            measurementEnabled.Select((active, idx) => active ? idx : -1).Where(x => x != -1).Select(x => (Channel)x);

        private bool FifoThresholdReached =>
            (MaximumFIFOCount - circularFifo.Count) <= fifoFullThreshold.Value;

        private readonly IMachine machine;
        private readonly AFESampleFIFO circularFifo;
        private readonly bool[] measurementEnabled;
        private readonly object feederThreadLock = new object();

        private readonly byte[] measurementLEDASource;
        private readonly byte[] measurementLEDBSource;
        private readonly byte[] measurementLEDCSource;

        private readonly uint[] measurementPDARange;
        private readonly uint[] measurementPDBRange;

        private readonly ushort[] measurementPDAOffset;
        private readonly ushort[] measurementPDBOffset;

        private bool feedingSamplesFromFile;
        private bool staleConfiguration;

        private int measurement1ADCValue;
        private int measurement2ADCValue;
        private int measurement3ADCValue;
        private int measurement4ADCValue;
        private int measurement5ADCValue;
        private int measurement6ADCValue;
        private int measurement7ADCValue;
        private int measurement8ADCValue;
        private int measurement9ADCValue;

        private uint resdFrequencyMultiplier;

        private AFESampleFrame defaultMeasurements;
        private IManagedThread feederThread;
        private RESDStream<MAX86171_AFESample> resdStream;
        private States? state;
        private Registers? chosenRegister;
        private bool previousFifoTresholdReached;

        private IFlagRegisterField statusFifoFull;

        private IFlagRegisterField clockSelect;
        private IValueRegisterField clockDividerHigh;
        private IValueRegisterField clockDividerLow;

        private IValueRegisterField[] measurementLEDACurrent;
        private IValueRegisterField[] measurementLEDBCurrent;
        private IValueRegisterField[] measurementLEDCCurrent;

        private IValueRegisterField[] ledRange;

        private IFlagRegisterField interrupt1FullEnabled;
        private IFlagRegisterField interrupt2FullEnabled;

        private IFlagRegisterField fifoAssertThresholdOnce;
        private IFlagRegisterField clearFlagsOnRead;

        private IValueRegisterField fifoFullThreshold;

        private IEnumRegisterField<OutputPinPolarity> polarityInterrupt1;
        private IEnumRegisterField<OutputPinPolarity> polarityInterrupt2;

        private const int MeasurementRegisterCount = 9;
        private const int MaximumFIFOCount = 256;

        [SampleType(SampleType.Custom)]
        private class MAX86171_AFESample : RESDSample
        {
            public override int? Width => null;

            public int[] Frame { get; private set; }

            public ushort?[] ConfigLedAExposure => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_a_exposure", out var value) ? value.As<ushort?>() : null
            ).ToArray();

            public byte?[] ConfigLedASource => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_a_source", out var value) ? value.As<byte?>() : null
            ).ToArray();

            public ushort?[] ConfigLedBExposure => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_b_exposure", out var value) ? value.As<ushort?>() : null
            ).ToArray();

            public byte?[] ConfigLedBSource => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_b_source", out var value) ? value.As<byte?>() : null
            ).ToArray();

            public ushort?[] ConfigLedCExposure => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_c_exposure", out var value) ? value.As<ushort?>() : null
            ).ToArray();

            public byte?[] ConfigLedCSource => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_led_c_source", out var value) ? value.As<byte?>() : null
            ).ToArray();

            public byte?[] ConfigPD1SourceFlags => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_a_source_flags", out var value) ? value.As<byte?>() : null
            ).ToArray();

            public uint?[] ConfigPD1ADCRange => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_a_adc_range", out var value) ? value.As<uint?>() : null
            ).ToArray();

            public short?[] ConfigPD1DACOffset => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_a_dac_offset", out var value) ? value.As<short?>() : null
            ).ToArray();

            public byte?[] ConfigPD2SourceFlags => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_b_source_flags", out var value) ? value.As<byte?>() : null
            ).ToArray();

            public uint?[] ConfigPD2ADCRange => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_b_adc_range", out var value) ? value.As<uint?>() : null
            ).ToArray();

            public short?[] ConfigPD2DACOffset => Enumerable.Range(0, MaxChannels).Select(i =>
                Metadata.TryGetValue($"meas{i}_pd_b_dac_offset", out var value) ? value.As<short?>() : null
            ).ToArray();

            public override bool TryReadFromStream(SafeBinaryReader reader)
            {
                if(!reader.TryReadByte(out var frameLength))
                {
                    return false;
                }
                var currentFrame = new int[frameLength];

                for(var i = 0; i < frameLength; ++i)
                {
                    if(!reader.TryReadInt32(out currentFrame[i]))
                    {
                        return false;
                    }
                }

                Frame = currentFrame;
                return true;
            }

            public override bool Skip(SafeBinaryReader reader, int count)
            {
                for(; count > 0 && !reader.EOF; count--)
                {
                    var frameLength = reader.ReadByte();
                    reader.SkipBytes(frameLength * 4);
                }
                return count == 0;
            }

            public override string ToString()
            {
                var sampleFrame = Frame.Select(sample => new AFESample(sample));
                return String.Join(", ", sampleFrame.Select(frame => $"[{frame}]"));
            }

            private const int MaxChannels = 9;
        }

        private class AFESampleFIFO
        {
            public AFESampleFIFO(MAX86171 parent)
            {
                samples = new Queue<AFESample>();
                this.parent = parent;
                Reset();
            }

            public void EnqueueFrame(AFESampleFrame frame)
            {
                var activeChannels = parent.ActiveChannels.ToList();
                var channelsInData = new List<Channel>();
                foreach(var samplePair in frame.SamplePairs)
                {
                    var channel = SampleSourceToChannel(samplePair[0].Tag);
                    if(!channel.HasValue || !activeChannels.Contains(channel.Value))
                    {
                        continue;
                    }
                    channelsInData.Add(channel.Value);

                    foreach(var sample in samplePair)
                    {
                        parent.circularFifo.EnqueueSample(sample);
                    }
                }

                var missingChannels = activeChannels.Except(channelsInData).ToList();
                if(missingChannels.Count > 0)
                {
                    parent.Log(LogLevel.Warning, "Provided sample data is missing samples for {0} channels: {1}",
                        missingChannels.Count,
                        string.Join(", ", missingChannels.Select(chan => chan.ToString())));
                }

                parent.UpdateStatus();
                parent.UpdateInterrupts();
            }

            public void EnqueueSample(AFESample sample)
            {
                if(!Enabled)
                {
                    return;
                }

                if(samples.Count == MaximumFIFOCount)
                {
                    parent.Log(LogLevel.Warning, "Sample FIFO overrun");
                    if(Rollover)
                    {
                        samples.Dequeue();
                    }
                    else
                    {
                        return;
                    }
                }
                samples.Enqueue(sample);
            }

            public byte DequeueByte()
            {
                byte output = default(byte);
                if(currentSampleEnumerator == null || !currentSampleEnumerator.TryGetNext(out output))
                {
                    if(samples.TryDequeue(out var currentSample))
                    {
                        currentSampleEnumerator = currentSample.Enumerator;
                        currentSampleEnumerator.TryGetNext(out output);
                    }
                    else
                    {
                        return (byte)SampleSource.InvalidData;
                    }
                }
                // output variable is always set to proper value
                return output;
            }

            public void Clear()
            {
                samples.Clear();
            }

            public void Reset()
            {
                Enabled = true;
                Rollover = false;
                Clear();
            }

            private Channel? SampleSourceToChannel(SampleSource ss)
            {
                switch(ss)
                {
                    case SampleSource.PPGMeasurement1:
                        return Channel.Measurement1;
                    case SampleSource.PPGMeasurement2:
                        return Channel.Measurement2;
                    case SampleSource.PPGMeasurement3:
                        return Channel.Measurement3;
                    case SampleSource.PPGMeasurement4:
                        return Channel.Measurement4;
                    case SampleSource.PPGMeasurement5:
                        return Channel.Measurement5;
                    case SampleSource.PPGMeasurement6:
                        return Channel.Measurement6;
                    case SampleSource.PPGMeasurement7:
                        return Channel.Measurement7;
                    case SampleSource.PPGMeasurement8:
                        return Channel.Measurement8;
                    case SampleSource.PPGMeasurement9:
                        return Channel.Measurement9;
                    default:
                        return null;
                }
            }

            public bool Enabled { get; set; }
            public bool Rollover { get; set; }
            public uint Count => (uint)samples.Count;

            private IEnumerator<byte> currentSampleEnumerator;

            private readonly Queue<AFESample> samples;
            private readonly MAX86171 parent;
        }

        private struct AFESample
        {
            public AFESample(decimal packet)
            {
                innerPacket = (int)packet;
            }

            public AFESample(SampleSource tag, int value)
            {
                innerPacket = (((int)tag & 0xF) << 20) | (value & 0x0FFFFF);
            }

            public int Value => ((innerPacket & 0x0FFFFF) << 12) >> 12;
            public SampleSource Tag => (SampleSource)((innerPacket & 0xF00000) >> 20);
            public byte Byte1 => (byte)((innerPacket & 0xFF0000) >> 16);
            public byte Byte2 => (byte)((innerPacket & 0x00FF00) >> 8);
            public byte Byte3 => (byte)(innerPacket & 0x0000FF);
            public byte[] Bytes => new byte[] { Byte1, Byte2, Byte3 };
            public IEnumerator<byte> Enumerator => Bytes.OfType<byte>().GetEnumerator();

            public override string ToString()
            {
                return $"{Tag}: {Value}";
            }

            private readonly int innerPacket;
        }

        private class AFESampleFrame
        {
            public AFESampleFrame(AFESample[] samples)
            {
                this.samples = samples;
            }

            public IEnumerable<AFESample[]> SamplePairs
            {
                get
                {
                    var i = 0;
                    while(i < samples.Length)
                    {
                        if((i + 1) >= samples.Length || samples[i].Tag != samples[i + 1].Tag)
                        {
                            Logger.Log(LogLevel.Warning, "Missing second sample for {0}", samples[i].Tag);
                            i += 1;
                            continue;
                        }

                        if(i > 0 && samples[i - 1].Tag > samples[i].Tag)
                        {
                            Logger.Log(LogLevel.Warning, "Invalid order of samples: {0} is before {1}", samples[i - 1].Tag, samples[i].Tag);
                        }

                        yield return new AFESample[2] { samples[i], samples[i + 1] };
                        i += 2;
                    }
                }
            }

            private AFESample[] samples;
        }

        private enum SampleSource : byte
        {
            Reserved1,
            PPGMeasurement1,
            PPGMeasurement2,
            PPGMeasurement3,
            PPGMeasurement4,
            PPGMeasurement5,
            PPGMeasurement6,
            PPGMeasurement7,
            PPGMeasurement8,
            PPGMeasurement9,
            PPGDarkData,
            PPGALCOverflow,
            PPGExposureOverflow,
            PPGPicketFenceData,
            InvalidData,
            Reaserved2,
        }

        private enum Channel
        {
            Measurement1,
            Measurement2,
            Measurement3,
            Measurement4,
            Measurement5,
            Measurement6,
            Measurement7,
            Measurement8,
            Measurement9,
        }

        private enum OutputPinPolarity
        {
            OpenDrainActiveLow = 0,
            ActiveHigh = 1,
            ActiveLow = 2,
            NotDefined = 3,
        }

        private enum States : byte
        {
            Write = 0x00,
            Read = 0x80,
        }

        private enum Registers : byte
        {
            Status1 = 0x00,
            Status2,
            Status3,

            FIFOWritePointer = 0x04,
            FIFOReadPointer,
            FIFOCounter1,
            FIFOCounter2,
            FIFOData,
            FIFOConfiguration1,
            FIFOConfiguration2,

            SystemConfiguration1 = 0x0C,
            SystemConfiguration2,
            SystemConfiguration3,
            PhotodiodeBias,
            PinFunctionalConfiguration,
            OutputPinConfiguration,

            FrameRateClockFrequency = 0x15,
            FrameRateClockDividerMSB,
            FrameRateClockDividerLSB,

            Measurement1Select = 0x18,
            Measurement1Configuration1,
            Measurement1Configuration2,
            Measurement1Configuration3,
            Measurement1DriverACurrent,
            Measurement1DriverBCurrent,
            Measurement1DriverCCurrent,

            Measurement2Select = 0x20,
            Measurement2Configuration1,
            Measurement2Configuration2,
            Measurement2Configuration3,
            Measurement2DriverACurrent,
            Measurement2DriverBCurrent,
            Measurement2DriverCCurrent,

            Measurement3Select = 0x28,
            Measurement3Configuration1,
            Measurement3Configuration2,
            Measurement3Configuration3,
            Measurement3DriverACurrent,
            Measurement3DriverBCurrent,
            Measurement3DriverCCurrent,

            Measurement4Select = 0x30,
            Measurement4Configuration1,
            Measurement4Configuration2,
            Measurement4Configuration3,
            Measurement4DriverACurrent,
            Measurement4DriverBCurrent,
            Measurement4DriverCCurrent,

            Measurement5Select = 0x38,
            Measurement5Configuration1,
            Measurement5Configuration2,
            Measurement5Configuration3,
            Measurement5DriverACurrent,
            Measurement5DriverBCurrent,
            Measurement5DriverCCurrent,

            Measurement6Select = 0x40,
            Measurement6Configuration1,
            Measurement6Configuration2,
            Measurement6Configuration3,
            Measurement6DriverACurrent,
            Measurement6DriverBCurrent,
            Measurement6DriverCCurrent,

            Measurement7Select = 0x48,
            Measurement7Configuration1,
            Measurement7Configuration2,
            Measurement7Configuration3,
            Measurement7DriverACurrent,
            Measurement7DriverBCurrent,
            Measurement7DriverCCurrent,

            Measurement8Select = 0x50,
            Measurement8Configuration1,
            Measurement8Configuration2,
            Measurement8Configuration3,
            Measurement8DriverACurrent,
            Measurement8DriverBCurrent,
            Measurement8DriverCCurrent,

            Measurement9Select = 0x58,
            Measurement9Configuration1,
            Measurement9Configuration2,
            Measurement9Configuration3,
            Measurement9DriverACurrent,
            Measurement9DriverBCurrent,
            Measurement9DriverCCurrent,

            ThresholdMeasurementSelect = 0x68,
            ThresholdHysteresis,
            PPGLoThreshold1,
            PPGHiThreshold1,
            PPGLoThreshold2,
            PPGHiThreshold2,

            PicketFenceMeasurementSelect = 0x70,
            PicketFenceConfiguration,

            Interrupt1Enable1 = 0x78,
            Interrupt1Enable2,
            Interrupt1Enable3,
            Interrupt2Enable1 = 0x7C,
            Interrupt2Enable2,
            Interrupt2Enable3,

            PartID = 0xFF,
        }
    }
}
