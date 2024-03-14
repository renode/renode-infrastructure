//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// license text is available in 'licenses/MIT.txt'.
//

using NUnit.Framework;
using System;

namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class CRCTests
    {
        [Test]
        public void ShouldThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => { new CRCEngine(0xFF, 7, true, true, 0x00, 0x00); }
            );
            Assert.Throws<ArgumentOutOfRangeException>(
                () => { new CRCEngine(0xFF, 0, true, true, 0x00, 0x00); }
            );
            Assert.Throws<ArgumentOutOfRangeException>(
                () => { new CRCEngine(0xFF, 33, true, true, 0x00, 0x00); }
            );
        }

        [Test]
        public void ShouldCalculateMultipleUpdates()
        {
            var crc = new CRCEngine(0x04C11DB7, 32, true, true, 0x00, 0x00);
            crc.Update(new byte[] { });
            Assert.AreEqual(0x0000000, crc.Value);
            crc.Update(new byte[] { 0x31 });
            Assert.AreEqual(0x51DE003A, crc.Value);
            crc.Update(new byte[] { 0x32 });
            Assert.AreEqual(0xE8A5632, crc.Value);
        }

        [Test]
        public void ShouldHandleReflections()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, false, true, 0x00, 0x00);
            crc.Update(new byte[] { 0x32, 0x33 });
            Assert.AreEqual(0xCE1E8E92, crc.Value);

            crc = new CRCEngine(CRCPolynomial.CRC32, true, false, 0x00, 0x00);
            crc.Update(new byte[] { 0x32, 0x33 });
            Assert.AreEqual(0xE6AC054A, crc.Value);

            crc = new CRCEngine(CRCPolynomial.CRC32, false, false, 0x00, 0x00);
            crc.Update(new byte[] { 0x32, 0x33 });
            Assert.AreEqual(0x49717873, crc.Value);
        }

        [Test]
        public void ShouldInitConfigProperly()
        {
            var crc32 = new CRCEngine(CRCPolynomial.CRC32);
            Assert.AreEqual(0xE8A5632, crc32.Calculate(new byte[] { 0x31, 0x32 }));

            var crc16ccitt = new CRCEngine(CRCPolynomial.CRC16_CCITT);
            Assert.AreEqual(0xBDEB, crc16ccitt.Calculate(new byte[] { 0x31, 0x32 }));

            var crc16 = new CRCEngine(CRCPolynomial.CRC16);
            Assert.AreEqual(0x4594, crc16.Calculate(new byte[] { 0x31, 0x32 }));
        }

        [Test]
        public void ShouldCalculateCRC32()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, true, true, 0xFFFFFFFF, 0xFFFFFFFF);
            Assert.AreEqual(0xCBF43926, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC32BZIP()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, false, false, 0xFFFFFFFF, 0xFFFFFFFF);
            Assert.AreEqual(0xFC891918, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC32JAMCRC()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, true, true, 0xFFFFFFFF, 0);
            Assert.AreEqual(0x340BC6D9, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC32MPEG2()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, false, false, 0xFFFFFFFF, 0);
            Assert.AreEqual(0x0376E6E7, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC32POSIX()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, false, false, 0, 0xFFFFFFFF);
            Assert.AreEqual(0x765E7680, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC32SATA()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC32, false, false, 0x52325032, 0);
            Assert.AreEqual(0xCF72AFE8, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC31Philips()
        {
            var crc = new CRCEngine(0x04C11DB7, 31, false, false, 0x7FFFFFFF, 0x7FFFFFFF);
            Assert.AreEqual(0x0CE9E46C, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC16CCITT()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC16_CCITT, true, true, 0x0000, 0x0000);
            Assert.AreEqual(0x2189, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC16()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC16, true, true, 0x0000, 0x0000);
            Assert.AreEqual(0xBB3D, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC8()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC8_CCITT, false, false, 0x00, 0x00);
            Assert.AreEqual(0xF4, crc.Calculate(checkInputData));
        }

        [Test]
        public void ShouldCalculateCRC7()
        {
            var crc = new CRCEngine(CRCPolynomial.CRC7, false, false, 0x00, 0x00);
            Assert.AreEqual(0x75, crc.Calculate(checkInputData));
        }

        private readonly byte[] checkInputData = new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39 };
    }
}
