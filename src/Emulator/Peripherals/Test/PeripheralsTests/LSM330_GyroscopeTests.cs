//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Sensors;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class LSM330_GyroscopeTests
    {
        [SetUp]
        public void CreateSensor()
        {
            gyro = new LSM330_Gyroscope();
        }

        [Test]
        public void ShouldReportWhoAmI()
        {
            // LSM330 datasheet (DocID023426 Rev 3), Table 17: WHO_AM_I_G = 1101 0100
            Assert.AreEqual(0xD4, ReadRegister(WhoAmI));
        }

        [Test]
        public void ShouldKeepControlRegister1()
        {
            Assert.AreEqual(0x07, ReadRegister(Control1), "default value");
            WriteRegister(Control1, 0x0F);
            Assert.AreEqual(0x0F, ReadRegister(Control1));
        }

        [Test]
        public void ShouldUseDatasheetSensitivityAt250Dps()
        {
            // Table 3, G_So: 8.75 mdps/digit -> 1 dps = 114.29 digits
            gyro.AngularRateZ = 1m;
            Assert.AreEqual(114, ReadOutputZ());
            gyro.AngularRateZ = -100m;
            Assert.AreEqual(-11429, ReadOutputZ());
        }

        [Test]
        public void ShouldUseDatasheetSensitivityAt500And2000Dps()
        {
            WriteRegister(Control4, 0x10);   // FS = 01: 500 dps, 17.50 mdps/digit
            gyro.AngularRateZ = 100m;
            Assert.AreEqual(5714, ReadOutputZ());
            WriteRegister(Control4, 0x20);   // FS = 10: 2000 dps, 70 mdps/digit
            Assert.AreEqual(1429, ReadOutputZ());
        }

        [Test]
        public void ShouldSaturateOutsideFullScale()
        {
            gyro.AngularRateZ = 300m;        // above 250 dps: saturates, does not wrap
            Assert.AreEqual(short.MaxValue, ReadOutputZ());
            gyro.AngularRateZ = -300m;
            Assert.AreEqual(short.MinValue, ReadOutputZ());
        }

        private short ReadOutputZ()
        {
            var low = ReadRegister(OutputZLow);
            var high = ReadRegister(OutputZHigh);
            return (short)(low | (high << 8));
        }

        private byte ReadRegister(byte register)
        {
            gyro.Write(new byte[] { register });
            var value = gyro.Read(1)[0];
            gyro.FinishTransmission();
            return value;
        }

        private void WriteRegister(byte register, byte value)
        {
            gyro.Write(new byte[] { register, value });
            gyro.FinishTransmission();
        }

        private LSM330_Gyroscope gyro;

        private const byte WhoAmI = 0x0F;
        private const byte Control1 = 0x20;
        private const byte Control4 = 0x23;
        private const byte OutputZLow = 0x2C;
        private const byte OutputZHigh = 0x2D;
    }
}
