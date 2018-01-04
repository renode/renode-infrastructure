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
    public class BMA180Test
    {
        [Test]
        public void InitTest()
        {
            List<byte> packet = new List<byte>();
            var bma180 = new BMA180();
            bma180.Reset();
            ////////////////////////////////////////////////////////////
            // Check the Chip ID
            packet.Add((byte)0xFC);
            packet.Add((byte)0x00);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] registerValue = bma180.Read();
            Assert.AreEqual(registerValue[0], 0x3);
            ////////////////////////////////////////////////////////////
            // Check CtrlReg0
            // Read default
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0D);
            bma180.Write(packet.ToArray());
            packet.Clear();
            registerValue = bma180.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            // Write, set ee_w bit
            packet.Add((byte)0xFD);
            packet.Add((byte)0x0D);
            packet.Add((byte)0x10);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read back
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0D);
            bma180.Write(packet.ToArray());
            packet.Clear();
            registerValue = bma180.Read();
            Assert.AreEqual(registerValue[0], 0x10);
            ////////////////////////////////////////////////////////////
            // Check the SoftReset
            packet.Add((byte)0xFC);
            packet.Add((byte)0x10);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] softReset = bma180.Read();
            Assert.AreEqual(softReset[0], 0);
            // Write, command 0xB6 for soft reset
            packet.Add((byte)0xFD);
            packet.Add((byte)0x10);
            packet.Add((byte)0xB6);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read back CtrlReg0 and check that ee_w bit is cleared
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0D);
            bma180.Write(packet.ToArray());
            packet.Clear();
            registerValue = bma180.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            ////////////////////////////////////////////////////////////
            // Check LowDur
            packet.Add((byte)0xFC);
            packet.Add((byte)0x26);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] lowDur = bma180.Read();
            Assert.AreEqual(lowDur[0], 0x50);
            ////////////////////////////////////////////////////////////
            // Check HighDur
            packet.Add((byte)0xFC);
            packet.Add((byte)0x27);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] highDur = bma180.Read();
            Assert.AreEqual(highDur[0], 0x32);
            ////////////////////////////////////////////////////////////
            // Check LowTh
            packet.Add((byte)0xFC);
            packet.Add((byte)0x29);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] lowTh = bma180.Read();
            Assert.AreEqual(lowTh[0], 0x17);
            ////////////////////////////////////////////////////////////
            // Check HighTh
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2A);
            bma180.Write(packet.ToArray());
            packet.Clear();
            byte[] highTh = bma180.Read();
            Assert.AreEqual(highTh[0], 0x50);
        }

        [Test]
        public void ReadDataTest()
        {
            List<byte> packet = new List<byte>();
            var bma180 = new BMA180();
            bma180.Reset();

            // Read temperature measurement
            // Construct packet list for read of temperature register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x08);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read Temperature register
            byte[] temperature = bma180.Read();
            Assert.Greater(temperature[0], 0);
            // Read Accelerometer X measurement
            packet.Add((byte)0xFC);
            packet.Add((byte)0x02);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read Accelerometer X LSB and MSB registers
            byte[] acc_x = bma180.Read();
            UInt16 accelerometerX = (UInt16)((((UInt16)acc_x[1] << 6) & 0x3FC0) + (((UInt16)acc_x[0] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerX, 0);
            Assert.LessOrEqual(accelerometerX, 0x3FFF);
            // Read Accelerometer Y measurement
            packet.Add((byte)0xFC);
            packet.Add((byte)0x04);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read Accelerometer Y LSB and MSB registers
            byte[] acc_y = bma180.Read();
            UInt16 accelerometerY = (UInt16)((((UInt16)acc_y[1] << 6) & 0x3FC0) + (((UInt16)acc_y[0] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerY, 0);
            Assert.LessOrEqual(accelerometerY, 0x3FFF);
            // Read Accelerometer Z measurement
            packet.Add((byte)0xFC);
            packet.Add((byte)0x06);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read Accelerometer Z LSB and MSB registers
            byte[] acc_z = bma180.Read();
            UInt16 accelerometerZ = (UInt16)((((UInt16)acc_z[1] << 6) & 0x3FC0) + (((UInt16)acc_z[0] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerZ, 0);
            Assert.LessOrEqual(accelerometerZ, 0x3FFF);
            // Test read of all three accelerometer ADC values in one go
            // Read Accelerometer X measurement
            packet.Add((byte)0xFC);
            packet.Add((byte)0x02);
            bma180.Write(packet.ToArray());
            packet.Clear();
            // Read once 
            byte[] acc_data = bma180.Read();
            // Check accelerometer data
            accelerometerX = (UInt16)((((UInt16)acc_data[1] << 6) & 0x3FC0) + (((UInt16)acc_data[0] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerX, 0);
            Assert.LessOrEqual(accelerometerX, 0x3FFF);
            accelerometerY = (UInt16)((((UInt16)acc_data[3] << 6) & 0x3FC0) + (((UInt16)acc_data[2] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerY, 0);
            Assert.LessOrEqual(accelerometerY, 0x3FFF);
            accelerometerZ = (UInt16)((((UInt16)acc_data[5] << 6) & 0x3FC0) + (((UInt16)acc_x[1] >> 2) & 0x3F));
            Assert.GreaterOrEqual(accelerometerZ, 0);
            Assert.LessOrEqual(accelerometerZ, 0x3FFF);
        }
    }
}

