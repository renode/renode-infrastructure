//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MAX30208 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor, IGPIOReceiver
    {
        public MAX30208(IMachine machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            this.machine = machine;

            samplesFifo = new Queue<TemperatureSampleWrapper>();

            GPIO0 = new GPIO();
            GPIO1 = new GPIO();

            DefineRegisters();
            UpdateInterrupts();
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            resdStream = this.CreateRESDStream<TemperatureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Error, "Unexpected write with no data");
                return;
            }

            registerAddress = (Registers)data[0];
            if(data.Length > 1)
            {
                foreach(var value in data.Skip(1))
                {
                    RegistersCollection.Write((byte)registerAddress, value);
                }
            }
        }

        public byte[] Read(int count)
        {
            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Error, "Trying to read without setting address");
                return new byte[] {};
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.Read((byte)((int)registerAddress));
            }
            return result;
        }

        public void OnGPIO(int pin, bool value)
        {
            if(pin != StartConversionPin)
            {
                this.Log(LogLevel.Warning, "Unexpected input on pin {0}; only pin 1 (GPIO1) is supported", pin);
                return;
            }

            // Interrupt1 (pin=1) is used as input to start temperature conversion
            if(gpio1Mode.Value == GPIOMode.IntConv && !value)
            {
                MeasureTemperature();
            }
        }

        public void FinishTransmission()
        {
            registerAddress = null;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            registerAddress = null;

            currentSampleEnumerator = null;
            alarmTemperatureLow = 0;
            alarmTemperatureHigh = 0;
            samplesFifo.Clear();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get
            {
                var currentSample = CurrentSample;
                return currentSample != null ? currentSample.Temperature / 1000m : defaultTemperature;
            }
            set => defaultTemperature = value;
        }

        public GPIO GPIO0 { get; }
        public GPIO GPIO1 { get; }

        private void UpdateInterrupts()
        {
            if(gpio0Mode.Value != GPIOMode.IntConv)
            {
                // If GPIO0 is not set as Interrupt, ignore
                return;
            }

            var interrupt = false;
            interrupt |= interruptTemperatureReady.Value && statusTemperatureReady.Value;
            interrupt |= interruptTemperatureHigh.Value && statusTemperatureHigh.Value;
            interrupt |= interruptTemperatureLow.Value && statusTemperatureLow.Value;
            interrupt |= interruptFifoThreshold.Value && statusFifoThreshold.Value;

            GPIO0.Set(!interrupt);
        }

        private void UpdateGPIOOutput()
        {
            var gpio0 = gpio0Mode.Value == GPIOMode.Output && gpio0Level.Value;
            var gpio1 = gpio1Mode.Value == GPIOMode.Output && gpio1Level.Value;

            if(gpio0Mode.Value != GPIOMode.IntConv)
            {
                GPIO0.Set(gpio0);
            }

            if(gpio1Mode.Value != GPIOMode.IntConv)
            {
                GPIO1.Set(gpio1);
            }
        }

        private void MeasureTemperature()
        {
            if(samplesFifo.Count == FIFOSize)
            {
                if(!fifoRollover.Value)
                {
                    // If FIFO Rollover is disabled, just ignore sample
                    return;
                }
                // Otherwise remove oldest item
                samplesFifo.Dequeue();
            }

            // Temperature in RESD is in milli-Celsius, so scale defaultTemperature accordingly
            var currentTemperature = CurrentSample?.Temperature ?? defaultTemperature / 0.001m;
            // Convert from 0.001C to 0.005C
            var convertedTemperature = (currentTemperature / 5).Clamp(short.MinValue, short.MaxValue);

            samplesFifo.Enqueue(new TemperatureSampleWrapper((short)convertedTemperature));

            if(alarmTemperatureHigh >= convertedTemperature)
            {
                statusTemperatureHigh.Value = true;
            }

            if(alarmTemperatureLow <= convertedTemperature)
            {
                statusTemperatureLow.Value = true;
            }

            if(fifoFullAssertOnThreshold.Value && (int)(fifoFullThreshold.Value + 1) == samplesFifo.Count)
            {
                statusFifoThreshold.Value = true;
            }
            else
            {
                statusFifoThreshold.Value = (int)fifoFullThreshold.Value >= samplesFifo.Count;
            }

            statusTemperatureReady.Value = true;
            UpdateInterrupts();
        }

        private byte DequeueSampleByte()
        {
            var output = default(byte);
            if(currentSampleEnumerator == null || !currentSampleEnumerator.TryGetNext(out output))
            {
                if(!samplesFifo.TryDequeue(out var sample))
                {
                    sample = new TemperatureSampleWrapper((short)(defaultTemperature / Sensitivity));
                }
                currentSampleEnumerator = sample.Enumerator;
                currentSampleEnumerator.TryGetNext(out output);
            }

            statusTemperatureReady.Value = false;
            if(clearFlagsOnRead.Value)
            {
                statusFifoThreshold.Value = false;
            }

            UpdateInterrupts();
            return output;
        }

        private void DefineRegisters()
        {
            Registers.Status.Define(this)
                .WithFlag(0, out statusTemperatureReady, FieldMode.ReadToClear, name: "STATUS.temp_rdy")
                .WithFlag(1, out statusTemperatureHigh, FieldMode.ReadToClear, name: "STATUS.temp_hi")
                .WithFlag(2, out statusTemperatureLow, FieldMode.ReadToClear, name: "STATUS.temp_lo")
                .WithReservedBits(3, 4)
                .WithFlag(7, out statusFifoThreshold, name: "STATUS.a_full")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptTemperatureReady, name: "INT_EN.temp_rdy")
                .WithFlag(1, out interruptTemperatureHigh, name: "INT_EN.temp_hi")
                .WithFlag(2, out interruptTemperatureLow, name: "INT_EN.temp_lo")
                .WithReservedBits(3, 4)
                .WithFlag(7, out interruptFifoThreshold, name: "INT_EN.a_full")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.FIFOWritePointer.Define(this)
                .WithTag("FIFO_WR_PTR.fifo_wr_ptr", 0, 5)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFOReadPointer.Define(this)
                .WithTag("FIFO_RD_PTR.fifo_rd_ptr", 0, 5)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFOOverflowCounter.Define(this)
                .WithTag("FIFO_OVF_COUNTER.ovf_counter", 0, 5)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFODataCounter.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "FIFO_DATA_COUNT.fifo_data_count",
                    valueProviderCallback: _ => (uint)samplesFifo.Count)
                .WithReservedBits(6, 2)
            ;

            Registers.FIFOData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "FIFO_DATA.fifo_data",
                    valueProviderCallback: _ => DequeueSampleByte())
            ;

            Registers.FIFOConfiguration1.Define(this, 0x0F)
                .WithValueField(0, 5, out fifoFullThreshold, name: "FIFO_CONF1.fifo_a_full")
                .WithReservedBits(5, 3)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.FIFOConfiguration2.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, out fifoRollover, name: "FIFO_CONF2.fifo_ro")
                .WithFlag(2, out fifoFullAssertOnThreshold, name: "FIFO_CONF2.a_full_type",
                    changeCallback: (_, value) =>
                    {
                        // If we disable 'on-threshold' trigger, update STATUS.a_full
                        // otherwise if we enable it, don't change anything.
                        if(!value)
                        {
                            statusFifoThreshold.Value |= (int)fifoFullThreshold.Value >= samplesFifo.Count;
                        }
                    })
                .WithFlag(3, out clearFlagsOnRead, name: "FIFO_CONF2.fifo_stat_clr")
                .WithFlag(4, FieldMode.WriteOneToClear, name: "FIFO_CONF2.flush_fifo",
                    writeCallback: (_, value) => { if(value) samplesFifo.Clear(); })
                .WithReservedBits(5, 3)
            ;

            Registers.SystemControl.Define(this)
                .WithFlag(0, FieldMode.Write, name: "SYSTEM_CTRL.reset",
                    writeCallback: (_, value) => { if(value) Reset(); })
            ;

            Registers.AlarmHighMSB.Define(this)
                .WithValueField(0, 8, name: "ALARM_HI_MSB.alarm_hi_msb",
                    valueProviderCallback: _ => alarmTemperatureHigh >> 8,
                    writeCallback: (_, value) => alarmTemperatureHigh = (alarmTemperatureHigh & 0xFF) | (uint)(value << 8))
            ;

            Registers.AlarmHighLSB.Define(this)
                .WithValueField(0, 8, name: "ALARM_HI_LSB.alarm_hi_lsb",
                    valueProviderCallback: _ => alarmTemperatureHigh,
                    writeCallback: (_, value) => alarmTemperatureHigh = (alarmTemperatureHigh & 0xFF00) | (uint)value)
            ;

            Registers.AlarmLowMSB.Define(this)
                .WithValueField(0, 8, name: "ALARM_LO_MSB.alarm_lo_msb",
                    valueProviderCallback: _ => alarmTemperatureLow >> 8,
                    writeCallback: (_, value) => alarmTemperatureLow = (alarmTemperatureLow & 0xFF) | (uint)(value << 8))
            ;

            Registers.AlarmLowLSB.Define(this)
                .WithValueField(0, 8, name: "ALARM_LO_LSB.alarm_lo_lsb",
                    writeCallback: (_, value) => alarmTemperatureLow = (alarmTemperatureLow & 0xFF00) | (uint)value)
            ;

            Registers.TempSensorSetup.Define(this, 0xC0)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "TEMP_SENS_SETUP.convert_t",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
			    if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                            var timeSource = machine.LocalTimeSource;
                            var now = timeSource.ElapsedVirtualTime;
                            // about 15ms (typ value)
                            var measurementFinishTime = now + TimeInterval.FromMilliseconds(15);
                            var measurementFinishTimeStamp = new TimeStamp(measurementFinishTime, timeSource.Domain);
                            timeSource.ExecuteInSyncedState(__ =>
                            {
                                MeasureTemperature();
                            }, measurementFinishTimeStamp);
                        }
                    })
                .WithReservedBits(1, 5)
                .WithTag("TEMP_SENS_SETUP.rfu", 6, 2)
            ;

            Registers.GPIOSetup.Define(this, 0x82)
                .WithEnumField<ByteRegister, GPIOMode>(0, 2, out gpio0Mode, name: "GPIO_SETUP.gpio0_mode")
                .WithReservedBits(2, 4)
                .WithEnumField<ByteRegister, GPIOMode>(6, 2, out gpio1Mode, name: "GPIO_SETUP.gpio1_mode")
                .WithChangeCallback((_, __) =>
                {
                    UpdateInterrupts();
                    UpdateGPIOOutput();
                })
            ;

            Registers.GPIOControl.Define(this)
                .WithFlag(0, out gpio0Level, name: "GPIO_CTRL.gpio0_ll")
                .WithReservedBits(1, 2)
                .WithFlag(3, out gpio1Level, name: "GPIO_CTRL.gpio1_ll")
                .WithReservedBits(4, 4)
                .WithChangeCallback((_, __) =>
                {
                    UpdateGPIOOutput();
                })
            ;

            Registers.PartIdentifier.Define(this, 0x30)
                .WithTag("PART_IDNET.data", 0, 8)
            ;
        }

        private TemperatureSample CurrentSample
        {
            get
            {
                if(resdStream == null)
                {
                    return null;
                }
                return resdStream.TryGetCurrentSample(this, out var sample, out _) == RESDStreamStatus.OK ? sample : null;
            }
        }

        private decimal defaultTemperature;

        private Registers? registerAddress;

        private uint alarmTemperatureLow;
        private uint alarmTemperatureHigh;

        private IEnumerator<byte> currentSampleEnumerator;
        private RESDStream<TemperatureSample> resdStream;

        private IFlagRegisterField statusTemperatureReady;
        private IFlagRegisterField statusTemperatureHigh;
        private IFlagRegisterField statusTemperatureLow;
        private IFlagRegisterField statusFifoThreshold;

        private IFlagRegisterField interruptTemperatureReady;
        private IFlagRegisterField interruptTemperatureHigh;
        private IFlagRegisterField interruptTemperatureLow;
        private IFlagRegisterField interruptFifoThreshold;

        private IFlagRegisterField fifoRollover;
        private IValueRegisterField fifoFullThreshold;
        private IFlagRegisterField clearFlagsOnRead;
        private IFlagRegisterField fifoFullAssertOnThreshold;

        private IEnumRegisterField<GPIOMode> gpio0Mode;
        private IEnumRegisterField<GPIOMode> gpio1Mode;

        private IFlagRegisterField gpio0Level;
        private IFlagRegisterField gpio1Level;

        private readonly IMachine machine;
        private readonly Queue<TemperatureSampleWrapper> samplesFifo;

        private const uint FIFOSize = 32;
        private const decimal Sensitivity = 0.005m;
        private const int StartConversionPin = 1;

        private struct TemperatureSampleWrapper
        {
            public TemperatureSampleWrapper(short data)
            {
                Value = data;
            }

            public short Value { get; }
            public byte Byte1 => (byte)(Value >> 8);
            public byte Byte2 => (byte)Value;
            public byte[] Bytes => new byte[] { Byte1, Byte2 };
            public IEnumerator<byte> Enumerator => Bytes.OfType<byte>().GetEnumerator();
        }

        private enum GPIOMode
        {
            Input,
            Output,
            PulldownInput,
            IntConv,
        }

        private enum Registers : byte
        {
            Status = 0x00,
            InterruptEnable = 0x01,

            FIFOWritePointer = 0x04,
            FIFOReadPointer = 0x05,
            FIFOOverflowCounter = 0x06,
            FIFODataCounter = 0x07,
            FIFOData = 0x08,
            FIFOConfiguration1 = 0x09,
            FIFOConfiguration2 = 0xA,

            SystemControl = 0x0C,

            AlarmHighMSB = 0x10,
            AlarmHighLSB = 0x11,
            AlarmLowMSB = 0x12,
            AlarmLowLSB = 0x13,
            TempSensorSetup = 0x14,

            GPIOSetup = 0x20,
            GPIOControl = 0x21,

            PartID1 = 0x31,
            PartID2 = 0x32,
            PartID3 = 0x33,
            PartID4 = 0x34,
            PartID5 = 0x35,
            PartID6 = 0x36,
            PartIdentifier = 0xFF,
        }
    }
}
