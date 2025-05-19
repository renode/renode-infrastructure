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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    /// <summary>
    /// This model supports different ways to provide sensor samples. Setting one of those overrides the other method.
    /// 1. Explicit samples are fed when they are set manually, <see cref="Temperature"/> and <see cref="Humidity"/>.
    /// 2. RESD samples are fed when the RESD file is loaded, <see cref="FeedTemperatureSamplesFromRESD"/> and <see cref="FeedHumiditySamplesFromRESD"/>.
    /// </summary>
    public class HS3001 : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, ITemperatureSensor, IHumiditySensor
    {
        public HS3001(IMachine machine, ushort sensorIdHigh = 0x0, ushort sensorIdLow = 0x0)
        {
            this.machine = machine;
            this.registerWriteBuffer = new List<byte>();
            this.sensorIdHigh = sensorIdHigh;
            this.sensorIdLow = sensorIdLow;

            temperatureResolution = MeasurementResolution.Bits14;
            humidityResolution = MeasurementResolution.Bits14;

            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void FeedTemperatureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            resdTemperatureStream?.Dispose();
            resdTemperatureStream = this.CreateRESDStream<TemperatureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public void FeedHumiditySamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            resdHumidityStream?.Dispose();
            resdHumidityStream = this.CreateRESDStream<HumiditySample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public void Write(params byte[] data)
        {
            if(isReconfiguringSensor)
            {
                if(ExitReconfigurationSequence.SequenceEqual(data))
                {
                    isReconfiguringSensor = false;
                    currentRegister = null;
                    registerWriteBuffer.Clear();
                    this.DebugLog("Exiting sensor reconfiguration mode");
                    return;
                }

                registerWriteBuffer.AddRange(data);
                if(registerWriteBuffer.Count < RegisterAccessByteCount)
                {
                    return;
                }

                if(registerWriteBuffer.Count > RegisterAccessByteCount)
                {
                    this.WarningLog("Register write received more than {0} bytes. Only the first {0} bytes will be taken", RegisterAccessByteCount);
                }

                var registerWriteData = registerWriteBuffer.Take(RegisterAccessByteCount).ToArray();
                registerWriteBuffer.RemoveRange(0, RegisterAccessByteCount);

                if(currentRegister.HasValue)
                {
                    var value = BitHelper.ToUInt16(registerWriteData, 1, true);
                    this.DebugLog("Writing 0x{0:X} to register {1}", value, (Registers)registerWriteData[0]);
                    RegistersCollection.Write(registerWriteData[0], value);
                    currentRegister = null;
                }
                else
                {
                    // The rest of the data is intentionally dropped, as according to the datasheet
                    // (HS300x datasheet chapter 4.9) the remaining two bytes should be set to 0
                    currentRegister = (Registers)registerWriteData[0];

                    if(registerWriteData[1] != 0x0 || registerWriteData[2] != 0x0)
                    {
                        this.WarningLog("Expected the remaining bytes to be 0x0 when setting register address. Got 0x{0:X} and 0x{1:X} instead",
                            registerWriteData[1], registerWriteData[2]);
                    }
                }
            }
            else
            {
                if(EnterReconfigurationSequence.SequenceEqual(data))
                {
                    if(canReconfigureSensor)
                    {
                        isReconfiguringSensor = true;
                        this.DebugLog("Entering sensor reconfiguration mode");
                    }
                    else
                    {
                        this.WarningLog("Attempted to reconfigure sensor with the ability to reconfigure the sensor disabled");
                    }
                }
                else
                {
                    this.WarningLog("Unexpected write to the sensor: {0}. Ignoring", Misc.PrettyPrintCollectionHex(data));
                }
            }
        }

        public byte[] Read(int count = 1)
        {
            if(isReconfiguringSensor)
            {
                var registerReadBuffer = new byte[RegisterAccessByteCount];

                var registerToRead = currentRegister;
                currentRegister = null;

                if(!registerToRead.HasValue)
                {
                    this.ErrorLog("Register read address has not been set, returning default value");
                    registerReadBuffer[0] = RegisterReadFail;
                    return registerReadBuffer;
                }

                if(!RegistersCollection.TryRead((long)registerToRead, out var value))
                {
                    this.LogUnhandledRead((long)registerToRead);
                    registerReadBuffer[0] = RegisterReadFail;
                    return registerReadBuffer;
                }

                registerReadBuffer[0] = RegisterReadSuccess;
                BitHelper.GetBytesFromValue(registerReadBuffer, 1, value, 2);

                return registerReadBuffer;
            }

            // Convertions based on HS300x datasheet chapter 5
            var humidity = ConvertMeasurement(Humidity, humidityResolution, value => value * ((1 << 14) - 1) / 100);
            var temperature = ConvertMeasurement(Temperature, temperatureResolution, value => ((1 << 14) - 1) * (value + 40) / 165, shift: 2);

            var humidityBytes = BitHelper.GetBytesFromValue(humidity, 2, true);
            var temperatureBytes = BitHelper.GetBytesFromValue(temperature, 2, true);

            // Returns (HS300x datasheet chapter 4.6):
            // | status, humidity[14:8] | humidity[7:0] | temperature[15:8] | temperature[7:2], mask |
            var measurement = new byte[MeasurementByteCount] {
                humidityBytes[1], humidityBytes[0], temperatureBytes[1], temperatureBytes[0]
            };

            SetStatusBits(ref measurement[0], MeasurementStatus.Valid);
            return measurement;
        }

        public void FinishTransmission()
        {
            currentRegister = null;
            registerWriteBuffer.Clear();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            canReconfigureSensor = true;
            isReconfiguringSensor = false;
            registerWriteBuffer.Clear();
            // From the HS300x datasheet chapter 4.8. Sensor can only be placed into the programming mode
            // in the first 10ms after applying power to it
            machine.ScheduleAction(TimeInterval.FromMilliseconds(10), _ => canReconfigureSensor = false);
        }

        public WordRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get => GetSampleFromRESDStream(ref resdTemperatureStream, (sample) => sample.Temperature / 1000, temperature);
            set
            {
                resdTemperatureStream?.Dispose();
                resdTemperatureStream = null;
                temperature = value;
            }
        }

        public decimal Humidity
        {
            get => GetSampleFromRESDStream(ref resdHumidityStream, (sample) => sample.Humidity / 1000, humidity);
            set
            {
                resdHumidityStream?.Dispose();
                resdHumidityStream = null;
                humidity = value;
            }
        }

        private decimal GetSampleFromRESDStream<T>(ref RESDStream<T> stream, Func<T, decimal> transformer, decimal defaultValue)
            where T: RESDSample, new()
        {
            if(stream == null)
            {
                return defaultValue;
            }

            switch(stream.TryGetCurrentSample(this, transformer, out var sample, out _))
            {
            case RESDStreamStatus.OK:
                return sample;
            case RESDStreamStatus.BeforeStream:
                return defaultValue;
            case RESDStreamStatus.AfterStream:
                stream.Dispose();
                stream = null;
                return sample;
            default:
                throw new Exception("Unreachable");
            }
        }

        private ushort ConvertMeasurement(decimal value, MeasurementResolution resolution, Func<decimal, decimal> converter, int shift = 0)
        {
            var converted = converter(UpdateMeasurementResolution(resolution, value));
            var clamped = converted.Clamp(0, MaxMeasurementValue);
            return (ushort)(BitHelper.GetValue((uint)Math.Round(clamped), 0, MeasurementBits) << shift);
        }

        private void SetStatusBits(ref byte data, MeasurementStatus status)
        {
            var d = (uint)data;
            BitHelper.UpdateWith(ref d, (byte)status, 6, 2);
            data = (byte)d;
        }

        private decimal UpdateMeasurementResolution(MeasurementResolution resolution, decimal value)
        {
            if(value == 0)
            {
                return 0;
            }

            var valueCount = 1 << ResolutionToBitCount(resolution);
            var step = Math.Floor(valueCount / value);
            return step == 0 ? 0 : valueCount / step;
        }

        private byte ResolutionToBitCount(MeasurementResolution resolution)
        {
            switch(resolution)
            {
            case MeasurementResolution.Bits8:
                return 8;
            case MeasurementResolution.Bits10:
                return 10;
            case MeasurementResolution.Bits12:
                return 12;
            case MeasurementResolution.Bits14:
                return 14;
            default:
                throw new ArgumentException($"Invalid MeasurementResolution: {resolution}");
            }
        }

        private void DefineRegisters()
        {
            Registers.HumiditySensorResolutionRead.Define(this)
                .WithReservedBits(0, 10)
                .WithEnumField<WordRegister, MeasurementResolution>(10, 2, FieldMode.Read,
                    name: "Humidity Sensor Resolution Read",
                    valueProviderCallback: _ => humidityResolution)
                .WithReservedBits(12, 4);

            Registers.HumiditySensorResolutionWrite.Define(this)
                .WithReservedBits(0, 10)
                .WithEnumField<WordRegister, MeasurementResolution>(10, 2, FieldMode.Write,
                    name: "Humidity Sensor Resolution Write",
                    writeCallback: (_, value) => humidityResolution = value)
                .WithReservedBits(12, 4);

            Registers.TemperatureSensorResolutionRead.Define(this)
                .WithReservedBits(0, 10)
                .WithEnumField<WordRegister, MeasurementResolution>(10, 2, FieldMode.Read,
                    name: "Temperature Sensor Resolution Read",
                    valueProviderCallback: _ => temperatureResolution)
                .WithReservedBits(12, 4);

            Registers.TemperatureSensorResolutionWrite.Define(this)
                .WithReservedBits(0, 10)
                .WithEnumField<WordRegister, MeasurementResolution>(10, 2, FieldMode.Write,
                    name: "Temperature Sensor Resolution Write",
                    writeCallback: (_, value) => temperatureResolution = value)
                .WithReservedBits(12, 4);

            Registers.ReadSensorIDHigh.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "Sensor ID High",
                    valueProviderCallback: _ => sensorIdHigh);

            Registers.ReadSensorIDLow.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "Sensor ID Low",
                    valueProviderCallback: _ => sensorIdLow);
        }

        private readonly IMachine machine;
        private readonly List<byte> registerWriteBuffer;

        private readonly ushort sensorIdHigh;
        private readonly ushort sensorIdLow;

        private Registers? currentRegister;
        private RESDStream<TemperatureSample> resdTemperatureStream;
        private RESDStream<HumiditySample> resdHumidityStream;

        private bool canReconfigureSensor;
        private bool isReconfiguringSensor;

        private MeasurementResolution temperatureResolution;
        private MeasurementResolution humidityResolution;

        private decimal temperature;
        private decimal humidity;

        private const byte MeasurementBits = 14;
        private const ushort MaxMeasurementValue = (1 << MeasurementBits) - 1;
        private const byte RegisterAccessByteCount = 3;
        private const byte MeasurementByteCount = 4;
        private const byte RegisterReadSuccess = 0x81;
        // Documentation only specifies the success status code. The fail
        // code is defined under the assumption that any other value should
        // be considered a fail status
        private const byte RegisterReadFail = 0x0;

        private static readonly byte[] EnterReconfigurationSequence = new byte[]{ 0xA0, 0x00, 0x00 };
        private static readonly byte[] ExitReconfigurationSequence = new byte[]{ 0x80, 0x00, 0x00 };

        private enum Registers : byte
        {
            HumiditySensorResolutionRead        = 0x6,
            TemperatureSensorResolutionRead     = 0x11,
            ReadSensorIDHigh                    = 0x1E,
            ReadSensorIDLow                     = 0x1F,
            HumiditySensorResolutionWrite       = 0x46,
            TemperatureSensorResolutionWrite    = 0x51
        }

        private enum MeasurementStatus : byte
        {
            Valid = 0x0,
            Stale = 0x1,
        }

        private enum MeasurementResolution: byte
        {
            Bits8 = 0b00,
            Bits10 = 0b01,
            Bits12 = 0b10,
            Bits14 = 0b11,
        }
    }
}
