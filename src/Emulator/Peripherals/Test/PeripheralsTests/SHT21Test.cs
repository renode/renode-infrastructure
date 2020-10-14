//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.I2C;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class SHT21Test
    {
        [Test]
        public void InitTest()
        {
            List<byte> packet = new List<byte>();
            var sht21 = new SHT21();
            sht21.Reset();
            // Check the serial number in on-chip memory 1 
            // TODO: not fully implemented, now returns zeros 
            packet.Add((byte)0xFC);
            packet.Add((byte)0xFA);
            packet.Add((byte)0x0F);
            sht21.Write(packet.ToArray());
            packet.Clear();
            byte[] serialNr = sht21.Read();
            Assert.AreEqual(serialNr[0], 0x0);
            Assert.AreEqual(serialNr[1], 0x0);
            Assert.AreEqual(serialNr[2], 0x0);
            Assert.AreEqual(serialNr[3], 0x0);
            Assert.AreEqual(serialNr[4], 0x0);
            Assert.AreEqual(serialNr[5], 0x0);
            Assert.AreEqual(serialNr[6], 0x0);
            Assert.AreEqual(serialNr[7], 0x0);
            Assert.AreEqual(serialNr[8], 0x0);
            // Check the serial number in on-chip memory 2 
            // TODO: not fully implemented, now returns zeros 
            packet.Add((byte)0xFD);
            packet.Add((byte)0xFA);
            packet.Add((byte)0xC9);
            sht21.Write(packet.ToArray());
            packet.Clear();
            serialNr = sht21.Read();
            Assert.AreEqual(serialNr[0], 0x0);
            Assert.AreEqual(serialNr[1], 0x0);
            Assert.AreEqual(serialNr[2], 0x0);
            Assert.AreEqual(serialNr[3], 0x0);
            Assert.AreEqual(serialNr[4], 0x0);
            Assert.AreEqual(serialNr[5], 0x0);
            Assert.AreEqual(serialNr[6], 0x0);
            // Check the user register, write and read back
            byte userWrite = 0xAA;
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE6);
            packet.Add(userWrite);
            sht21.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE7);
            sht21.Write(packet.ToArray());
            packet.Clear();
            byte[] userRead = sht21.Read();
            Assert.AreEqual(userRead[0], userWrite);
            // Do a soft reset and check reset value of user register 
            packet.Add((byte)0xFD);
            packet.Add((byte)0xFE);
            packet.Add((byte)0x0);
            sht21.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE7);
            sht21.Write(packet.ToArray());
            packet.Clear();
            userRead = sht21.Read();
            Assert.AreEqual(userRead[0], 0x3A);
        }

        private byte GetSTH21CRC(byte[] array, int nrOfBytes)
        {
            const uint POLYNOMIAL = 0x131;  // P(x)=x^8+x^5+x^4+1 = 100110001
            byte crc = 0;
            for(byte i = 0; i < nrOfBytes; ++i)
            {
                crc ^= (array[i]);
                for(byte bit = 8; bit > 0; --bit)
                {
                    if((crc & 0x80) == 0x80)
                    {
                        crc = (byte)(((uint)crc << 1) ^ POLYNOMIAL);
                    }
                    else
                    {
                        crc = (byte)(crc << 1);
                    }
                }
            }
            return crc;
        }

        [Test, Ignore("Ignored")]
        public void ReadHumidityTest()
        {
            List<byte> packet = new List<byte>();
            var sht21 = new SHT21();
            sht21.Reset();
            ////////////////////////////////////////////////////////////
            // Start measurement, Relative Humidity - hold master - 12-bit resolution
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Read Relative Humidity sensor data result
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            byte[] humidityBytes = sht21.Read();
            // Check SHT21 CRC
            byte[] resultArray = new byte[2] { 0, 0 };
            resultArray[0] = humidityBytes[0];
            resultArray[1] = humidityBytes[1];
            byte checkCRC = GetSTH21CRC(resultArray, 2);
            Assert.AreEqual(checkCRC, humidityBytes[2]);
            // Assemble data bytes - default precision value is 12 bit
            UInt32 humidityInt = (UInt32)((((UInt16)humidityBytes[0] << 4) & 0xFF0) + (((UInt16)humidityBytes[1] >> 4) & 0xF));
            // Calculate relative humidity
            // RH = -6 + 125 * SRH/2^16
            double humidity = 125.0 * Convert.ToDouble(humidityInt << 4) / 65536.0 - 6.0;
            humidity = Math.Round(humidity, 1);
            Assert.Greater(humidity, 0);
            Assert.LessOrEqual(humidity, 100);
            ////////////////////////////////////////////////////////////
            // Start measurement, Relative Humidity - hold master - 8-bit resolution
            // Set 8-bit resolution
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE6);
            packet.Add((byte)0x3B);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Start measurement
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Read Relative Humidity sensor data result
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            humidityBytes = sht21.Read();
            // Check SHT21 CRC
            resultArray = new byte[2] { 0, 0 };
            resultArray[0] = humidityBytes[0];
            resultArray[1] = humidityBytes[1];
            checkCRC = GetSTH21CRC(resultArray, 2);
            Assert.AreEqual(checkCRC, humidityBytes[2]);
            // Assemble data bytes - precision value is 8 bit
            humidityInt = (UInt32)(humidityBytes[0] & 0xFF);
            // Calculate relative humidity
            // RH = -6 + 125 * SRH/2^16
            humidity = 125.0 * Convert.ToDouble(humidityInt << 8) / 65536.0 - 6.0;
            //humidity *= 100;
            humidity = Math.Round(humidity, 1);
            Assert.Greater(humidity, 0);
            Assert.LessOrEqual(humidity, 100);
            // Start measurement, Relative Humidity - hold master - 8-bit resolution
            // Set 10-bit resolution
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE6);
            packet.Add((byte)0xBA);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Start measurement
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Read Relative Humidity sensor data result
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            humidityBytes = sht21.Read();
            // Check SHT21 CRC
            resultArray = new byte[2] { 0, 0 };
            resultArray[0] = humidityBytes[0];
            resultArray[1] = humidityBytes[1];
            checkCRC = GetSTH21CRC(resultArray, 2);
            Assert.AreEqual(checkCRC, humidityBytes[2]);
            // Assemble data bytes - precision value is 10 bit
            humidityInt = (UInt32)((((UInt16)humidityBytes[0] << 2) & 0x3FC) + (((UInt16)humidityBytes[1] >> 6) & 0x3));
            // RH = -6 + 125 * SRH/2^16
            humidity = 125.0 * Convert.ToDouble(humidityInt << 6) / 65536.0 - 6.0;
            //humidity *= 100;
            humidity = Math.Round(humidity, 1);
            Assert.Greater(humidity, 0);
            Assert.LessOrEqual(humidity, 100);
            // Start measurement, Relative Humidity - hold master - 8-bit resolution
            // Set 11-bit resolution
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE6);
            packet.Add((byte)0xBB);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Start measurement
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Read Relative Humidity sensor data result
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE5);
            sht21.Write(packet.ToArray());
            packet.Clear();
            humidityBytes = sht21.Read();
            // Check SHT21 CRC
            resultArray = new byte[2] { 0, 0 };
            resultArray[0] = humidityBytes[0];
            resultArray[1] = humidityBytes[1];
            checkCRC = GetSTH21CRC(resultArray, 2);
            Assert.AreEqual(checkCRC, humidityBytes[2]);
            // Assemble data bytes - precision value is 11 bit
            humidityInt = (UInt32)((((UInt16)humidityBytes[0] << 3) & 0x7F8) + (((UInt16)humidityBytes[1] >> 5) & 0x7));
            // Calculate relative humidity
            // RH = -6 + 125 * SRH/2^16
            humidity = 125.0 * Convert.ToDouble(humidityInt << 5) / 65536.0 - 6.0;
            //humidity *= 100;
            humidity = Math.Round(humidity, 1);
            Assert.Greater(humidity, 0);
            Assert.LessOrEqual(humidity, 100);
        }

        [Test]
        public void ReadTemperatureTest()
        {
            List<byte> packet = new List<byte>();
            var sht21 = new SHT21();
            sht21.Reset();
            // Start measurement, Temperature - hold master 
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE3);
            sht21.Write(packet.ToArray());
            packet.Clear();
            // Read temperature sensor data result
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE3);
            sht21.Write(packet.ToArray());
            packet.Clear();
            byte[] temperatureBytes = sht21.Read();
            // Check SHT21 CRC
            // Check SHT21 CRC
            byte[] resultArray = new byte[2] { 0, 0 };
            resultArray[0] = temperatureBytes[0];
            resultArray[1] = temperatureBytes[1];
            byte checkCRC = GetSTH21CRC(resultArray, 2);
            Assert.AreEqual(checkCRC, temperatureBytes[2]);
            // Assemble data bytes - default precision value is 14 bit
            UInt16 temperatureInt = (UInt16)((((int)temperatureBytes[0] << 6) & 0x3FC0) + (((int)temperatureBytes[1] & 0xFC) >> 2));
            // Calculate temperature 
            // T = -46.85 + 175.72 * ST/2^16
            double temperature = 17572.0 * Convert.ToDouble(temperatureInt) / 65536.0 - 46.85;
            temperature = Math.Round(temperature, 2);
            Assert.Greater(temperature, 0);
        }
    }
}

