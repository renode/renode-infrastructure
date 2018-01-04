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
    public class BMC050Test
    {
        [Test]
        public void InitTest()
        {
            List<byte> packet = new List<byte>();
            var bmc050 = new BMC050();
            bmc050.Reset();
            // Check the Chip ID
            packet.Add((byte)0xFC);
            packet.Add((byte)0x00);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            byte[] chipId = bmc050.Read();
            Assert.AreEqual(chipId[0], 0x3);
            // Check the reserved 0x01 register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            byte[] registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x21);
            // Check the bandwidth register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x10);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x1F);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x10);
            packet.Add((byte)0x0C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x10);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0C);
            // Check the power register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x11);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x11);
            packet.Add((byte)0x07);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x11);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x07);
            // Check the g-range register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x3);
            // Check the SoftReset register value
            // And funtionality through write of 0x5 to g-range register and check default value 0x3
            packet.Add((byte)0xFC);
            packet.Add((byte)0x14);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x0F);
            packet.Add((byte)0x05);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x5);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x14);
            packet.Add((byte)0xB6); // Issue Soft Reset command
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x0F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x3);
            // Check the DAQ control register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x13);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x13);
            packet.Add((byte)0xC0);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x13);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0xC0);
            // Check the first IRQ control register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x16);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x16);
            packet.Add((byte)0x21);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x16);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x21);
            // Check the second IRQ control register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x17);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x17);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x17);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the first IRQ mapping register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x19);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x19);
            packet.Add((byte)0x02);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x19);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x02);
            // Check the second IRQ mapping register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x1A);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the third IRQ mapping register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x1B);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the IRQ data source register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x1E);
            packet.Add((byte)0x04);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x1E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x04);
            // Check the IRQ electrical behaviour register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x20);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x20);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x20);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the IRQ reset and mode register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x21);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x21);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x21);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the LowDur register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x22);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x09);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x22);
            packet.Add((byte)0x0D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x22);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0D);
            // Check the LowTh register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x23);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x30);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x23);
            packet.Add((byte)0x40);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x23);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x40);
            // Check the low-g interrupt hysteresis register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x24);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x81);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x24);
            packet.Add((byte)0x41);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x24);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x41);
            // Check the HighDur register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x25);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0F);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x25);
            packet.Add((byte)0x1F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x25);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x1F);
            // Check the HighTh register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x26);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0xC0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x26);
            packet.Add((byte)0xC1);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x26);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0xC1);
            // Check the SlopeDur register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x27);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x27);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x27);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the SlopeTh register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x28);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x14);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x28);
            packet.Add((byte)0x11);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x28);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x11);
            // Check the first tap configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x04);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2A);
            packet.Add((byte)0x02);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x02);
            // Check the second tap configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0A);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2B);
            packet.Add((byte)0x0C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0C);
            // Check the first orientation configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x18);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2C);
            packet.Add((byte)0x28);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x28);
            // Check the second orientation configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x08);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2D);
            packet.Add((byte)0x04);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x04);
            // Check the first flat angle configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x08);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2E);
            packet.Add((byte)0x10);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x10);
            // Check the second flat configuration register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x10);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x2F);
            packet.Add((byte)0x20);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x2F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x20);
            // Check the self test register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x32);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x70);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x32);
            packet.Add((byte)0x75);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x32);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x75);
            // Check the EEPROM control register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x33);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x04);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x33);
            packet.Add((byte)0x41); // Unlocks the EEPROM
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x33);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x41);
            // Check the interface config register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x34);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x34);
            packet.Add((byte)0x02);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x34);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x02);
            // Check the offset compensation register
            // Setting bit 7 resets the value of the register 
            packet.Add((byte)0xFC);
            packet.Add((byte)0x36);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x36);
            packet.Add((byte)0x02);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x36);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x02);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x36);
            packet.Add((byte)0x80);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x36);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            // Check the offset target register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x37);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x37);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x37);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for filtered x axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x38);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x38);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x38);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for filtered y axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x39);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x39);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x39);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for filtered z axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x3A);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for unfiltered x axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x3B);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for unfiltered y axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x3C);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the offset compensation for unfiltered z axis data register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x3D);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x3D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the Magnetometer Chip ID 
            packet.Add((byte)0xFC);
            packet.Add((byte)0x40);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x32);
            // Check the magnetometer interrupt status register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4A);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            // Check the magnetometer control register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x1);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4B);
            packet.Add((byte)0x0);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            // Do not leave it in suspended state
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4B);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4B);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the magnetometer operation mode register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x06);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4C);
            packet.Add((byte)0x07);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4C);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x07);
            // Check the first magnetometer interrupt ctrl register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x3F);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4D);
            packet.Add((byte)0x38);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4D);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x38);
            // Check the second magnetometer interrupt ctrl register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x07);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4E);
            packet.Add((byte)0x05);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4E);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x05);
            // Check the magnetometer LowTh register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x4F);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x4F);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the magnetometer HighTh register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x50);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x50);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x50);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the magnetometer RepXY register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x51);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x51);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x51);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
            // Check the magnetometer RepZ register
            packet.Add((byte)0xFC);
            packet.Add((byte)0x52);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x0);
            packet.Add((byte)0xFD);
            packet.Add((byte)0x52);
            packet.Add((byte)0x01);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            packet.Add((byte)0xFC);
            packet.Add((byte)0x52);
            bmc050.Write(packet.ToArray());
            packet.Clear();
            registerValue = bmc050.Read();
            Assert.AreEqual(registerValue[0], 0x01);
        }

        [Test]
        public void ReadDataTest()
        {
            List<byte> packet = new List<byte>();
            var bmc050 = new BMC050();
            bmc050.Reset();

            // TODO : setup device? With parameters for measurements
            for(int i = 0; i < 5; i++)
            {
                // Read temperature measurement
                // Construct packet list for read of temperature register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x08);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Temperature register
                byte[] temperature = bmc050.Read();
                Assert.Greater(temperature[0], 0);
                ////////////////////////////////////////////////////////////
                // Read Accelerometer X measurement
                // Construct packet list for read of accelerometer X register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x02);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Accelerometer X LSB and MSB registers
                byte[] acc_x = bmc050.Read();
                int accelerometerX = (((int)acc_x[1] << 2) & 0xFFC) + (((int)acc_x[0] >> 6) & 0x3);
                Assert.Greater(accelerometerX, 0);
                // Read Accelerometer Y measurement
                // Construct packet list for read of accelerometer Y register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x04);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Accelerometer Y LSB and MSB registers
                byte[] acc_y = bmc050.Read();
                int accelerometerY = (((int)acc_y[1] << 2) & 0xFFC) + (((int)acc_y[0] >> 6) & 0x3);
                Assert.Greater(accelerometerY, 0);
                // Read Accelerometer Z measurement
                // Construct packet list for read of accelerometer Z register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x06);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Accelerometer Z LSB and MSB registers
                byte[] acc_z = bmc050.Read();
                int accelerometerZ = (((int)acc_z[1] << 2) & 0xFFC) + (((int)acc_z[0] >> 6) & 0x3);
                Assert.Greater(accelerometerZ, 0);
                ////////////////////////////////////////////////////////////
                // Read Magnetometer X measurement
                // Construct packet list for read of magnetometer X register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x42);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Magnetometer X LSB and MSB registers
                byte[] mag_x = bmc050.Read();
                int magnetometerX = (UInt16)((((UInt16)mag_x[1] << 5) & 0x1FE0) + (((UInt16)mag_x[0] >> 3) & 0x1F));
                Assert.Greater(magnetometerX, 0);
                // Read Magnetometer Y measurement
                // Construct packet list for read of magnetometer Y register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x44);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Magnetometer Y LSB and MSB registers
                byte[] mag_y = bmc050.Read();
                int magnetometerY = (UInt16)((((UInt16)mag_y[1] << 5) & 0x1FE0) + (((UInt16)mag_y[0] >> 3) & 0x1F));
                Assert.Greater(magnetometerY, 0);
                // Read Magnetometer Z measurement
                // Construct packet list for read of magnetometer Z register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x46);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Magnetometer Z LSB and MSB registers
                byte[] mag_z = bmc050.Read();
                int magnetometerZ = (UInt16)((((UInt16)mag_z[1] << 5) & 0x1FE0) + (((UInt16)mag_z[0] >> 3) & 0x1F));
                Assert.Greater(magnetometerZ, 0);
                // Read Hall resistance measurement
                // Construct packet list for read of Hall resistance registers
                packet.Add((byte)0xFC);
                packet.Add((byte)0x48);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read Hall resistance LSB and MSB registers
                byte[] rhall = bmc050.Read();
                int resistanceHall = (UInt16)((((UInt16)rhall[1] << 5) & 0x1FE0) + (((UInt16)rhall[0] >> 3) & 0x1F));
                Assert.Greater(resistanceHall, 0);
                ////////////////////////////////////////////////////////////
                // Test read of all three accelerometer ADC values in one go
                packet.Add((byte)0xFC);
                packet.Add((byte)0x02);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read 
                byte[] acc_data = bmc050.Read();
                accelerometerX = (((int)acc_data[1] << 2) & 0xFFC) + (((int)acc_data[0] >> 6) & 0x3);
                Assert.Greater(accelerometerX, 0);
                accelerometerY = (((int)acc_data[3] << 2) & 0xFFC) + (((int)acc_data[2] >> 6) & 0x3);
                Assert.Greater(accelerometerY, 0);
                accelerometerZ = (((int)acc_data[5] << 2) & 0xFFC) + (((int)acc_data[4] >> 6) & 0x3);
                Assert.Greater(accelerometerZ, 0);
                ////////////////////////////////////////////////////////////
                // Test read of all three magnetometer ADC and Hall resistance values in one go
                // Construct packet list for read of magnetometer X register
                packet.Add((byte)0xFC);
                packet.Add((byte)0x42);
                bmc050.Write(packet.ToArray());
                packet.Clear();
                // Read 
                byte[] mag_data = bmc050.Read();
                magnetometerX = (UInt16)((((UInt16)mag_data[1] << 5) & 0x1FE0) + (((UInt16)mag_data[0] >> 3) & 0x1F));
                Assert.Greater(magnetometerX, 0);
                magnetometerY = (UInt16)((((UInt16)mag_data[3] << 5) & 0x1FE0) + (((UInt16)mag_data[2] >> 3) & 0x1F));
                Assert.Greater(magnetometerY, 0);
                magnetometerZ = (UInt16)((((UInt16)mag_data[5] << 5) & 0x1FE0) + (((UInt16)mag_data[4] >> 3) & 0x1F));
                Assert.Greater(magnetometerZ, 0);
                resistanceHall = (UInt16)((((UInt16)mag_data[7] << 5) & 0x1FE0) + (((UInt16)mag_data[6] >> 3) & 0x1F));
                Assert.Greater(resistanceHall, 0);
            }
        }
    }
}

