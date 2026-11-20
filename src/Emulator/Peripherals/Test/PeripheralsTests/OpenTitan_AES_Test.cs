//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals.Miscellaneous;

using NUnit.Framework;

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

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x405);
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
        public void ShouldEncryptInCBCModeWithNistVector()
        {
            // Write Key Share 0 (128-bit Key: 2b7e151628aed2a6abf7158809cf4f3c)
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_0, 0x16157e2b);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_1, 0xa6d2ae28);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_2, 0x8815f7ab);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_3, 0x3c4fcf09);
            
            // Zero out the remaining 128 bits of the 256-bit key share space
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_4, 0x0);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_5, 0x0);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_6, 0x0);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare0_7, 0x0);

            // Write Key Share 1 (All zeros so Share0 XOR Share1 = Share0)
            for(int i = 0; i < 8; i++) 
            {
                peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitialKeyShare1_0 + (i * 4), 0x0);
            }

            // Write Initialization Vector (000102030405060708090a0b0c0d0e0f)
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitializationVector_0, 0x03020100);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitializationVector_1, 0x07060504);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitializationVector_2, 0x0b0a0908);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InitializationVector_3, 0x0f0e0d0c);

            // Configure Control Register: 0x109 (Encryption, CBC Mode, 128-bit)
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x109);

            // Write Plaintext (6bc1bee22e409f96e93d7e117393172a)
            // Writing to InputData automatically triggers the cipher process
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_0, 0xe2bec16b);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_1, 0x969f402e);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_2, 0x117e3de9);
            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.InputData_3, 0x2a179373);

            // Read and Assert Ciphertext (Expected: 7649abac8119b246cee98e9b12e9197d)
            Assert.AreEqual(0xacab4976, peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0));
            Assert.AreEqual(0x46b21981, peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_1));
            Assert.AreEqual(0x9b8ee9ce, peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_2));
            Assert.AreEqual(0x7d19e912, peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_3));
        }

        [Test]
        //This test is based on OpenTitan AES smoketest binary
        public void ShouldGiveCorrectDecryptionOutput()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x406);

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

            for(var i = 0; i < 4; i++)
            {
                var actual = peripheral.ReadDoubleWord((long)OpenTitan_AES.Registers.OutputData_0 + 4 * i);
                Assert.AreNotEqual(encrypted[i], actual);
            }
        }

        [Test]
        public void ShouldBeAbleToDoMultipleTransformationsWithASingleKey()
        {
            SetKeyShare();

            peripheral.WriteDoubleWord((long)OpenTitan_AES.Registers.Control, 0x406);

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