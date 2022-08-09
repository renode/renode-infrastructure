//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals.Miscellaneous;
using System.Collections.Generic;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class OpenTitan_AES_Tests
    {
        [SetUp]
        public void SetUp()
        {
            peripheral = new OpenTitan_AES(new Machine());
        }

        [Test]
        public void ShouldStartInIdleMode()
        {
            var status = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.Status);
            Assert.AreEqual(0x1, status & 0x1);
        }

        [Test]
        public void ShouldChangeStatusWhenSetToAutomaticMode()
        {
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x8);
            var status = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.Status);
            Assert.AreEqual(0x10, status & 0x10);
        }

        [Test]
        public void ShouldKeepTrackOfDataWrites()
        {
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_0, 0x1234);

            Assert.IsFalse(peripheral.InputDataReady);
            WriteInputData(encrypted);
            Assert.IsTrue(peripheral.InputDataReady);
        }

        [Test]
        //This test is based on OpenTitan AES smoketest binary
        public void ShouldGiveCorrectEncryptionOutput()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x205);
            WriteInputData(decrypted);

            for(int i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreEqual(encrypted[i], actual, "DATA_OUT_{0}: Expected: 0x{1:X}, Got: 0x{2:x}", i, encrypted[i], actual);
            }
        }

        [Test]
        //This test is based on OpenTitan AES smoketest binary
        public void ShouldWorkInManualModeToo()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x8405);
            WriteInputData(decrypted);

            // Before trigger
            for(int i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreEqual(0x0, actual, "DATA_OUT_{0}: Expected to be zero before trigger, Got: 0x{1:x}", i, actual);
            }

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Trigger, 0x1);

            // After trigger
            for(int i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreEqual(encrypted[i], actual, "DATA_OUT_{0}: Expected: 0x{1:X}, Got: 0x{2:x}", i, encrypted[i], actual);
            }

        }

        [Test]
        //This test is based on OpenTitan AES smoketest binary
        public void ShouldGiveCorrectDecryptionOutput()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x206);

            WriteInputData(encrypted);

            for(int i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0+ 4 * i);
                Assert.AreEqual(decrypted[i], actual, "DATA_OUT_{0}: Expected: 0x{1:X}, Got: 0x{2:x}", i, decrypted[i], actual);
            }
        }

        [Test]
        public void ShouldRandomizeDataOutputOnTrigger()
        {
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Trigger, 0x4);
            uint sum = 0;
            for(int i = 0; i < 4; i++)
            {
                sum += peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
            }
            Assert.AreNotEqual(0x0, sum);
        }

        [Test]
        public void ShouldRandomizeInputRegistersOnTrigger()
        {
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x1005);

            // We only test if the output is different after randomizing
            WriteInputData(decrypted);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Trigger, 0x6);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Trigger, 0x1);

            for( var i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreNotEqual(encrypted[i], actual);
            }
        }

        [Test]
        public void ShouldBeAbleToDoMultipleTransformationsWithASingleKey()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x206);

            WriteInputData(decrypted);
            WriteInputData(encrypted);

            for(int i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreEqual(decrypted[i], actual, "DATA_OUT_{0}: Expected: 0x{1:X}, Got: 0x{2:x}", i, decrypted[i], actual);
            }
        }

        private void WriteInputData(uint[] data)
        {
            DebugHelper.Assert(data.Length == 4);

            for(var i = 0; i < 4; i++)
            {
                peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_0 + 4 * i, data[i]);
            }
        }

        private void SetKeyShare()
        {
            for(int i = 0; i < 8; i++)
            {
                peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_0 + i * 4, keyShare0[i]);
                peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare1_0 + i * 4, keyShare1[i]);
            }
        }

        private OpenTitan_AES peripheral;

        private readonly uint[] keyShare0 = { 0x3C2D1E0F, 0x78695A4B, 0xB4A59687, 0xF0E1D2C3, 0x29380B1A, 0x6D7C4F5E, 0xA1B08392, 0xE5F4C7D6 };
        private readonly uint[] keyShare1 = { 0x3F2F1F0F, 0x7F6F5F4F, 0xBFAF9F8F, 0xFFEFDFCF, 0x3A2A1A0A, 0x7A6A5A4A, 0xBAAA9A8A, 0xFAEADACA };
        private readonly uint[] encrypted = { 0xCAB7A28E, 0xBF456751, 0x9049FCEA, 0x8960494B };
        private readonly uint[] decrypted = { 0x33221100, 0x77665544, 0xBBAA9988, 0xFFEEDDCC };
    }
}
