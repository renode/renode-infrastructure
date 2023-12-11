//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948
    {
        private void DefineGyroAccelUserBank1Registers()
        {
            GyroAccelUserBank1Registers.SelfTestXGyroscope.Define(gyroAccelUserBank1Registers)
                .WithTag("XG_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.SelfTestYGyroscope.Define(gyroAccelUserBank1Registers)
                .WithTag("YG_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.SelfTestZGyroscope.Define(gyroAccelUserBank1Registers)
                .WithTag("ZG_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.SelfTestXAcceleration.Define(gyroAccelUserBank1Registers)
                .WithTag("XA_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.SelfTestYAcceleration.Define(gyroAccelUserBank1Registers)
                .WithTag("YA_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.SelfTestZAcceleration.Define(gyroAccelUserBank1Registers)
                .WithTag("ZA_ST_DATA", 0, 8)
            ;

            GyroAccelUserBank1Registers.XAxisOffsetHigh.Define(gyroAccelUserBank1Registers)
                .WithTag("XA_OFFS", 0, 8)
            ;

            GyroAccelUserBank1Registers.XAxisOffsetLow.Define(gyroAccelUserBank1Registers)
                .WithTag("XA_OFFS", 1, 7)
                .WithReservedBits(0, 1)
            ;

            GyroAccelUserBank1Registers.YAxisOffsetHigh.Define(gyroAccelUserBank1Registers)
                .WithTag("YA_OFFS", 0, 8)
            ;

            GyroAccelUserBank1Registers.YAxisOffsetLow.Define(gyroAccelUserBank1Registers)
                .WithTag("YA_OFFS", 1, 7)
                .WithReservedBits(0, 1)
            ;

            GyroAccelUserBank1Registers.ZAxisOffsetHigh.Define(gyroAccelUserBank1Registers)
                .WithTag("ZA_OFFS", 0, 8)
            ;

            GyroAccelUserBank1Registers.ZAxisOffsetLow.Define(gyroAccelUserBank1Registers)
                .WithTag("ZA_OFFS", 1, 7)
                .WithReservedBits(0, 1)
            ;

            GyroAccelUserBank1Registers.TimebaseCorrectionPLL.Define(gyroAccelUserBank1Registers)
                .WithTag("TBC_PLL", 0, 8)
            ;

            DefineBankSelectRegister(gyroAccelUserBank1Registers);
        }

        private enum GyroAccelUserBank1Registers : byte
        {
            SelfTestXGyroscope = 0x2,
            SelfTestYGyroscope = 0x3,
            SelfTestZGyroscope = 0x4,
            SelfTestXAcceleration = 0xE,
            SelfTestYAcceleration = 0xF,
            SelfTestZAcceleration = 0x10,
            XAxisOffsetHigh = 0x14,
            XAxisOffsetLow = 0x15,
            YAxisOffsetHigh = 0x17,
            YAxisOffsetLow = 0x18,
            ZAxisOffsetHigh = 0x1A,
            ZAxisOffsetLow = 0x1B,
            TimebaseCorrectionPLL = 0x28,
        }
    }
}
