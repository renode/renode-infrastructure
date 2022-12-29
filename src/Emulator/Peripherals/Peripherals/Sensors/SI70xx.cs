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
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class SI70xx : II2CPeripheral, ITemperatureSensor, IHumiditySensor
    {
        public SI70xx(Model model)
        {
            this.model = model;
            commands = new I2CCommandManager<Action<byte[]>>();
            outputBuffer = new Queue<byte>();

            commands.RegisterCommand(MeasureHumidity, 0xE5);
            commands.RegisterCommand(MeasureHumidity, 0xF5);
            commands.RegisterCommand(MeasureTemperature, 0xE0);
            commands.RegisterCommand(MeasureTemperature, 0xE3);
            commands.RegisterCommand(MeasureTemperature, 0xF3);
            commands.RegisterCommand(ReadElectronicId1stByte, 0xFA, 0xF);
            commands.RegisterCommand(ResetOutputBuffer, 0xFE);
            commands.RegisterCommand(ReadElectronicId2ndByte, 0xFC, 0xC9);

            Reset();
        }

        public byte[] Read(int count = 1)
        {
            var result = outputBuffer.ToArray();
            this.Log(LogLevel.Noisy, "Reading {0} bytes from the device (asked for {1} bytes).", result.Length, count);
            outputBuffer.Clear();
            return result;
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Received {0} bytes: [{1}]", data.Length, string.Join(", ", data.Select(x => x.ToString())));
            if(!commands.TryGetCommand(data, out var command))
            {
                this.Log(LogLevel.Warning, "Unknown command: [{0}]. Ignoring the data.", string.Join(", ", data.Select(x => string.Format("0x{0:X}", x))));
                return;
            }
            command(data);
        }

        public void FinishTransmission()
        {
        }

        public void Reset()
        {
            Temperature = 0;
            Humidity = 0;
            outputBuffer.Clear();
        }

        public decimal Humidity
        {
            get
            {
                return (humidity * 125) / 65536 - 6;
            }
            set
            {
                if(MinHumidity > value || value > MaxHumidity)
                {
                    throw new RecoverableException("The humidity value must be between {0} and {1}.".FormatWith(MinHumidity, MaxHumidity));
                }
                humidity = (value + 6) * 65536 / 125;
            }
        }

        public decimal Temperature
        {
            get
            {
                return (temperature * 175.72m) / 65536 - 46.85m;
            }
            set
            {
                if(MinTemperature > value || value > MaxTemperature)
                {
                    throw new RecoverableException("The temperature value must be between {0} and {1}.".FormatWith(MinTemperature, MaxTemperature));
                }
                temperature = (value + 46.85m) * 65536 / 175.72m;
            }
        }

        private void MeasureHumidity(byte[] command)
        {
            outputBuffer.Enqueue((byte)((uint)humidity >> 8));
            outputBuffer.Enqueue((byte)((uint)humidity & 0xFF));
        }

        private void MeasureTemperature(byte[] command)
        {
            outputBuffer.Enqueue((byte)((uint)temperature >> 8));
            outputBuffer.Enqueue((byte)((uint)temperature & 0xFF));
        }

        private void ReadElectronicId1stByte(byte[] command)
        {
            // 1st: SNA_3
            // 2nd: CRC
            // 3rd: SNA_2
            // 4th: CRC
            // ...
            for(var i = 0; i < 8; i++)
            {
                outputBuffer.Enqueue(0x0);
            }
        }

        private void ReadElectronicId2ndByte(byte[] command)
        {
            // 1st: SNB_3
            // 2nd: SNB_2
            // 3rd: CRC
            // 4th: SNB_1
            // 5th: SNB_0
            // 6th: CRC
            outputBuffer.Enqueue((byte)model);
            for(var i = 0; i < 5; i++)
            {
                outputBuffer.Enqueue(0x0);
            }
        }

        private void ResetOutputBuffer(byte[] command)
        {
            outputBuffer.Clear();
        }

        private decimal humidity;
        private decimal temperature;

        private readonly Model model;
        private readonly I2CCommandManager<Action<byte[]>> commands;
        private readonly Queue<byte> outputBuffer;

        private const decimal MaxHumidity = 100;
        private const decimal MinHumidity = 0;
        private const decimal MaxTemperature = 85;
        private const decimal MinTemperature = -40;

        public enum Model
        {
            SI7021 = 0x15,
            SI7006 = 0x06
        }
    }
}
