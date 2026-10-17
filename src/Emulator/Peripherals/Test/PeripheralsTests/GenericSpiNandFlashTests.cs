//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.SPI;
using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class GenericSpiNandFlashTests
    {
        [Test]
        public void ShouldReadDefaultJedecId()
        {
            var flash = new GenericSpiNandFlash();
            var id = ReadId(flash, 2);
            Assert.AreEqual(0x52, id[0], "Manufacturer ID should match Alliance Memory default (0x52)");
            Assert.AreEqual(0x24, id[1], "Device ID should match AS5F14G04SND default (0x24)");
        }

        [Test]
        public void ShouldReadCustom16BitJedecId()
        {
            var flash = new GenericSpiNandFlash(manufacturerId: 0xEF, deviceId: 0xAA21);
            var id = ReadId(flash, 3);
            Assert.AreEqual(0xEF, id[0], "Manufacturer ID should match Winbond (0xEF)");
            Assert.AreEqual(0xAA, id[1], "Device ID MSB should match (0xAA)");
            Assert.AreEqual(0x21, id[2], "Device ID LSB should match (0x21)");
        }

        [Test]
        public void ShouldControlWriteEnableLatch()
        {
            var flash = new GenericSpiNandFlash();
            var status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0, status & 0x02, "WEL bit should be 0 initially");

            WriteEnable(flash);
            status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0x02, status & 0x02, "WEL bit should be 1 after Write Enable");

            WriteDisable(flash);
            status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0, status & 0x02, "WEL bit should be 0 after Write Disable");
        }

        [Test]
        public void ShouldGetAndSetFeatureRegisters()
        {
            var flash = new GenericSpiNandFlash();
            // Test Configuration register (0xB0)
            SetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Config, 0x1A);
            var config = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Config);
            Assert.AreEqual(0x1A, config & 0x1A, "Configuration register should reflect written bits");

            // Test Block Lock register (0xA0)
            SetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.BlockLock, 0x00);
            var blockLock = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.BlockLock);
            Assert.AreEqual(0x00, blockLock, "Block Lock register should be unlocked");
        }

        [Test]
        public void ShouldProgramAndReadBackPage()
        {
            var flash = new GenericSpiNandFlash();
            var testData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x42, 0x13, 0x37, 0xAA };

            WriteEnable(flash);
            ProgramPage(flash, rowAddress: 0x0005, columnAddress: 0x0000, testData);

            var readBack = ReadPage(flash, rowAddress: 0x0005, columnAddress: 0x0000, testData.Length);
            CollectionAssert.AreEqual(testData, readBack, "Read back data should match programmed data");
        }

        [Test]
        public void ShouldSupportRandomProgramLoad()
        {
            var flash = new GenericSpiNandFlash();
            var baseData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            var randomPatch = new byte[] { 0xAA, 0xBB };

            // Program Load (0x02) base data at offset 0
            flash.Transmit(0x02);
            flash.Transmit(0x00);
            flash.Transmit(0x00);
            for(int i = 0; i < baseData.Length; i++)
            {
                flash.Transmit(baseData[i]);
            }
            flash.FinishTransmission();

            // Program Load Random (0x84) patch data at offset 2
            flash.Transmit(0x84);
            flash.Transmit(0x00);
            flash.Transmit(0x02);
            for(int i = 0; i < randomPatch.Length; i++)
            {
                flash.Transmit(randomPatch[i]);
            }
            flash.FinishTransmission();

            // Commit to flash
            WriteEnable(flash);
            flash.Transmit(0x10);
            flash.Transmit(0x00);
            flash.Transmit(0x00);
            flash.Transmit(0x01);
            flash.FinishTransmission();

            // Read back
            var readBack = ReadPage(flash, rowAddress: 0x0001, columnAddress: 0x0000, 6);
            var expected = new byte[] { 0x01, 0x02, 0xAA, 0xBB, 0x05, 0x06 };
            CollectionAssert.AreEqual(expected, readBack, "Random program load should overwrite targeted columns without resetting buffer");
        }

        [Test]
        public void ShouldRejectProgramExecuteWithoutWriteEnable()
        {
            var flash = new GenericSpiNandFlash();
            var testData = new byte[] { 0x11, 0x22, 0x33, 0x44 };

            // Do NOT call WriteEnable
            ProgramPage(flash, rowAddress: 0x0002, columnAddress: 0x0000, testData);

            var status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0x08, status & 0x08, "P_FAIL bit (bit 3) should be set on write without WEL");

            var readBack = ReadPage(flash, rowAddress: 0x0002, columnAddress: 0x0000, testData.Length);
            var expectedEmpty = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            CollectionAssert.AreEqual(expectedEmpty, readBack, "Flash page should remain erased (0xFF)");
        }

        [Test]
        public void ShouldEraseBlock()
        {
            var flash = new GenericSpiNandFlash();
            var testData = new byte[] { 0x12, 0x34, 0x56, 0x78 };

            WriteEnable(flash);
            ProgramPage(flash, rowAddress: 0x0040, columnAddress: 0x0000, testData);

            var beforeErase = ReadPage(flash, rowAddress: 0x0040, columnAddress: 0x0000, testData.Length);
            CollectionAssert.AreEqual(testData, beforeErase);

            WriteEnable(flash);
            BlockErase(flash, rowAddress: 0x0040);

            var afterErase = ReadPage(flash, rowAddress: 0x0040, columnAddress: 0x0000, testData.Length);
            var expectedErased = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            CollectionAssert.AreEqual(expectedErased, afterErase, "Flash page should be 0xFF after block erase");
        }

        [Test]
        public void ShouldRejectBlockEraseWithoutWriteEnable()
        {
            var flash = new GenericSpiNandFlash();
            var testData = new byte[] { 0x99, 0x88, 0x77, 0x66 };

            WriteEnable(flash);
            ProgramPage(flash, rowAddress: 0x0000, columnAddress: 0x0000, testData);

            // Attempt BlockErase without WriteEnable
            BlockErase(flash, rowAddress: 0x0000);

            var status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0x04, status & 0x04, "E_FAIL bit (bit 2) should be set on erase without WEL");

            var readBack = ReadPage(flash, rowAddress: 0x0000, columnAddress: 0x0000, testData.Length);
            CollectionAssert.AreEqual(testData, readBack, "Block should not be erased without WEL");
        }

        [Test]
        public void ShouldResetPeripheralOnResetCommand()
        {
            var flash = new GenericSpiNandFlash();
            WriteEnable(flash);
            var status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0x02, status & 0x02);

            // Send Reset command (0xFF)
            flash.Transmit(0xFF);
            flash.FinishTransmission();

            status = GetFeature(flash, (byte)GenericSpiNandFlash.RegisterType.Status);
            Assert.AreEqual(0, status & 0x02, "WEL bit should be reset to 0 after Reset command");
        }

        [Test]
        public void ShouldSupportHighRowAddressForLargeCapacities()
        {
            // 4Gb capacity: 4096 blocks * 64 pages = 262,144 pages (0x40000 pages)
            var flash = new GenericSpiNandFlash(blocksCount: 4096);
            var testData = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };

            // Target page in block 2048 (rowAddress = 0x20000 = page 131072)
            uint highRowAddress = 0x20000;
            WriteEnable(flash);
            ProgramPage(flash, rowAddress: highRowAddress, columnAddress: 0x0000, testData);

            var readBack = ReadPage(flash, rowAddress: highRowAddress, columnAddress: 0x0000, testData.Length);
            CollectionAssert.AreEqual(testData, readBack, "Page data written to high row address (RA >= 0x10000) should be read back accurately");
        }

        private byte[] ReadId(GenericSpiNandFlash flash, int count = 2)
        {
            flash.Transmit(0x9F);
            flash.Transmit(0x00);
            var result = new byte[count];
            for(int i = 0; i < count; i++)
            {
                result[i] = flash.Transmit(0x00);
            }
            flash.FinishTransmission();
            return result;
        }

        private byte GetFeature(GenericSpiNandFlash flash, byte regAddr)
        {
            flash.Transmit(0x0F);
            flash.Transmit(regAddr);
            var val = flash.Transmit(0x00);
            flash.FinishTransmission();
            return val;
        }

        private void SetFeature(GenericSpiNandFlash flash, byte regAddr, byte val)
        {
            flash.Transmit(0x1F);
            flash.Transmit(regAddr);
            flash.Transmit(val);
            flash.FinishTransmission();
        }

        private void WriteEnable(GenericSpiNandFlash flash)
        {
            flash.Transmit(0x06);
            flash.FinishTransmission();
        }

        private void WriteDisable(GenericSpiNandFlash flash)
        {
            flash.Transmit(0x04);
            flash.FinishTransmission();
        }

        private void ProgramPage(GenericSpiNandFlash flash, uint rowAddress, ushort columnAddress, byte[] data, bool isRandom = false)
        {
            flash.Transmit((byte)(isRandom ? 0x84 : 0x02));
            flash.Transmit((byte)(columnAddress >> 8));
            flash.Transmit((byte)(columnAddress & 0xFF));
            for(int i = 0; i < data.Length; i++)
            {
                flash.Transmit(data[i]);
            }
            flash.FinishTransmission();

            flash.Transmit(0x10);
            flash.Transmit((byte)(rowAddress >> 16));
            flash.Transmit((byte)(rowAddress >> 8));
            flash.Transmit((byte)(rowAddress & 0xFF));
            flash.FinishTransmission();
        }

        private byte[] ReadPage(GenericSpiNandFlash flash, uint rowAddress, ushort columnAddress, int count)
        {
            flash.Transmit(0x13);
            flash.Transmit((byte)(rowAddress >> 16));
            flash.Transmit((byte)(rowAddress >> 8));
            flash.Transmit((byte)(rowAddress & 0xFF));
            flash.FinishTransmission();

            flash.Transmit(0x03);
            flash.Transmit((byte)(columnAddress >> 8));
            flash.Transmit((byte)(columnAddress & 0xFF));
            flash.Transmit(0x00);
            var result = new byte[count];
            for(int i = 0; i < count; i++)
            {
                result[i] = flash.Transmit(0x00);
            }
            flash.FinishTransmission();
            return result;
        }

        private void BlockErase(GenericSpiNandFlash flash, uint rowAddress)
        {
            flash.Transmit(0xD8);
            flash.Transmit((byte)(rowAddress >> 16));
            flash.Transmit((byte)(rowAddress >> 8));
            flash.Transmit((byte)(rowAddress & 0xFF));
            flash.FinishTransmission();
        }
    }
}
