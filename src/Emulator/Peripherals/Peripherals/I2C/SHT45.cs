//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class SHT45 : II2CPeripheral, ITemperatureSensor
    {
        public SHT45()
        {
            crcEngine = new CRCEngine(0x31, 8, false, false, 0xFF);
            Reset();
        }

        public void Reset()
        {
            readBuffer = new byte[6];
        }

        public void Write(byte[] data)
        {
            this.NoisyLog("Written {0}", Misc.PrettyPrintCollectionHex(data));
            if(data.Length == 0)
            {
                return;
            }
            if(data.Length > 1)
            {
                this.WarningLog("Write too long ({0} bytes, expected 1)", data.Length);
            }

            var register = (Registers)data[0];
            switch(register)
            {
            case Registers.MeasureHighPrecision:
            case Registers.MeasureMediumPrecision:
            case Registers.MeasureLowPrecision:
            case Registers.MeasureWithHeater200mw1s:
            case Registers.MeasureWithHeater200mw01s:
            case Registers.MeasureWithHeater110mw1s:
            case Registers.MeasureWithHeater110mw01s:
            case Registers.MeasureWithHeater20mw1s:
            case Registers.MeasureWithHeater20mw01s:
                UpdateReadBuffer(EncodeMeasurementMessage());
                break;
            case Registers.ReadSerialNumber:
                UpdateReadBuffer(EncodeSerialNumberMessage());
                break;
            case Registers.SoftReset:
                Reset();
                break;
            default:
                this.WarningLog("Invalid register {0}", register);
                break;
            }
        }

        public byte[] Read(int count = 0)
        {
            if(count > readBuffer.Length)
            {
                this.WarningLog("Trying to read too many bytes ({0} bytes, available {1})", count, readBuffer.Length);
            }
            var result = new byte[count];
            Array.Copy(readBuffer, result, count);
            this.NoisyLog("Read {0} bytes: {1}", count, Misc.PrettyPrintCollectionHex(result));
            return result;
        }

        //we are required to implement this method, but in case of this device there is nothing we want to do here
        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission");
        }

        public uint SerialNumber { get; set; }

        public decimal Temperature { get; set; }

        public double Humidity { get; set; }

        private byte[] EncodeSerialNumberMessage()
        {
            var serialNumberBuffer = new byte[6];

            serialNumberBuffer[0] = (byte)((SerialNumber >> 24));
            serialNumberBuffer[1] = (byte)((SerialNumber >> 16));
            serialNumberBuffer[2] = (byte)crcEngine.Calculate(new ArraySegment<byte>(serialNumberBuffer, 0, 2));
            serialNumberBuffer[3] = (byte)((SerialNumber >> 8));
            serialNumberBuffer[4] = (byte)(SerialNumber);
            serialNumberBuffer[5] = (byte)crcEngine.Calculate(new ArraySegment<byte>(serialNumberBuffer, 3, 2));

            return serialNumberBuffer;
        }

        private byte[] EncodeMeasurementMessage()
        {
            var mesurementBuffer = new byte[6];

            var temperatureBytes = EncodeTemperature(Temperature);
            var temperatureCrc = (byte)crcEngine.Calculate(temperatureBytes);

            var relativeHumidity = EncodeHumidity(Humidity);
            var relativeHumidityCrc = (byte)crcEngine.Calculate(relativeHumidity);

            temperatureBytes.CopyTo(mesurementBuffer, 0);
            mesurementBuffer[2] = temperatureCrc;
            relativeHumidity.CopyTo(mesurementBuffer, 3);
            mesurementBuffer[5] = relativeHumidityCrc;

            return mesurementBuffer;
        }

        private byte[] EncodeTemperature(decimal temperature)
        {
            var temperatureSignal = (temperature + 45) * 65535.0m / 175.0m;
            var temperatureSignalU16 = (ushort)Math.Round(temperatureSignal);
            var temperatureSignalLow = (byte)(temperatureSignalU16);
            var temperatureSignalHi = (byte)((temperatureSignalU16 >> 8));

            return new byte[2] { temperatureSignalHi, temperatureSignalLow };
        }

        private byte[] EncodeHumidity(double humidity)
        {
            var relativeHumiditySignal = (humidity + 6) * 65535.0 / 125.0;
            var relativeHumiditySignalU16 = Convert.ToUInt16(Math.Round(relativeHumiditySignal));
            var relativeHumiditySignalLow = (byte)(relativeHumiditySignalU16);
            var relativeHumiditySignalHi = (byte)((relativeHumiditySignalU16 >> 8));

            return new byte[2] { relativeHumiditySignalHi, relativeHumiditySignalLow };
        }

        private void UpdateReadBuffer(byte[] newReadBuffer)
        {
            readBuffer = newReadBuffer;
        }

        private byte[] readBuffer;
        private readonly CRCEngine crcEngine;

        private enum Registers
        {
            MeasureWithHeater20mw01s = 0x15,
            MeasureWithHeater20mw1s = 0x1E,
            MeasureWithHeater110mw01s = 0x24,
            MeasureWithHeater110mw1s = 0x2F,
            MeasureWithHeater200mw01s = 0x32,
            MeasureWithHeater200mw1s = 0x39,
            ReadSerialNumber = 0x89,
            SoftReset = 0x94,
            MeasureLowPrecision = 0xE0,
            MeasureMediumPrecision = 0xF6,
            MeasureHighPrecision = 0xFD
        }
    }
}
