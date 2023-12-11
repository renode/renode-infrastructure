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
        private void DefineGyroAccelUserBank2Registers()
        {
            GyroAccelUserBank2Registers.GyroscopeSampleRateDivider.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroSampleRateDivider, name: "GYRO_SMPLRT_DIV")
            ;

            GyroAccelUserBank2Registers.GyroscopeConfig1.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(6, 2)
                .WithTag("GYRO_DLPFCFG", 3, 3)
                .WithEnumField(1, 2, out gyroFullScaleRange, name: "GYRO_FS_SEL")
                .WithFlag(0, out gyroFilterChoice, name: "GYRO_FCHOICE")
            ;

            GyroAccelUserBank2Registers.GyroscopeConfig2.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(6, 2)
                .WithFlag(5, out gyroSelfTestEnableX, name: "XGYRO_CTEN")
                .WithFlag(4, out gyroSelfTestEnableY, name: "YGYRO_CTEN")
                .WithFlag(3, out gyroSelfTestEnableZ, name: "ZGYRO_CTEN")
                .WithValueField(0, 3, out gyroAveragingFilterExponent, name: "GYRO_AVGCFG")
            ;

            GyroAccelUserBank2Registers.GyroscopeXOffsetCancellationHigh.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationXHigh, name: "X_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.GyroscopeXOffsetCancellationLow.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationXLow, name: "X_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.GyroscopeYOffsetCancellationHigh.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationYHigh, name: "Y_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.GyroscopeYOffsetCancellationLow.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationYLow, name: "Y_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.GyroscopeZOffsetCancellationHigh.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationZHigh, name: "Z_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.GyroscopeZOffsetCancellationLow.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out gyroOffsetCancellationZLow, name: "Z_OFFS_USER")
            ;

            GyroAccelUserBank2Registers.EnableODRStartTimeAlignment.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(1, 7)
                .WithTag("ODR_ALIGN_EN", 0, 1)
            ;

            GyroAccelUserBank2Registers.AccelerationSampleRateDivider1.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(4, 4)
                .WithValueField(0, 4, out accelerometerSampleRateDividerHigh, name: "ACCEL_SMPLRT_DIV")
            ;

            GyroAccelUserBank2Registers.AccelerationSampleRateDivider2.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out accelerometerSampleRateDividerLow, name: "ACCEL_SMPLRT_DIV")
            ;

            GyroAccelUserBank2Registers.AccelerationIntelControl.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(2, 6)
                .WithFlag(1, out wakeOnMotionEnabled, name: "ACCEL_INTEL_EN")
                .WithEnumField(0, 1, out wakeOnMotionAlgorithm, name: "ACCEL_INTEL_MODE_INT")
            ;

            GyroAccelUserBank2Registers.AccelerationWakeOnMotionThreshold.Define(gyroAccelUserBank2Registers)
                .WithValueField(0, 8, out accelerometerWakeOnMotionThreshold, name: "WOM_THRESHOLD")
            ;

            GyroAccelUserBank2Registers.AccelerationConfig.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(6, 2)
                .WithTag("ACCEL_DLPFCFG", 3, 3)
                .WithEnumField(1, 2, out accelerometerFullScaleRange, name: "ACCEL_FS_SEL")
                .WithFlag(0, out accelerometerFilterChoice, name: "ACCEL_FCHOICE")
            ;

            GyroAccelUserBank2Registers.AccelerationConfig2.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(5, 3)
                .WithFlag(4, out accelerometerSelfTestEnableX, name: "AX_ST_EN_REG")
                .WithFlag(3, out accelerometerSelfTestEnableY, name: "AY_ST_EN_REG")
                .WithFlag(2, out accelerometerSelfTestEnableZ, name: "AZ_ST_EN_REG")
                .WithEnumField(0, 2, out accelerometerDecimatorConfig, name: "DEC3_CFG")
            ;

            GyroAccelUserBank2Registers.FSYNCConfig.Define(gyroAccelUserBank2Registers)
                .WithTag("DELAY_TIME_EN", 7, 1)
                .WithReservedBits(6, 1)
                .WithTag("WOF_DEGLITCH_EN", 5, 1)
                .WithTag("WOF_EDGE_INT", 4, 1)
                .WithTag("EXT_SYNC_SET", 0, 4)
            ;

            GyroAccelUserBank2Registers.TemperatureConfig.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(3, 5)
                .WithTag("TEMP_DLPFCFG", 0, 3)
            ;

            GyroAccelUserBank2Registers.ModControl.Define(gyroAccelUserBank2Registers)
                .WithReservedBits(1, 7)
                .WithTag("REG_LP_DMP_EN", 0, 1)
            ;

            DefineBankSelectRegister(gyroAccelUserBank2Registers);
        }

        private IValueRegisterField gyroSampleRateDivider;
        private IEnumRegisterField<GyroFullScaleRangeSelection> gyroFullScaleRange;
        private IFlagRegisterField gyroFilterChoice;
        private IEnumRegisterField<AccelerationFullScaleRangeSelection> accelerometerFullScaleRange;
        private IFlagRegisterField accelerometerFilterChoice;
        private IValueRegisterField accelerometerSampleRateDividerHigh;
        private IValueRegisterField accelerometerSampleRateDividerLow;
        private IValueRegisterField accelerometerWakeOnMotionThreshold;
        private IFlagRegisterField accelerometerSelfTestEnableX;
        private IFlagRegisterField accelerometerSelfTestEnableY;
        private IFlagRegisterField accelerometerSelfTestEnableZ;
        private IEnumRegisterField<AccelerometerDecimator> accelerometerDecimatorConfig;
        private IFlagRegisterField wakeOnMotionEnabled;
        private IEnumRegisterField<WakeOnMotionCompareAlgorithm> wakeOnMotionAlgorithm;
        private IValueRegisterField gyroOffsetCancellationXHigh;
        private IValueRegisterField gyroOffsetCancellationXLow;
        private IValueRegisterField gyroOffsetCancellationYHigh;
        private IValueRegisterField gyroOffsetCancellationYLow;
        private IValueRegisterField gyroOffsetCancellationZHigh;
        private IValueRegisterField gyroOffsetCancellationZLow;
        private IFlagRegisterField gyroSelfTestEnableX;
        private IFlagRegisterField gyroSelfTestEnableY;
        private IFlagRegisterField gyroSelfTestEnableZ;
        private IValueRegisterField gyroAveragingFilterExponent;

        private enum GyroAccelUserBank2Registers : byte
        {
            GyroscopeSampleRateDivider = 0x0,
            GyroscopeConfig1 = 0x1,
            GyroscopeConfig2 = 0x2,
            GyroscopeXOffsetCancellationHigh = 0x3,
            GyroscopeXOffsetCancellationLow = 0x4,
            GyroscopeYOffsetCancellationHigh = 0x5,
            GyroscopeYOffsetCancellationLow = 0x6,
            GyroscopeZOffsetCancellationHigh = 0x7,
            GyroscopeZOffsetCancellationLow = 0x8,
            EnableODRStartTimeAlignment = 0x9,
            AccelerationSampleRateDivider1 = 0x10,
            AccelerationSampleRateDivider2 = 0x11,
            AccelerationIntelControl = 0x12,
            AccelerationWakeOnMotionThreshold = 0x13,
            AccelerationConfig = 0x14,
            AccelerationConfig2 = 0x15,
            FSYNCConfig = 0x52,
            TemperatureConfig = 0x53,
            ModControl = 0x54,
        }
    }
}
