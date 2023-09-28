//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class AS6221 : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, ITemperatureSensor
    {
        public AS6221(IMachine machine)
        {
            RegistersCollection = new WordRegisterCollection(this);

            this.machine = machine;

            DefineRegisters();

            conversionTimer = new LimitTimer(machine.ClockSource, 1, this, "conv", autoUpdate: true, eventEnabled: true, enabled: true);
            conversionTimer.LimitReached += HandleConversion;

            Reset();
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
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            var offset = 0;
            if(state == States.Read)
            {
                this.Log(LogLevel.Warning, "Trying to write while in read mode; ignoring");
                return;
            }
            else if(state == States.Idle)
            {
                registerAddress = (Registers)data[0];
                offset = 1;
                state = data.Length == 1 ? States.Read : States.Write;
            }

            if(data.Length > offset)
            {
                foreach(var item in data.Skip(offset).Select((value, index) => new { index, value }))
                {
                    RegistersCollection.WriteWithOffset((long)registerAddress.Value, item.index, item.value, msbFirst: true);
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

            if(state != States.Read)
            {
                this.Log(LogLevel.Error, "Trying to read while in write mode");
                return new byte[] {};
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.ReadWithOffset((long)registerAddress.Value, i, msbFirst: true);
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = null;
            state = States.Idle;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            conversionTimer.Reset();

            registerAddress = null;
            state = States.Idle;

            currentTemperature = 0;
            temperatureLowThreshold = DefaultLowThreshold;
            temperatureHighThreshold = DefaultHighThreshold;

            UpdateConversionFrequency();
        }

        public WordRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get => (decimal)currentTemperature / (decimal)Resolution;
            set
            {
                if(value > MaximumTemperature)
                {
                    currentTemperature = short.MaxValue;
                    this.Log(LogLevel.Warning, "{0} is higher than maximum of {1} and has been clamped", value, MaximumTemperature);
                }
                else if(value < MinimumTemperature)
                {
                    currentTemperature = short.MinValue;
                    this.Log(LogLevel.Warning, "{0} is lower than minimum of {1} and has been clamped", value, MinimumTemperature);
                }
                else
                {
                    currentTemperature = (short)(value * (decimal)Resolution);
                }
            }
        }

        private void UpdateConversionFrequency()
        {
            var frequencyAndLimit = ConversionRateToFrequencyAndLimit(conversionRate.Value);
            conversionTimer.Frequency = frequencyAndLimit.Item1;
            conversionTimer.Limit = frequencyAndLimit.Item2;
        }

        private void HandleConversion()
        {
            if(resdStream != null)
            {
                switch(resdStream.TryGetCurrentSample(this, (sample) => sample.Temperature / 1000m, out var temperature, out _))
                {
                    case RESDStreamStatus.OK:
                        Temperature = temperature;
                        break;
                    case RESDStreamStatus.BeforeStream:
                        // Just ignore and return previously set value
                        break;
                    case RESDStreamStatus.AfterStream:
                        // No more samples in file
                        resdStream.Dispose();
                        resdStream = null;
                        break;
                }
            }

            if(interruptMode.Value)
            {
                alarmFlag =
                    currentTemperature > temperatureHighThreshold || currentTemperature < temperatureLowThreshold;
            }
            else
            {
                if(consecutiveFaults < consecutiveFaultsThreshold)
                {
                    consecutiveFaults++;
                }
                alarmFlag = consecutiveFaults == consecutiveFaultsThreshold;
            }
        }

        private void DefineRegisters()
        {
            Registers.Temperature.Define(this)
                .WithValueField(0, 16, name: "TEMP.temp",
                    valueProviderCallback: _ => (uint)currentTemperature,
                    writeCallback: (_, value) => currentTemperature = (short)value)
            ;

            Registers.Configuration.Define(this, 0xA0)
                .WithReservedBits(0, 5)
                .WithFlag(5, FieldMode.Read, name: "CONF.alarm",
                    valueProviderCallback: _ => alarmFlag ? alarmPolarity.Value : !alarmPolarity.Value)
                .WithEnumField<WordRegister, ConversionRate>(6, 2, out conversionRate, name: "CONF.conv_rate",
                    changeCallback: (_, __) => UpdateConversionFrequency())
                .WithTaggedFlag("CONF.sleep_mode", 8)
                .WithFlag(9, out interruptMode, name: "CONF.interrupt_mode",
                    changeCallback: (_, value) => consecutiveFaults = 0)
                .WithFlag(10, out alarmPolarity, name: "CONF.polarity")
                .WithValueField(11, 2, name: "CONF.consecutive_faults",
                    valueProviderCallback: _ => consecutiveFaultsThreshold - 1,
                    writeCallback: (_, value) =>
                    {
                        consecutiveFaultsThreshold = (uint)value + 1;
                        consecutiveFaults = 0;
                    })
                .WithReservedBits(13, 2)
                .WithTaggedFlag("CONF.single_shot", 15)
            ;

            Registers.TemperatureLow.Define(this)
                .WithValueField(0, 16, name: "TLOW.tlow",
                    valueProviderCallback: _ => (uint)temperatureLowThreshold,
                    writeCallback: (_, value) =>
                    {
                        temperatureLowThreshold = (short)value;
                        consecutiveFaults = 0;
                        alarmFlag = false;
                    })
            ;

            Registers.TemperatureHigh.Define(this)
                .WithValueField(0, 16, name: "THIGH.thigh",
                    valueProviderCallback: _ => (uint)temperatureHighThreshold,
                    writeCallback: (_, value) =>
                    {
                        temperatureHighThreshold = (short)value;
                        consecutiveFaults = 0;
                        alarmFlag = false;
                    })
            ;
        }

        private static Tuple<int, ulong> ConversionRateToFrequencyAndLimit(ConversionRate cr)
        {
            switch(cr)
            {
                case ConversionRate.Quarter:
                    return new Tuple<int, ulong>(1, 4);
                case ConversionRate.One:
                    return new Tuple<int, ulong>(1, 1);
                case ConversionRate.Four:
                    return new Tuple<int, ulong>(4, 1);
                case ConversionRate.Eight:
                    return new Tuple<int, ulong>(8, 1);
                default:
                    throw new Exception("unreachable code");
            }
        }

        private Registers? registerAddress;
        private States state;
        private RESDStream<TemperatureSample> resdStream;

        private IEnumRegisterField<ConversionRate> conversionRate;

        private IFlagRegisterField alarmPolarity;
        private IFlagRegisterField interruptMode;

        private bool alarmFlag;
        private short currentTemperature;
        private short temperatureLowThreshold;
        private short temperatureHighThreshold;
        private uint consecutiveFaultsThreshold;

        private uint consecutiveFaults;

        private const short Resolution = 0x80;
        private const short DefaultLowThreshold = 70 * Resolution;
        private const short DefaultHighThreshold = 80 * Resolution;
        private const decimal MaximumTemperature = (decimal)short.MaxValue / (decimal)Resolution;
        private const decimal MinimumTemperature = (decimal)short.MinValue / (decimal)Resolution;

        private readonly IMachine machine;
        private readonly LimitTimer conversionTimer;

        private enum States
        {
            Idle,
            Write,
            Read,
        }

        private enum ConversionRate
        {
            Quarter,
            One,
            Four,
            Eight,
        }

        private enum Registers : byte
        {
            Temperature = 0x00,
            Configuration = 0x01,
            TemperatureLow = 0x02,
            TemperatureHigh = 0x03,
        }
    }
}
