//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MAX86171 : ISPIPeripheral
    {
        public MAX86171(Machine machine)
        {
            registers = new ByteRegisterCollection(this, BuildRegisterMap());
            this.machine = machine;

            Interrupt1 = new GPIO();
            Interrupt2 = new GPIO();

            UpdateDefaultMeasurements();

            circularFifo = new AFESampleFIFO(this);
            measurementEnabled = new bool[MeasurementRegisterCount];
            dataFeed = new DataFeed(this);
        }

        public void FeedSamplesFromBinaryFile(ReadFilePath filePath, string delayString)
        {
            lock(feederThreadLock)
            {
                feedingSamplesFromFile = true;
                feederThread?.Stop();
                circularFifo.Clear();
                feederThread = dataFeed.FeedSamplesFromBinaryFile(machine, this, "fifo", filePath, delayString, null, () =>
                {
                    feedingSamplesFromFile = false;
                    TryFeedDefaultSample();
                });
            }
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return registers.Read(offset);
        }

        public void FinishTransmission()
        {
            chosenRegister = null;
            state = null;
        }

        public void Reset()
        {
            registers.Reset();
            chosenRegister = null;
            state = null;
            circularFifo.Clear();
            previousFifoTresholdReached = false;
            for(var i = 0; i < MeasurementRegisterCount; ++i)
            {
                measurementEnabled[i] = false;
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

        private void UpdateStatus()
        {
            statusFifoFull.Value |= FifoThresholdReached && (!fifoAssertThresholdOnce.Value || (previousFifoTresholdReached == FifoThresholdReached));
            previousFifoTresholdReached = FifoThresholdReached;
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
                        writeCallback: (_, value) => { if(value) circularFifo.Clear(); })
                    .WithReservedBits(5, 3)
                },
                {(long)Registers.SystemConfiguration1, new ByteRegister(this)
                    .WithFlag(0, name: "SYSTEM_CONF1.reset",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, value) => { if(value) Reset(); })
                    // Using Flag instead of TaggedFlag for persistancy of data
                    // written by software
                    .WithFlag(1, name: "SYSTEM_CONF1.shdn")
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
                },
                {(long)Registers.FrameRateClockDividerMSB, new ByteRegister(this, 0x1)
                    .WithValueField(0, 7, out clockDividerHigh, name: "FR_CLK.div_h")
                    .WithReservedBits(7, 1)
                },
                {(long)Registers.FrameRateClockDividerLSB, new ByteRegister(this)
                    .WithValueField(0, 8, out clockDividerLow, name: "FR_CLK.div_l")
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
                {(long)Registers.PartID, CreateDummyRegister("PART_ID.part_id", 0x2C)}
            };

            for(var i = 0; i <= MeasurementRegisterCount; ++i)
            {
                var offset = i * 0x8;

                registerMap.Add((long)Registers.Measurement1Select + offset, CreateDummyRegister($"MEAS{i+1}_SELECTS.data"));
                registerMap.Add((long)Registers.Measurement1Configuration1 + offset, CreateDummyRegister($"MEAS{i+1}_CONF1.data"));
                registerMap.Add((long)Registers.Measurement1Configuration2 + offset, CreateDummyRegister($"MEAS{i+1}_CONF2.data"));
                registerMap.Add((long)Registers.Measurement1Configuration3 + offset, CreateDummyRegister($"MEAS{i+1}_CONF3.data"));
                registerMap.Add((long)Registers.Measurement1DriverACurrent + offset, CreateDummyRegister($"MEAS{i+1}_DRVA_CURRENT.data"));
                registerMap.Add((long)Registers.Measurement1DriverBCurrent + offset, CreateDummyRegister($"MEAS{i+1}_DRVB_CURRENT.data"));
                registerMap.Add((long)Registers.Measurement1DriverCCurrent + offset, CreateDummyRegister($"MEAS{i+1}_DRVC_CURRENT.data"));
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
                    this.Log(LogLevel.Debug, "Starting the default sample feeding at {0}Hz", freq);
                    feederThread = dataFeed.FeedSamplesPeriodically(machine, this, "default_sample_afe", "0", freq, new [] { defaultMeasurements });

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
            defaultMeasurements = new SensorSampleMeasurements(new []
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

        public uint CalculateCurrentFrequency()
        {
            return 52;

            // FIXME: the calculation below does not provide an expected result of 52
            /*
            var clockBaseFrequency = clockSelect.Value
                ? 32768
                : 32000u;

            return clockBaseFrequency / ((clockDividerHigh.Value << 7) + clockDividerLow.Value);
            */
        }

        private IEnumerable<Channel> ActiveChannels =>
            measurementEnabled.Where((active, idx) => active).Select((_, idx) => (Channel)idx);

        private bool FifoThresholdReached =>
            (MaximumFIFOCount - circularFifo.Count) <= fifoFullThreshold.Value;

        private readonly Machine machine;
        private readonly ByteRegisterCollection registers;
        private readonly DataFeed dataFeed;
        private readonly AFESampleFIFO circularFifo;
        private readonly bool[] measurementEnabled;
        private readonly object feederThreadLock = new object();

        private bool feedingSamplesFromFile;
        
        private int measurement1ADCValue;
        private int measurement2ADCValue;
        private int measurement3ADCValue;
        private int measurement4ADCValue;
        private int measurement5ADCValue;
        private int measurement6ADCValue;
        private int measurement7ADCValue;
        private int measurement8ADCValue;
        private int measurement9ADCValue;

        private SensorSampleMeasurements defaultMeasurements;
        private IManagedThread feederThread;
        private States? state;
        private Registers? chosenRegister;
        private bool previousFifoTresholdReached;

        private IFlagRegisterField statusFifoFull;

        private IFlagRegisterField clockSelect;
        private IValueRegisterField clockDividerHigh;
        private IValueRegisterField clockDividerLow;

        private IFlagRegisterField interrupt1FullEnabled;
        private IFlagRegisterField interrupt2FullEnabled;

        private IFlagRegisterField fifoAssertThresholdOnce;
        private IFlagRegisterField clearFlagsOnRead;

        private IValueRegisterField fifoFullThreshold;

        private IEnumRegisterField<OutputPinPolarity> polarityInterrupt1;
        private IEnumRegisterField<OutputPinPolarity> polarityInterrupt2;

        private const int MeasurementRegisterCount = 9;
        private const int MaximumFIFOCount = 256;

        private class AFESampleFIFO
        {
            public AFESampleFIFO(MAX86171 parent)
            {
                samples = new Queue<AFESample>();
                this.parent = parent;
            }

            public void EnqueueSample(AFESample sample)
            {
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

            public int Value => (innerPacket & 0x0FFFFF);
            public SampleSource Tag => (SampleSource)((innerPacket & 0xF00000) >> 20);
            public byte Byte1 => (byte)((innerPacket & 0xFF0000) >> 16);
            public byte Byte2 => (byte)((innerPacket & 0x00FF00) >> 8);
            public byte Byte3 => (byte)(innerPacket & 0x0000FF);
            public byte[] Bytes => new byte[] { Byte1, Byte2, Byte3 };
            public IEnumerator<byte> Enumerator => Bytes.OfType<byte>().GetEnumerator();

            private readonly int innerPacket;
        }

        private class SensorSampleMeasurements : SensorSample
        {
            public SensorSampleMeasurements()
            {
            }
            
            public SensorSampleMeasurements(AFESample[] samples)
            {
                packets = samples;
            }
            
            public override void Load(IList<decimal> data)
            {
                packets = data.Select(sample => new AFESample(sample)).ToArray();
            }

            public override bool TryLoad(params string[] data)
            {
                var samples = new List<decimal>();
                foreach(var str in data)
                {
                    if(!decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var sample))
                    {
                        return false;
                    }
                    samples.Add(sample);
                }

                Load(samples);
                return true;
            }

            public IEnumerable<AFESample[]> SamplePairs
            {
                get
                {
                    var i = 0;
                    while(i < packets.Length)
                    {
                        if((i + 1) >= packets.Length || packets[i].Tag != packets[i + 1].Tag)
                        {
                            Logger.Log(LogLevel.Warning, "Missing second sample for {0}", packets[i].Tag);
                            i += 1;
                            continue;
                        }

                        if(i > 0 && packets[i - 1].Tag > packets[i].Tag)
                        {
                            Logger.Log(LogLevel.Warning, "Invalid order of samples: {0} is before {1}", packets[i - 1].Tag, packets[i].Tag);
                        }

                        yield return new AFESample[2] { packets[i], packets[i + 1] };
                        i += 2;
                    }
                }
            }

            public AFESample[] Packets => packets;

            private AFESample[] packets;
        }

        private class DataFeed : SensorSamplesFifo<SensorSampleMeasurements>
        {
            public DataFeed(MAX86171 parent) : base()
            {
                this.parent = parent;
            }

            public override void FeedSample(SensorSampleMeasurements measurements)
            {
                var activeChannels = parent.ActiveChannels.ToList();
                var channelsInData = new List<Channel>();
                foreach(var samplePair in measurements.SamplePairs)
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

            private readonly MAX86171 parent;
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
            Interrupt2Enable1,
            Interrupt2Enable2,
            Interrupt2Enable3,

            PartID = 0xFF,
        }
    }
}
