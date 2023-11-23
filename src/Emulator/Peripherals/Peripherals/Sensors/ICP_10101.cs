//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ICP_10101 : II2CPeripheral, ISensor
    {
        public ICP_10101()
        {
            crcEngine = new CRCEngine(0x31, 8, false, false, 0xFF, 0x00);
            writeHandlers = new Dictionary<Command, Action<byte[], int>>();
            readHandlers = new Dictionary<Command, Func<int, IEnumerable<byte>>>();
            DefineCommandHandlers();
        }

        public void Reset()
        {
            command = null;
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
            command = (Command)BitHelper.ToUInt16(data, 0, reverse: true);
            HandleWrite(data, 2);
        }

        public byte[] Read(int count = 1)
        {
            if(!command.HasValue)
            {
                this.Log(LogLevel.Error, "Attempted read, but Command not specified.");
            }
            return DataWithCrc(HandleRead(count)).Take(count).ToArray();
        }

        public void FinishTransmission()
        {
            command = null;
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
                    Reset();
                }
            );

            RegisterCommand(Command.Id,
                readHandler: _ => BitHelper.GetBytesFromValue(0b001000, 2, reverse: true)
            );

            RegisterCommand(Command.CalibrationParametersPointer,
                writeHandler: (data, offset) =>
                {
                    // TODO
                }
            );

            RegisterCommand(Command.CalibrationParameters,
                readHandler: _ =>
                {
                    // TODO
                    return Enumerable.Empty<byte>();
                }
            );
        }

        private void RegisterCommand(Command command, Action<byte[], int> writeHandler = null, Func<int, IEnumerable<byte>> readHandler = null)
        {
            if(writeHandlers.ContainsKey(command) || readHandlers.ContainsKey(command))
            {
                throw new RecoverableException("Attempted to register command twice");
            }

            writeHandlers.Add(command, writeHandler ?? ((data, offset) =>
            {
                if(offset != data.Length)
                {
                    this.Log(LogLevel.Warning, $"Attempted write while handling read-only command '{command}'.");
                }
            }));
            readHandlers.Add(command, readHandler ?? (count =>
            {
                if(count != 0)
                {
                    this.Log(LogLevel.Warning, $"Attempted read while handling write-only command '{command}'.");
                }
                return Enumerable.Empty<byte>();
            }));
        }

        private IEnumerable<byte> GetTemperatureBytes(OperationMode mode)
        {
            return BitHelper.GetBytesFromValue(GetTemperature(mode), 2, reverse: true);
        }

        private ushort GetTemperature(OperationMode mode)
        {
            return 0; // TODO
        }

        private IEnumerable<byte> GetPressureBytes(OperationMode mode)
        {
            return BitHelper.GetBytesFromValue(GetPressure(mode), 4, reverse: true);
        }

        private uint GetPressure(OperationMode mode)
        {
            return 0; // TODO
        }

        private ushort? GetCalibrationParameter(ulong pointer)
        {
            if(pointer < CalibrationParameterStart || CalibrationParameterStart + NumberOfCalibrationParameters <= pointer)
            {
                return null;
            }
            // TODO
            return 0x0;
        }

        private IEnumerable<byte> DataWithCrc(IEnumerable<byte> data)
        {
            var i = 0;
            var word = new byte[2];
            foreach(var b in data)
            {
                yield return b;
                word[i % 2] = b;
                if(i % 2 == 1)
                {
                    yield return (byte)crcEngine.Calculate(word);
                }
                i += 1;
            }
        }

        private void HandleWriteDefault(byte[] data, int offset)
        {
            this.Log(LogLevel.Warning, "Unhandled write, command (0x{0:X4}).", command);
        }

        private IEnumerable<byte> HandleReadDefault(int count)
        {
            this.Log(LogLevel.Warning, "Unhandled read, command (0x{0:X4}).", command);
            return Enumerable.Empty<byte>();
        }

        private Action<byte[], int> HandleWrite => writeHandlers.TryGetValue(command.Value, out var value) ? value : HandleWriteDefault;
        private Func<int, IEnumerable<byte>> HandleRead => readHandlers.TryGetValue(command.Value, out var value) ? value : HandleReadDefault;

        private Command? command;

        private readonly CRCEngine crcEngine;
        private readonly Dictionary<Command, Action<byte[], int>> writeHandlers;
        private readonly Dictionary<Command, Func<int, IEnumerable<byte>>> readHandlers;

        private const int NumberOfCalibrationParameters = 4;
        private const int CalibrationParameterStart = 0x00669C;

        public enum OperationMode
        {
            LowPower,
            Normal,
            LowNoise,
            UltraLowNoise,
        }

        public enum Command
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
