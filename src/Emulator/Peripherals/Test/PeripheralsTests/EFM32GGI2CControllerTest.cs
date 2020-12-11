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
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensors;
using System.Threading;
using System.Diagnostics;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class EFM32GGI2CControllerTest
    {
        [Test]
        public void InitTest()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x00), 0);
            // Enable I2C controller
            efm32ggi2ccontroller.WriteDoubleWord(0x0, 0x1);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x00), 0x1);
            // Need check reset of interrupt flags before reading rx data (0x1C)
            // as this will trigger RXUF if enabled
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28), 0);
            // Check I2Cn_IEN before enabling interrupts
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x34), 0);
            // Enable all interrupts, bits 0-16
            efm32ggi2ccontroller.WriteDoubleWord(0x34, 0x1FFFF);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x08), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x0C), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x10), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x14), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x18), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x1C), 0);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x20), 0);
            // IF - RXUF
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28), 0x2000);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x34), 0x1FFFF);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x38), 0);
        }

        [Test]
        public void CtrlTest()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();
            uint ctrlValue = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x0, ctrlValue);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x0), ctrlValue);
        }

        [Test]
        public void InterruptTest()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();

            // Enable I2C controller
            efm32ggi2ccontroller.WriteDoubleWord(0x0, 0x1);
            // Enable all interrupts, bits 0-16
            efm32ggi2ccontroller.WriteDoubleWord(0x34, 0x1FFFF);
            // Clear all interrupts
            efm32ggi2ccontroller.WriteDoubleWord(0x30, 0x1FFFF);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28), 0x0);
            // Set Start, ACK, NACK interrupt flags
            uint interruptMask = 0x1 | 0x40 | 0x80;
            efm32ggi2ccontroller.WriteDoubleWord(0x2C, interruptMask);
            // Check the result on interrupt register
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28), interruptMask);
            // Clear all interrupts
            efm32ggi2ccontroller.WriteDoubleWord(0x30, 0x1FFFF);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28), 0x0);
            // Send start command and check that start interrupt is flagged
            efm32ggi2ccontroller.WriteDoubleWord(0x4, 0x1);
            Assert.AreEqual(efm32ggi2ccontroller.ReadDoubleWord(0x28) & 0x1, 0x1);
        }

        [Test, Ignore("Ignored")]
        public void ReadFromSlaveTest()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();
            var bmp180 = new BMP180();
            bmp180.Reset();
            efm32ggi2ccontroller.Register(bmp180, new NumberRegistrationPoint<int>(0xEE));
            // Enable I2C controller
            uint ctrl = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x0, ctrl);
            // Enable all interrupts
            uint interruptMask = 0x1FFFF;
            efm32ggi2ccontroller.WriteDoubleWord(0x34, interruptMask);
            // Clear all interrupts
            efm32ggi2ccontroller.WriteDoubleWord(0x30, interruptMask);
            // Check interrupt flags
            uint interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual(interruptFlags, 0x0);
            // Write slave address byte to transmit buffer
            uint txData = 0xEE; // Write address for BMP180
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Send start command
            uint cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check slave ACK for address
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x40), 0x40);
            // Write slave BMP180 OutMSB Register Address
            txData = 0xF6;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Initiate read with writing BMP180 address byte with read bit set
            // Send restart command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            txData = 0xEF;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0xC), 0xC);
            // Wait and check if the receive buffer has data
            int loopCounter = 0;
            do
            {
                interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
                loopCounter++;
                Thread.Sleep(10);
            }
            while(((interruptFlags & 0x20) != 0x20) && (loopCounter < 1000));
            Assert.AreEqual((interruptFlags & 0x20), 0x20);
            Assert.AreNotEqual(loopCounter, 1000);
            // Read MSB byte and see that it is the reset value 0x80
            uint rxData = efm32ggi2ccontroller.ReadDoubleWord(0x1C);
            Assert.AreEqual(rxData, 0x80);
            // Send stop command
            cmd = 0x2;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that MSTOP interrupt has been issued
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x100), 0x100);
        }

        [Test, Ignore("Ignored")]
        public void TemperatureMeasurementTest()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();
            var bmp180 = new BMP180();
            bmp180.Reset();
            efm32ggi2ccontroller.Register(bmp180, new NumberRegistrationPoint<int>(0xEE));
            // Enable I2C controller
            uint ctrl = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x0, ctrl);
            // Enable all interrupts
            uint interruptMask = 0x1FFFF;
            efm32ggi2ccontroller.WriteDoubleWord(0x34, interruptMask);
            // Send start command
            uint cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that the START flag was set
            uint interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x1), 0x1);
            // Write slave address byte to transmit buffer
            uint txData = 0xEE; // Write address for BMP180
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Check slave ACK for address
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x40), 0x40);
            // Write more bytes for transmission, start temperature measurement
            txData = 0xF4; // CtrlMeasurment Register Address
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            txData = 0x2E; // Temperature measurement code
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);

            // Wait 5 milliseconds, (> 4.5 is ok - see Datasheet for BMP180)
            Thread.Sleep(5);

            // Start read by specifying OutMSB register
            // - this will return MSB and LSB for sequential reads
            // Send restart command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Write slave address byte to transmit buffer
            txData = 0xEE; // Write address for BMP180
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Check slave ACK for address
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x40), 0x40);
            // Write OutMSB Register Address
            txData = 0xF6;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);

            // Send restart command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Tell BMP180 sensor we will read
            txData = 0xEF; // Write address for BMP180
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);

            // Read byte from slave through controller rx buffer (register address 0x1C)
            // Check if read data is available - RXDATAV interrupt flag
            bool finishedRead = false;
            uint[] rxData = new uint[2] { 0, 0 };
            uint index = 0;
            uint loopCounter = 0;
            while(!finishedRead)
            {
                interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
                if((interruptFlags & 0x20) == 0x20)
                {
                    rxData[index++] = efm32ggi2ccontroller.ReadDoubleWord(0x1C);
                }
                if(index == 2 || loopCounter == 1000)
                {
                    finishedRead = true;
                }
                Thread.Sleep(10);
                loopCounter++;
            }
            Assert.AreNotEqual(loopCounter, 1000);
            uint temperature = ((rxData[0] << 8) & 0xFF00) + rxData[1];
            Assert.Greater(temperature, 0);
            // Send stop command
            cmd = 0x2;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
        }

        [Test]
        public void DualI2CAddressBMC050Test()
        {
            var machine = new Machine();
            var efm32ggi2ccontroller = new EFM32GGI2CController(machine);
            machine.SystemBus.Register(efm32ggi2ccontroller, new BusRangeRegistration(0x4000A000, 0x400));
            efm32ggi2ccontroller.Reset();
            var bmc050 = new BMC050();
            bmc050.Reset();
            efm32ggi2ccontroller.Register(bmc050, new NumberRegistrationPoint<int>(0x18));
            efm32ggi2ccontroller.Register(bmc050, new NumberRegistrationPoint<int>(0x10));
            ////////////////////////////////////////////////////////////
            // Setup the EFM32GG I2C controller
            // Enable I2C controller
            uint ctrl = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x0, ctrl);
            // Enable all interrupts
            uint interruptMask = 0x1FFFF;
            efm32ggi2ccontroller.WriteDoubleWord(0x34, interruptMask);
            ////////////////////////////////////////////////////////////
            // Write BMC050 accelerometer slave address byte to transmit buffer
            // Send start command
            uint cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that the START flag was set
            uint interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x1), 0x1);
            uint txData = 0x18; // Write address for BMC050 accelerometer
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Check slave ACK for address
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x40), 0x40);
            // Write slave BMC050 accelerometer chip ID register address
            txData = 0x0;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Initiate read with writing BMC050 acc address byte with read bit set
            // Send restart command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            txData = 0x19;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0xC), 0xC);
            // Wait and check if the receive buffer has data
            int loopCounter = 0;
            do
            {
                interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
                loopCounter++;
                Thread.Sleep(10);
            }
            while(((interruptFlags & 0x20) != 0x20) && (loopCounter < 1000));
            Assert.AreEqual((interruptFlags & 0x20), 0x20);
            Assert.AreNotEqual(loopCounter, 1000);
            // Read MSB byte and see that it is the correct value
            uint rxData = efm32ggi2ccontroller.ReadDoubleWord(0x1C);
            Assert.AreEqual(rxData, 0x3);
            // Send stop command
            cmd = 0x2;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that MSTOP interrupt has been issued
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x100), 0x100);
            ////////////////////////////////////////////////////////////
            // Send start command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that the START flag was set
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x1), 0x1);
            // Write BMC050 magnetometer slave address byte to transmit buffer
            txData = 0x10; // Write address for BMC050 Magnetometer
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Check slave ACK for address
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x40), 0x40);
            // Write slave BMC050 magnetometer chip ID register address
            txData = 0x40;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0x1000), 0x1000);
            // Initiate read with writing BMC050 mag address byte with read bit set
            // Send restart command
            cmd = 0x1;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            txData = 0x11;
            efm32ggi2ccontroller.WriteDoubleWord(0x24, txData);
            // Check that the transmit buffers are not overflowing
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreNotEqual((interruptFlags & 0xC), 0xC);
            // Wait and check if the receive buffer has data
            loopCounter = 0;
            do
            {
                interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
                loopCounter++;
                Thread.Sleep(10);
            }
            while(((interruptFlags & 0x20) != 0x20) && (loopCounter < 1000));
            Assert.AreEqual((interruptFlags & 0x20), 0x20);
            Assert.AreNotEqual(loopCounter, 1000);
            // Read MSB byte and see that it is the reset value 0x01
            rxData = efm32ggi2ccontroller.ReadDoubleWord(0x1C);
            Assert.AreEqual(rxData, 0x32);
            // Send stop command
            cmd = 0x2;
            efm32ggi2ccontroller.WriteDoubleWord(0x4, cmd);
            // Check that MSTOP interrupt has been issued
            interruptFlags = efm32ggi2ccontroller.ReadDoubleWord(0x28);
            Assert.AreEqual((interruptFlags & 0x100), 0x100);
        }
    }
}

