//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class SHT45 : II2CPeripheral
    {
        public SHT45()
        {
            crc = new CRCEngine(0x31, 8, false, false, 0xFF);
            Reset();
        }

        public void Reset()
        {
            message = new byte[6];
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Write {0}", data.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
            if(data.Length == 0)
            {
               return;
            }
            if(data.Length > 1)
            {
                this.Log(LogLevel.Warning, "Write too long ({0} bytes, expected 1)", data.Length);
            }

            Registers register = (Registers)data[0];
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
                    EncodeMeasurementMessage();
                    break;
                case Registers.ReadSerialNumber:
                    EncodeSerialNumberMessage();
                    break;
                case Registers.SoftReset:
                    Reset();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Invalid register {0}", register);
                    break;
            }
        }

        public byte[] Read(int count = 0)
        {
            if(count > message.Length)
            {
                this.Log(LogLevel.Warning, "Trying to read too many bytes ({0} bytes, available {1})", count, message.Length);
            }
            byte[] buf = new byte[count];
            Array.Copy(message, buf, count);
            this.Log(LogLevel.Noisy, "Read {0}", buf.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
            return buf;
        }

        //we are required to implement this method, but in case of this device there is nothing we want to do here
        public void FinishTransmission()
        {
        }

        public uint SerialNumber { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }

        private void EncodeSerialNumberMessage()
        {
            message[0] = (byte)((SerialNumber >> 24));
            message[1] = (byte)((SerialNumber >> 16));
            message[2] = (byte)crc.Calculate(new ArraySegment<byte>(message, 0, 2));
            message[3] = (byte)((SerialNumber >> 8));
            message[4] = (byte)(SerialNumber);
            message[5] = (byte)crc.Calculate(new ArraySegment<byte>(message, 3, 2));
        }

        private void EncodeMeasurementMessage()
        {
            var temp = EncodeTemperature(Temperature);
            byte tempCrc = (byte)crc.Calculate(temp);

            var rh = EncodeHumidity(Humidity);
            byte rhCrc = (byte)crc.Calculate(rh);

            temp.CopyTo(message, 0);
            message[2] = tempCrc;
            rh.CopyTo(message, 3);
            message[5] = rhCrc;
        }

        private byte[] EncodeTemperature(double temperature)
        {
            double st = (temperature + 45) * 65535.0 / 175.0;
            ushort stU16 = (ushort)Math.Round(st);
            byte stLo = (byte)(stU16);
            byte stHi = (byte)((stU16 >> 8));

            return new byte[2] {stHi, stLo};
        }

        private byte[] EncodeHumidity(double humidity)
        {
            double Srh = (humidity + 6) * 65535.0 / 125.0;
            UInt16 SrhU16 = Convert.ToUInt16(Math.Round(Srh));
            byte SrhLo = (byte)(SrhU16);
            byte SrhHi = (byte)((SrhU16 >> 8));

            return new byte[2] {SrhHi, SrhLo};
        }

        private byte[] message;
        readonly private CRCEngine crc;

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

