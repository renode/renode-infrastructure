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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ICP_101xx : II2CPeripheral, ISensor, ITemperatureSensor, IUnderstandRESD
    {
        public ICP_101xx(IMachine machine)
        {
            crcEngine = new CRCEngine(0x31, 8, false, false, 0xFF, 0x00);
            writeHandlers = new Dictionary<Command, Action<byte[], int>>();
            readHandlers = new Dictionary<Command, Func<int, IEnumerable<byte>>>();
            DefaultPressure = MinPressure;
            this.machine = machine;
            DefineCommandHandlers();
        }

        public void SoftwareReset()
        {
            command = null;
        }

        public void Reset()
        {
            SoftwareReset();

            temperatureResdStream?.Dispose();
            temperatureResdStream = null;

            pressureResdStream?.Dispose();
            pressureResdStream = null;
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Error, "Unexpected write with no data.");
                return;
            }
            if(data.Length < 2)
            {
                this.Log(LogLevel.Warning, "Malformed command.");
                return;
            }
            this.Log(LogLevel.Debug, "Received command: {0}", Misc.PrettyPrintCollectionHex(data));
            command = (Command)BitHelper.ToUInt16(data, 0, reverse: false);
            HandleWrite(data, 2);
        }

        public byte[] Read(int count = 1)
        {
            if(!command.HasValue)
            {
                this.Log(LogLevel.Error, "Attempted read, but Command not specified.");
                return Enumerable.Empty<byte>().ToArray();
            }
            // Concat to handle cases, where HandleRead could return 0 bytes
            var rets = InsertCrc(HandleRead(count)).Concat(new byte[]{ 0, 0 }).Take(count).ToArray();
            this.Log(LogLevel.Debug, "Reading data with CRC: {0}, current command: {1}, requested: {2} bytes", Misc.PrettyPrintCollectionHex(rets), GetCommandString(), count);
            return rets;
        }

        public void FeedTemperatureSamplesFromRESD(ReadFilePath path, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            temperatureResdStream?.Dispose();
            temperatureResdStream = this.CreateRESDStream<TemperatureSample>(path, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public void FeedPressureSamplesFromRESD(ReadFilePath path, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            pressureResdStream?.Dispose();
            pressureResdStream = this.CreateRESDStream<PressureSample>(path, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public void FinishTransmission()
        {
            // Don't reset state machine (current command) here - instead the software driver should use Soft Reset command
        }

        public void SetCalibrationValue(uint index, ushort value)
        {
            if(index >= CalibrationValues.Length)
            {
                throw new RecoverableException($"Index has to be in range 0 - {CalibrationValues.Length - 1}");
            }
            CalibrationValues[index] = value;
        }

        // Sensor calibration values, used when reading the pressure
        // This calibration values have been determined using statistical solution search with the following goals in mind:
        // * decrease probability that the raw pressure value returned from the sensor is negative
        // * make sure the raw pressure value fits in 3 bytes of return value
        // * minimize error margin between set pressure and reported raw pressure (post-conversion) as much as possible, taking into account temperature changes
        // in real HW these are definitely different, but for our use case they are sufficient
        public ushort[] CalibrationValues { get; private set; } = new ushort[4] { 620, 17540, 9475, 2945 };
        // Another good vectors:
        // { 15320, 15780, 32302, 3497 };
        // { 30261, -20852 /*44684*/, -24209 /*41327*/, 3107 };

        // Pressure is specified in Pascals
        public long DefaultPressure
        {
            get => defaultPressure;
            set
            {
                defaultPressure = value;
                ClampPressureAndLogWarning(ref defaultPressure);
            }
        }

        public long Pressure
        {
            get
            {
                UpdateCurrentPressureSample();
                ClampPressureAndLogWarning(ref pressure);
                return pressure;
            }
            set
            {
                throw new RecoverableException("Explicitly setting pressure is not supported by this model. " +
                $"Pressure should be provided from a RESD file or set via the '{nameof(DefaultPressure)}' property");
            }
        }

        // Temperature is specified in degrees Celsius
        public decimal DefaultTemperature
        {
            get => defaultTemperature;
            set
            {
                defaultTemperature = value;
                ClampTemperatureAndLogWarning(ref defaultTemperature);
            }
        }

        public decimal Temperature
        {
            get
            {
                UpdateCurrentTemperatureSample();
                ClampTemperatureAndLogWarning(ref temperature);
                return temperature;
            }
            set
            {
                throw new RecoverableException("Explicitly setting temperature is not supported by this model. " +
                $"Temperature should be provided from a RESD file or set via the '{nameof(DefaultTemperature)}' property");
            }
        }

        public const decimal MinTemperature = -45;
        public const decimal MaxTemperature = 85;

        public const uint MinPressure = 30000;
        public const uint MaxPressure = 110000;

        private void ClampPressureAndLogWarning(ref long pressure)
        {
            if(pressure < MinPressure || pressure > MaxPressure)
            {
                this.Log(LogLevel.Warning, "Pressure value: {0} is out of range and it will be clamped. Supported range: {1} - {2}", pressure, MinPressure, MaxPressure);
                pressure = pressure.Clamp(MinPressure, MaxPressure);
            }
        }

        private void ClampTemperatureAndLogWarning(ref decimal temperature)
        {
            if(temperature < MinTemperature || temperature > MaxTemperature)
            {
                this.Log(LogLevel.Warning, "Temperature value: {0} is out of range and it will be clamped. Supported range: {1} - {2}", temperature, MinTemperature, MaxTemperature);
                temperature = temperature.Clamp(MinTemperature, MaxTemperature);
            }
        }

        private void DefineCommandHandlers()
        {
            RegisterCommand(Command.LowPowerTemperaturePressure,
                readHandler: _ => Enumerable.Concat(
                    GetTemperatureBytes(OperationMode.LowPower),
                    GetPressureBytes(OperationMode.LowPower)
                )
            );

            RegisterCommand(Command.LowPowerPressureTemperature,
                readHandler: _ => Enumerable.Concat(
                    GetPressureBytes(OperationMode.LowPower),
                    GetTemperatureBytes(OperationMode.LowPower)
                )
            );

            RegisterCommand(Command.NormalTemperaturePressure,
                readHandler: _ => Enumerable.Concat(
                    GetTemperatureBytes(OperationMode.Normal),
                    GetPressureBytes(OperationMode.Normal)
                )
            );

            RegisterCommand(Command.NormalPressureTemperature,
                readHandler: _ => Enumerable.Concat(
                    GetPressureBytes(OperationMode.Normal),
                    GetTemperatureBytes(OperationMode.Normal)
                )
            );

            RegisterCommand(Command.LowNoiseTemperaturePressure,
                readHandler: _ => Enumerable.Concat(
                    GetTemperatureBytes(OperationMode.LowNoise),
                    GetPressureBytes(OperationMode.LowNoise)
                )
            );

            RegisterCommand(Command.LowNoisePressureTemperature,
                readHandler: _ => Enumerable.Concat(
                    GetPressureBytes(OperationMode.LowNoise),
                    GetTemperatureBytes(OperationMode.LowNoise)
                )
            );

            RegisterCommand(Command.UltraLowNoiseTemperaturePressure,
                readHandler: _ => Enumerable.Concat(
                    GetTemperatureBytes(OperationMode.UltraLowNoise),
                    GetPressureBytes(OperationMode.UltraLowNoise)
                )
            );

            RegisterCommand(Command.UltraLowNoisePressureTemperature,
                readHandler: _ => Enumerable.Concat(
                    GetPressureBytes(OperationMode.UltraLowNoise),
                    GetTemperatureBytes(OperationMode.UltraLowNoise)
                )
            );

            RegisterCommand(Command.SoftReset,
                writeHandler: (_, __) =>
                {
                    SoftwareReset();
                }
            );

            RegisterCommand(Command.Id,
                readHandler: _ => BitHelper.GetBytesFromValue(0b001000, 2, reverse: false) // It's already in the correct byte order
            );

            RegisterCommand(Command.CalibrationParametersPointer,
                writeHandler: (data, offset) =>
                {
                    if(data.Length <= offset + 2)
                    {
                        this.Log(LogLevel.Error, "Read from OTP received not enough bytes, got: {0}, total of: {0} are expected", data.Length, offset + 3);
                        return;
                    }
                    var addr = data[offset] << 16 | data[offset + 1] << 8 | data[offset + 2];
                    if(addr != CalibrationAddress)
                    {
                        this.Log(LogLevel.Error, "Read from OTP from offset 0x{0:X}, different than 0x{1:X} is unsupported!", addr, CalibrationAddress);
                        return;
                    }
                    // Just reset the current calibration value number, as there is no data 
                    // other than calibration values to be read from the sensor memory
                    // and the docs don't mention another usage for this command at this moment (rev 1.2)
                    calibrationValueIndex = 0;
                }
            );

            RegisterCommand(Command.CalibrationParameters,
                readHandler: _ =>
                {
                    ushort rets = CalibrationValues[calibrationValueIndex];
                    calibrationValueIndex = (calibrationValueIndex + 1) % CalibrationValues.Length;
                    return BitHelper.GetBytesFromValue(rets, 2, reverse: false);
                }
            );
        }

        private void RegisterCommand(Command command, Action<byte[], int> writeHandler = null, Func<int, IEnumerable<byte>> readHandler = null)
        {
            if(writeHandlers.ContainsKey(command) || readHandlers.ContainsKey(command))
            {
                throw new RecoverableException("Attempted to register command twice");
            }

            this.Log(LogLevel.Noisy, "Registered command 0x{0:X}.", command);

            writeHandlers.Add(command, writeHandler ?? ((data, offset) =>
            {
                if(offset != data.Length)
                {
                    this.Log(LogLevel.Warning, "Attempted write while handling read-only command '{0}'.", command);
                }
            }));
            readHandlers.Add(command, readHandler ?? (count =>
            {
                if(count != 0)
                {
                    this.Log(LogLevel.Warning, "Attempted read while handling write-only command '{0}'.", command);
                }
                return Enumerable.Empty<byte>();
            }));
        }

        private void UpdateCurrentTemperatureSample()
        {
            if(TryGetSampleFromRESDStream<TemperatureSample>(temperatureResdStream, out var sample))
            {
                temperature = (decimal)sample.Temperature / 1e3m;
            }
            else
            {
                temperature = DefaultTemperature;
            }
        }

        private void UpdateCurrentPressureSample()
        {
            if(TryGetSampleFromRESDStream<PressureSample>(pressureResdStream, out var sample))
            {
                pressure = (long)(sample.Pressure / 1000);
            }
            else
            {
                pressure = DefaultPressure;
            }
        }

        private bool TryGetSampleFromRESDStream<T>(RESDStream<T> stream, out T sample) where T : RESDSample, new()
        {
            sample = null;
            if(stream == null)
            {
                return false;
            }

            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }

            var currentTimestamp = machine.ClockSource.CurrentValue.TotalNanoseconds;
            if(stream.TryGetSample(currentTimestamp, out sample) != RESDStreamStatus.BeforeStream)
            {
                return true;
            }
            return false;
        }

        private IEnumerable<byte> GetTemperatureBytes(OperationMode mode)
        {
            return BitHelper.GetBytesFromValue(GetTemperature(mode), 2, reverse: false);
        }

        private ushort GetTemperature(OperationMode _)
        {
            return (ushort)((Temperature + 45) * (1 << 16) / 175);
        }

        private uint GetPressure(OperationMode mode)
        {
            GetCoefficients(mode, out long A, out long B, out long C);

            var decimalPressure = (B / (Pressure - A)) - C;
            // Detect and report that the sensor might be miscalibrated
            if(decimalPressure < 0 || decimalPressure > 0xFFFFFF)
            {
                this.Log(LogLevel.Warning, "Raw pressure value is invalid: {0}. The sensor might be miscalibrated.", decimalPressure);
            }
            uint rawPressure = (uint)decimalPressure;

            this.Log(LogLevel.Noisy, "p_raw={0}, p_long={1}", rawPressure, decimalPressure);
            return rawPressure;
        }

        private IEnumerable<byte> GetPressureBytes(OperationMode mode)
        {
            // Shift value left, since LLSB is discarded (has no meaning in pressure calculation)
            return BitHelper.GetBytesFromValue((uint)(GetPressure(mode) << 8), 4, reverse: false);
        }

        private void GetCoefficients(OperationMode mode, out long A, out long B, out long C)
        {
            long t = GetTemperature(mode) - 32768;
            long p_LUT0 = LUT_lower + (CalibrationValues[0] * t * t) / InverseQuadraticFactor;
            long p_LUT1 = OffsetFactor * CalibrationValues[3] + (CalibrationValues[1] * t * t) / InverseQuadraticFactor;
            long p_LUT2 = LUT_upper + (CalibrationValues[2] * t * t) / InverseQuadraticFactor;
            
            C = (p_LUT0 * p_LUT1 * (p_Pa[0] - p_Pa[1]) +
                 p_LUT1 * p_LUT2 * (p_Pa[1] - p_Pa[2]) +
                 p_LUT2 * p_LUT0 * (p_Pa[2] - p_Pa[0])) /
                (p_LUT2 * (p_Pa[0] - p_Pa[1]) +
                 p_LUT0 * (p_Pa[1] - p_Pa[2]) +
                 p_LUT1 * (p_Pa[2] - p_Pa[0]));

            A = (p_Pa[0] * p_LUT0 - p_Pa[1] * p_LUT1 - (p_Pa[1] - p_Pa[0]) * C) / (p_LUT0 - p_LUT1);

            B = (p_Pa[0] - A) * (p_LUT0 + C);

            this.Log(LogLevel.Noisy, "A={0} B={1} C={2}, p_LUT0={3}, p_LUT1={4}, p_LUT2={5}", A, B, C, p_LUT0, p_LUT1, p_LUT2);
        }

        private IEnumerable<byte> InsertCrc(IEnumerable<byte> data)
        {
            var wordsNumber = 0;
            var word = new byte[2];
            foreach(var b in data)
            {
                yield return b;
                word[wordsNumber % 2] = b;
                if(wordsNumber % 2 == 1)
                {
                    yield return (byte)crcEngine.Calculate(word);
                }
                ++wordsNumber;
            }
            if(wordsNumber % 2 == 1)
            {
                throw new InvalidOperationException("Data length has to be multiple of 2 bytes");
            }
        }

        private string GetCommandString() => command != null ? $"0x{command:X} ({command})" : "None";

        private void HandleWriteDefault(byte[] data, int offset)
        {
            this.Log(LogLevel.Warning, "Unhandled write, command: {0}.", GetCommandString());
        }

        private IEnumerable<byte> HandleReadDefault(int count)
        {
            this.Log(LogLevel.Warning, "Unhandled read, command: {0}.", GetCommandString());
            return Enumerable.Empty<byte>();
        }

        private Action<byte[], int> HandleWrite => writeHandlers.TryGetValue(command.Value, out var handler) ? handler : HandleWriteDefault;
        private Func<int, IEnumerable<byte>> HandleRead => readHandlers.TryGetValue(command.Value, out var handler) ? handler : HandleReadDefault;

        private Command? command;

        private long defaultPressure;
        private long pressure;
        private decimal defaultTemperature;
        private decimal temperature;

        private readonly CRCEngine crcEngine;
        private readonly IMachine machine;
        private readonly Dictionary<Command, Action<byte[], int>> writeHandlers;
        private readonly Dictionary<Command, Func<int, IEnumerable<byte>>> readHandlers;

        // Which calibration value is read in incremental read-from OTP
        private int calibrationValueIndex = 0;

        private RESDStream<TemperatureSample> temperatureResdStream;
        private RESDStream<PressureSample> pressureResdStream;

        // Configuration constants, taken directly from the datasheet (https://invensense.tdk.com/wp-content/uploads/2021/06/DS-000408-ICP-10101-v1.2.pdf)
        private readonly int[] p_Pa = new int [] { 45000, 80000, 105000 };
        private const int LUT_lower = 3670016; //3.5 * (1 << 20);
        private const int LUT_upper = 12058624; //11.5 * (1 << 20);
        private const int InverseQuadraticFactor = 16777216;
        private const int OffsetFactor = 2048;

        private const int CalibrationAddress = 0x00669C;

        public enum OperationMode
        {
            LowPower,
            Normal,
            LowNoise,
            UltraLowNoise,
        }

        public enum Command : ushort
        {
            LowPowerTemperaturePressure = 0x609C,
            LowPowerPressureTemperature = 0x401A,
            NormalTemperaturePressure = 0x6825,
            NormalPressureTemperature = 0x48A3,
            LowNoiseTemperaturePressure = 0x70DF,
            LowNoisePressureTemperature = 0x5059,
            UltraLowNoiseTemperaturePressure = 0x7866,
            UltraLowNoisePressureTemperature = 0x58E0,
            SoftReset = 0x805D,
            Id = 0xEFC8,
            CalibrationParametersPointer = 0xC595,
            CalibrationParameters = 0xC7F7,
        }
    }
}
