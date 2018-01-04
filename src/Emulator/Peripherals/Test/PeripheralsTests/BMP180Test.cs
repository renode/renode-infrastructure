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
    public class BMP180Test
    {
        [Test]
        public void InitTest()
        {
            List<byte> packet = new List<byte>();
            var bmp180 = new BMP180();
            bmp180.Reset();
            // Check the Chip ID
            packet.Add((byte)0xFC);
            packet.Add((byte)0xD0);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] chipId = bmp180.Read();
            Assert.AreEqual(chipId[0], 0x55);
            // Check the SoftReset
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE0);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] softReset = bmp180.Read();
            Assert.AreEqual(softReset[0], 0);
            // Check the CtrlMeasurement
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] ctrlMeasurement = bmp180.Read();
            Assert.AreEqual(ctrlMeasurement[0], 0);
            // Read, write and check all calibration parameters in one go 
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAA);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibXX = bmp180.Read();
            Assert.AreEqual(calibXX[0], 0x1B);
            Assert.AreEqual(calibXX[1], 0xCB);
            Assert.AreEqual(calibXX[2], 0xFB);
            Assert.AreEqual(calibXX[3], 0xCD);
            Assert.AreEqual(calibXX[4], 0xC6);
            Assert.AreEqual(calibXX[5], 0x91);
            Assert.AreEqual(calibXX[6], 0x7B);
            Assert.AreEqual(calibXX[7], 0xA8);
            Assert.AreEqual(calibXX[8], 0x5F);
            Assert.AreEqual(calibXX[9], 0xE8);
            Assert.AreEqual(calibXX[10], 0x43);
            Assert.AreEqual(calibXX[11], 0x35);
            Assert.AreEqual(calibXX[12], 0x15);
            Assert.AreEqual(calibXX[13], 0x7A);
            Assert.AreEqual(calibXX[14], 0x00);
            Assert.AreEqual(calibXX[15], 0x38);
            Assert.AreEqual(calibXX[16], 0x80);
            Assert.AreEqual(calibXX[17], 0x00);
            Assert.AreEqual(calibXX[18], 0xD4);
            Assert.AreEqual(calibXX[19], 0xBD);
            Assert.AreEqual(calibXX[20], 0x09);
            Assert.AreEqual(calibXX[21], 0x80);
            packet.Add((byte)0xFD);
            packet.Add((byte)0xAA);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            packet.Add((byte)0xDE);
            packet.Add((byte)0xAD);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAA);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            calibXX = bmp180.Read();
            Assert.AreEqual(calibXX[0], 0xDE);
            Assert.AreEqual(calibXX[1], 0xAD);
            Assert.AreEqual(calibXX[2], 0xDE);
            Assert.AreEqual(calibXX[3], 0xAD);
            Assert.AreEqual(calibXX[4], 0xDE);
            Assert.AreEqual(calibXX[5], 0xAD);
            Assert.AreEqual(calibXX[6], 0xDE);
            Assert.AreEqual(calibXX[7], 0xAD);
            Assert.AreEqual(calibXX[8], 0xDE);
            Assert.AreEqual(calibXX[9], 0xAD);
            Assert.AreEqual(calibXX[10], 0xDE);
            Assert.AreEqual(calibXX[11], 0xAD);
            Assert.AreEqual(calibXX[12], 0xDE);
            Assert.AreEqual(calibXX[13], 0xAD);
            Assert.AreEqual(calibXX[14], 0xDE);
            Assert.AreEqual(calibXX[15], 0xAD);
            Assert.AreEqual(calibXX[16], 0xDE);
            Assert.AreEqual(calibXX[17], 0xAD);
            Assert.AreEqual(calibXX[18], 0xDE);
            Assert.AreEqual(calibXX[19], 0xAD);
            Assert.AreEqual(calibXX[20], 0xDE);
            Assert.AreEqual(calibXX[21], 0xAD);
            // Reset calibration parameters
            bmp180.Reset();
            // Read, write and check calibration parameters individually
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAA);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAA = bmp180.Read();
            Assert.AreEqual(calibAA[0], 0x1B);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAB);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAB = bmp180.Read();
            Assert.AreEqual(calibAB[0], 0xCB);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAC);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAC = bmp180.Read();
            Assert.AreEqual(calibAC[0], 0xFB);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAD);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAD = bmp180.Read();
            Assert.AreEqual(calibAD[0], 0xCD);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAE);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAE = bmp180.Read();
            Assert.AreEqual(calibAE[0], 0xC6);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAF);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibAF = bmp180.Read();
            Assert.AreEqual(calibAF[0], 0x91);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB0);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB0 = bmp180.Read();
            Assert.AreEqual(calibB0[0], 0x7B);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB1);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB1 = bmp180.Read();
            Assert.AreEqual(calibB1[0], 0xA8);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB2);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB2 = bmp180.Read();
            Assert.AreEqual(calibB2[0], 0x5F);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB3);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB3 = bmp180.Read();
            Assert.AreEqual(calibB3[0], 0xE8);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB4 = bmp180.Read();
            Assert.AreEqual(calibB4[0], 0x43);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB5);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB5 = bmp180.Read();
            Assert.AreEqual(calibB5[0], 0x35);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB6);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB6 = bmp180.Read();
            Assert.AreEqual(calibB6[0], 0x15);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB7);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB7 = bmp180.Read();
            Assert.AreEqual(calibB7[0], 0x7A);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB8);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB8 = bmp180.Read();
            Assert.AreEqual(calibB8[0], 0x00);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xB9);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibB9 = bmp180.Read();
            Assert.AreEqual(calibB9[0], 0x38);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBA);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBA = bmp180.Read();
            Assert.AreEqual(calibBA[0], 0x80);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBB);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBB = bmp180.Read();
            Assert.AreEqual(calibBB[0], 0x00);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBC);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBC = bmp180.Read();
            Assert.AreEqual(calibBC[0], 0xD4);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBD);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBD = bmp180.Read();
            Assert.AreEqual(calibBD[0], 0xBD);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBE);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBE = bmp180.Read();
            Assert.AreEqual(calibBE[0], 0x09);
            packet.Add((byte)0xFC);
            packet.Add((byte)0xBF);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibBF = bmp180.Read();
            Assert.AreEqual(calibBF[0], 0x80);
        }

        [Test]
        public void CtrlTest()
        {
            List<byte> packet = new List<byte>();
            var bmp180 = new BMP180();
            bmp180.Reset();
            // Check the CtrlMeasurement register
            // Write a value to register
            byte ctrlValue = 0xAA;
            packet.Add((byte)0xFD);
            packet.Add((byte)0xF4);
            packet.Add(ctrlValue);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read the CtrlMeasuremnent register value
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] ctrlMeasurement = bmp180.Read();
            Assert.AreEqual(ctrlMeasurement[0], ctrlValue);
            // Check the SoftReset
            ctrlValue = 0xB6; //Should do same sequence as power-on-reset
            packet.Add((byte)0xFD);
            packet.Add((byte)0xE0);
            packet.Add(ctrlValue);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read the SoftReset register value
            packet.Add((byte)0xFC);
            packet.Add((byte)0xE0);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] softReset = bmp180.Read();
            Assert.AreEqual(softReset[0], 0);
            // Read the CtrlMeasuremnent register value
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            ctrlMeasurement = bmp180.Read();
            Assert.AreEqual(ctrlMeasurement[0], 0);
        }

        [Test]
        public void ReadMeasurementsTest()
        {
            List<byte> packet = new List<byte>();
            var bmp180 = new BMP180();
            bmp180.Reset();
            // Read and setup calibration parameters.
            packet.Add((byte)0xFC);
            packet.Add((byte)0xAA);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] calibXX = bmp180.Read();
            Int16 AC1 = (Int16)(((Int16)calibXX[0] << 8) + (Int16)calibXX[1]);
            Int16 AC2 = (Int16)(((Int16)calibXX[2] << 8) + (Int16)calibXX[3]);
            Int16 AC3 = (Int16)(((Int16)calibXX[4] << 8) + (Int16)calibXX[5]);
            UInt16 AC4 = (UInt16)(((UInt16)calibXX[6] << 8) + (UInt16)calibXX[7]);
            UInt16 AC5 = (UInt16)(((UInt16)calibXX[8] << 8) + (UInt16)calibXX[9]);
            UInt16 AC6 = (UInt16)(((UInt16)calibXX[10] << 8) + (UInt16)calibXX[11]);
            Int16 B1 = (Int16)(((Int16)calibXX[12] << 8) + (Int16)calibXX[13]);
            Int16 B2 = (Int16)(((Int16)calibXX[14] << 8) + (Int16)calibXX[15]);
            // MB is currently not used
            //Int16 MB = (Int16)(((Int16)calibXX[16] << 8) + (Int16)calibXX[17]);
            Int16 MC = (Int16)(((Int16)calibXX[18] << 8) + (Int16)calibXX[19]);
            Int16 MD = (Int16)(((Int16)calibXX[20] << 8) + (Int16)calibXX[21]);
            // Start temperature measurement
            byte ctrlValue = 0x2E;
            packet.Add((byte)0xFD);
            packet.Add((byte)0xF4);
            packet.Add(ctrlValue);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read the CtrlMeasuremnent register value
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            byte[] ctrlMeasurement = bmp180.Read();
            Assert.AreEqual(ctrlMeasurement[0], ctrlValue);
            // Wait 5 milliseconds, (> 4.5 is ok - see Datasheet for BMP180) 
            Thread.Sleep(5);
            // Construct packet list for read of temperature registers
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF6);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read Temperature MSB and LSB registers
            byte[] temp_bytes = bmp180.Read();
            // Calculate temperature
            // X1  = (UT - AC6)*AC5/2^15
            // X2  = MC*2^11/(X1+MD)
            // For pressure calculation B5 = X1 + X2 is needed
            // T   = (X1 + X2 + 8)/2^4
            Int32 UT = (((Int32)temp_bytes[0] << 8) & 0xFF00) + ((Int32)temp_bytes[1] & 0xFF);
            Int32 X1 = (UT - Convert.ToInt32(AC6)) * Convert.ToInt32(AC5) / 0x8000;
            Int32 X2 = (Convert.ToInt32(MC) * 0x800) / (X1 + Convert.ToInt32(MD));
            Int32 B5 = X1 + X2;
            Int32 temperatureInt = (X1 + X2 + 8) / 16;
            // Temperature is given in 0.1 degrees C scale
            double temperature = Convert.ToDouble(temperatureInt) / 10.0;
            Assert.Greater(temperature, -40.0);
            // Start pressure measurement
            // Set oversampling ratio
            byte ossBits = 0; // 0,1,2,3 means 1,2,4,8 times oversampling
            ctrlValue = (byte)(0x34 + ((int)ossBits << 6));
            packet.Add((byte)0xFD);
            packet.Add((byte)0xF4);
            packet.Add(ctrlValue);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read the CtrlMeasuremnent register value
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF4);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            ctrlMeasurement = bmp180.Read();
            Assert.AreEqual(ctrlMeasurement[0], ctrlValue);
            // Wait 5 milliseconds, (> 4.5 is ok - see Datasheet for BMP180) 
            Thread.Sleep(5);
            // Construct packet list for read of pressure registers
            packet.Add((byte)0xFC);
            packet.Add((byte)0xF6);
            bmp180.Write(packet.ToArray());
            packet.Clear();
            // Read pressure MSB, LSB and XLSB registers
            byte[] pressure_bytes = bmp180.Read();
            // Calculate pressure ; B5 is carried over from temperature calculation
            // B6 = B5 - 4000 
            // --- Calculate B3
            // X1 = B2 * (B6*B6 / 2^12) / 2^11
            // X2 = AC2 * B6 / 2^11
            // X3 = X1 + X2
            // B3 = ((AC1 * 4 + X3) << oss + 2) / 4
            // --- Calculate B4
            // X1 = AC3 * B6 / 2^13
            // X2 = B1 * (B6*B6 / 2^12) / 2^16
            // X3 = (X1 + X2 + 2) / 2^2
            // B4 = AC4 * (unsigned long)(X3 + 32768) / 2^15
            // --- Calculate B7
            // B7 = ((unsigned long)UP - B3) * (50000 >> oss)
            // --- Finally calculate pressure
            // if (B7 < 0x80000000) 
            //   p = (2 * B7) / B4
            // else 
            //   p = 2 * (B7 / B4)
            // X1 = (p / 2^8) * (p / 2^8)
            // X1 = (X1 * 3038) / 2^16
            // X2 = (-7357 * p) / 2^16
            // P = p + (X1 + X2 + 3791) / 2^4
            UInt32 uintUP = (((UInt32)pressure_bytes[0] << 16) & 0xFF0000) + (((UInt32)pressure_bytes[1] << 8) & 0xFF00) + (((UInt32)pressure_bytes[2]) & 0xFF);
            // Handle oversampling setting
            double UP = Convert.ToDouble(uintUP >> (8 - ossBits));
            Int32 B6 = B5 - 4000;
            X1 = Convert.ToInt32(B2) * (B6 * B6 / 4096) / 2048;
            X2 = Convert.ToInt32(AC2) * B6 / 2048;
            Int32 X3 = X1 + X2;
            Int32 B3 = (((4 * Convert.ToInt32(AC1) + X3) << ossBits) + 2) / 4;
            X1 = Convert.ToInt32(AC3) * B6 / 8192;
            X2 = Convert.ToInt32(B1) * (B6 * B6 / 4096) / 65536;
            X3 = (X1 + X2 + 2) / 4;
            UInt32 B4 = Convert.ToUInt32(AC4) * Convert.ToUInt32(X3 + 32768) / 32768;
            UInt32 B7 = (Convert.ToUInt32(UP) - Convert.ToUInt32(B3)) * Convert.ToUInt32((50000 >> ossBits));
            Int32 p1 = 0;
            if(B7 < 0x80000000)
            {
                p1 = Convert.ToInt32((2 * B7) / B4);
            }
            else
            {
                p1 = Convert.ToInt32(2 * (B7 / B4));
            }
            X1 = (p1 / 256) * (p1 / 256);
            X1 = (X1 * 3038) / 65536;
            X2 = 0;
            X2 -= 7357 * p1 / 65536;
            double pressure = Convert.ToDouble(p1 + (X1 + X2 + 3791) / 16);
            pressure = Math.Round(pressure, 0);
            Assert.Greater(pressure, 0);
            // Given the pressure at sea level p_0 e.g. 101325 Pa
            // Calculate altitude = 44330 * [1 - ( p / p_0 )^( 1 / 5.255 )]
            //Int32 altitudeInt = 44330 * (1 - Convert.ToInt32(Math.Pow((pressure / 101325.0), (1.0 / 5.255))));
            //Console.WriteLine("Altitude: " + altitudeInt.ToString() + " meters above sea level");
        }
    }
}

