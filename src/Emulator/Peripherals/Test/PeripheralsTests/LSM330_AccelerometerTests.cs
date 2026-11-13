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
    public class LSM330_AccelerometerTests
    {
        [SetUp]
        public void CreateSensor()
        {
            accelerometer = new LSM330_Accelerometer();
        }

        [Test]
        public void ShouldReportWhoAmI()
        {
            // LSM330 datasheet (DocID023426 Rev 3), Table 17: WHO_AM_I_A = 0100 0000
            Assert.AreEqual(0x40, ReadRegister(WhoAmI));
        }

        [Test]
        public void ShouldKeepControlRegister5()
        {
            // Table 17: CTRL_REG5_A defaults to 0000 0111 (power-down, X/Y/Z enabled)
            Assert.AreEqual(0x07, ReadRegister(Control5), "default value");
            WriteRegister(Control5, 0x77);   // 400 Hz, X/Y/Z enabled
            Assert.AreEqual(0x77, ReadRegister(Control5));
        }

        [Test]
        public void ShouldUseDatasheetSensitivityAt2G()
        {
            // Table 3, LA_So: 0.061 mg/digit -> 1 g = 16393 digits
            accelerometer.AccelerationX = 1m;
            Assert.AreEqual(16393, ReadOutputX());
            accelerometer.AccelerationX = -0.5m;
            Assert.AreEqual(-8197, ReadOutputX());
        }

        [Test]
        public void ShouldUseDatasheetSensitivityAt16G()
        {
            WriteRegister(Control6, 0x20);   // FSCALE = 100: +-16 g, 0.732 mg/digit
            accelerometer.AccelerationX = 1m;
            Assert.AreEqual(1366, ReadOutputX());
        }

        [Test]
        public void ShouldSaturateOutsideFullScale()
        {
            accelerometer.AccelerationX = 3m;         // above +-2 g: saturates, does not wrap
            Assert.AreEqual(short.MaxValue, ReadOutputX());
            accelerometer.AccelerationX = -3m;
            Assert.AreEqual(short.MinValue, ReadOutputX());
        }

        private short ReadOutputX()
        {
            var low = ReadRegister(OutputXLow);
            var high = ReadRegister(OutputXHigh);
            return (short)(low | (high << 8));
        }

        private byte ReadRegister(byte register)
        {
            accelerometer.Write(new byte[] { register });
            var value = accelerometer.Read(1)[0];
            accelerometer.FinishTransmission();
            return value;
        }

        private void WriteRegister(byte register, byte value)
        {
            accelerometer.Write(new byte[] { register, value });
            accelerometer.FinishTransmission();
        }

        private LSM330_Accelerometer accelerometer;

        private const byte WhoAmI = 0x0F;
        private const byte Control5 = 0x20;
        private const byte Control6 = 0x24;
        private const byte OutputXLow = 0x28;
        private const byte OutputXHigh = 0x29;
    }
}
