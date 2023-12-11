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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948
    {
        private void DefineGyroAccelUserBank0Registers()
        {
            GyroAccelUserBank0Registers.WhoAmI.Define(gyroAccelUserBank0Registers, 0xEA)
                .WithTag("WHO_AM_I", 0, 8)
            ;

            GyroAccelUserBank0Registers.UserControl.Define(gyroAccelUserBank0Registers)
                .WithTag("DMP_EN", 7, 1)
                .WithTag("FIFO_EN", 6, 1)
                .WithTag("I2C_MST_EN", 5, 1)
                .WithTag("I2C_IF_DIS", 4, 1)
                .WithTag("DMP_RST", 3, 1)
                .WithTag("SRAM_RST", 2, 1)
                .WithTag("I2C_MST_RST", 1, 1)
                .WithReservedBits(0, 1)
            ;

            GyroAccelUserBank0Registers.LPConfig.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(7, 1)
                .WithTag("I2C_MST_CYCLE", 6, 1)
                .WithTag("ACCEL_CYCLE", 5, 1)
                .WithTag("GYRO_CYCLE", 4, 1)
                .WithReservedBits(0, 4)
            ;

            GyroAccelUserBank0Registers.PowerManagement1.Define(gyroAccelUserBank0Registers)
                .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { if(val) SoftwareReset(); }, name: "DEVICE_RESET")
                .WithTag("SLEEP", 6, 1)
                .WithTag("LP_EN", 5, 1)
                .WithReservedBits(4, 1)
                .WithTag("TEMP_DIS", 3, 1)
                .WithTag("CLKSEL", 0, 3)
            ;

            GyroAccelUserBank0Registers.PowerManagement2.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(6, 2)
                .WithTag("DISABLE_ACCEL", 3, 3)
                .WithTag("DISABLE_GYRO", 0, 3)
            ;

            GyroAccelUserBank0Registers.InterruptPinConfig.Define(gyroAccelUserBank0Registers)
                .WithTag("INT1_ACTL", 7, 1)
                .WithTag("INT1_OPEN", 6, 1)
                .WithTag("INT1_LATCH_INT_EN", 5, 1)
                .WithTag("INT_ANYRD_2CLEAR", 4, 1)
                .WithTag("ACTL_FSYNC", 3, 1)
                .WithTag("FSYNC_INT_MODE_EN", 2, 1)
                .WithTag("BYPASS_EN", 1, 1)
                .WithReservedBits(0, 1)
            ;

            GyroAccelUserBank0Registers.InterruptEnable.Define(gyroAccelUserBank0Registers)
                .WithFlag(7, out wakeOnFSYNCInterruptEnabled, name: "REG_WOF_EN")
                .WithReservedBits(4, 3)
                .WithFlag(3, out wakeOnMotionInterruptEnabled, name: "WOM_INT_EN")
                .WithFlag(2, out pllReadyInterruptEnabled, name: "PLL_RDY_EN")
                .WithFlag(1, out digitalMotionProcessorInterruptEnabled, name: "DMP_INT1_EN")
                .WithFlag(0, out i2cMasterInterruptEnabled, name: "I2C_MST_INT_EN")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptEnable1.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out rawDataReadyInterruptEnabled, name: "RAW_DATA_0_RDY_EN")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptEnable2.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out fifoOverflowInterruptEnabled, name: "FIFO_OVERFLOW_EN")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptEnable3.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out fifoWatermarkInterruptEnabled,  name: "FIFO_WM_EN")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.I2CMstStatus.Define(gyroAccelUserBank0Registers)
                .WithTag("PASS_THROUGH", 7, 1)
                .WithTag("I2C_SLV4_DONE", 6, 1)
                .WithTag("I2C_LOST_ARB", 5, 1)
                .WithTag("I2C_SLV4_NACK", 4, 1)
                .WithTag("I2C_SLV3_NACK", 3, 1)
                .WithTag("I2C_SLV2_NACK", 2, 1)
                .WithTag("I2C_SLV1_NACK", 1, 1)
                .WithTag("I2C_SLV0_NACK", 0, 1)
            ;

            GyroAccelUserBank0Registers.InterruptStatus.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(4, 4)
                .WithFlag(3, out wakeOnMotionInterruptStatus, name: "WOM_INT")
                .WithFlag(2, out pllReadyInterruptStatus, name: "PLL_RDY_INT")
                .WithFlag(1, out digitalMotionProcessorInterruptStatus, name: "DMP_INT1")
                .WithFlag(0, out i2cMasterInterruptStatus, name: "I2C_MST_INT")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptStatus1.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out rawDataReadyInterruptStatus, name: "RAW_DATA_0_RDY_INT")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptStatus2.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out fifoOverflowInterruptStatus, name: "FIFO_OVERFLOW_INT")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.InterruptStatus3.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithFlag(0, out fifoWatermarkInterruptStatus, name: "FIFO_WM_INT")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            GyroAccelUserBank0Registers.DelayTimeHigh.Define(gyroAccelUserBank0Registers)
                .WithTag("DELAY_TIMEH", 0, 8)
            ;

            GyroAccelUserBank0Registers.DelayTimeLow.Define(gyroAccelUserBank0Registers)
                .WithTag("DELAY_TIMEL", 0, 8)
            ;

            GyroAccelUserBank0Registers.AccelerationXOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAccelerationX >> 8), name: "ACCEL_XOUT_H")
            ;

            GyroAccelUserBank0Registers.AccelerationXOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAccelerationX, name: "ACCEL_XOUT_L")
            ;

            GyroAccelUserBank0Registers.AccelerationYOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAccelerationY >> 8), name: "ACCEL_YOUT_H")
            ;

            GyroAccelUserBank0Registers.AccelerationYOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAccelerationY, name: "ACCEL_YOUT_L")
            ;

            GyroAccelUserBank0Registers.AccelerationZOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAccelerationZ >> 8), name: "ACCEL_ZOUT_H")
            ;

            GyroAccelUserBank0Registers.AccelerationZOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAccelerationZ, name: "ACCEL_ZOUT_L")
            ;

            GyroAccelUserBank0Registers.GyroscopeXOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAngularRateX >> 8), name: "GYRO_XOUT_H")
            ;

            GyroAccelUserBank0Registers.GyroscopeXOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAngularRateX, name: "GYRO_XOUT_L")
            ;

            GyroAccelUserBank0Registers.GyroscopeYOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAngularRateY >> 8), name: "GYRO_YOUT_H")
            ;

            GyroAccelUserBank0Registers.GyroscopeYOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAngularRateY, name: "GYRO_YOUT_L")
            ;

            GyroAccelUserBank0Registers.GyroscopeZOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawAngularRateZ >> 8), name: "GYRO_ZOUT_H")
            ;

            GyroAccelUserBank0Registers.GyroscopeZOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawAngularRateZ, name: "GYRO_ZOUT_L")
            ;

            GyroAccelUserBank0Registers.TemperatureOutHigh.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(RawTemperature >> 8), name: "TEMP_OUT_H")
            ;

            GyroAccelUserBank0Registers.TemperatureOutLow.Define(gyroAccelUserBank0Registers)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)RawTemperature, name: "TEMP_OUT_L")
            ;

            GyroAccelUserBank0Registers.ExtSlvSensorData0.DefineMany(gyroAccelUserBank0Registers, NumberOfExternalSlaveSensorDataRegisters, (register, index) =>
            {
                register
                    .WithValueField(0, 8, out externalSlaveSensorData[index], FieldMode.Read, name: "EXT_SLV_SENS_DATA_00")
                ;
            });

            GyroAccelUserBank0Registers.FifoEnable1.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(4, 4)
                .WithTag("SLV_3_FIFO_EN", 3, 1)
                .WithTag("SLV_2_FIFO_EN", 2, 1)
                .WithTag("SLV_1_FIFO_EN", 1, 1)
                .WithTag("SLV_0_FIFO_EN", 0, 1)
            ;

            GyroAccelUserBank0Registers.FifoEnable2.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(5, 3)
                .WithTaggedFlag("ACCEL_FIFO_EN", 4)
                .WithTaggedFlag("GYRO_Z_FIFO_EN", 3)
                .WithTaggedFlag("GYRO_Y_FIFO_EN", 2)
                .WithTaggedFlag("GYRO_X_FIFO_EN", 1)
                .WithTaggedFlag("TEMP_FIFO_EN", 0)
            ;

            GyroAccelUserBank0Registers.FifoReset.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_RESET", 0, 5)
            ;

            GyroAccelUserBank0Registers.FifoMode.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_MODE", 0, 5)
            ;

            GyroAccelUserBank0Registers.FifoCountHigh.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_CNT", 0, 5)
            ;

            GyroAccelUserBank0Registers.FifoCountLow.Define(gyroAccelUserBank0Registers)
                .WithTag("FIFO_CNT", 0, 8)
            ;

            GyroAccelUserBank0Registers.FifoRW.Define(gyroAccelUserBank0Registers)
                .WithTag("FIFO_R_W", 0, 8)
            ;

            GyroAccelUserBank0Registers.DataReadyStatus.Define(gyroAccelUserBank0Registers)
                .WithTag("WOF_STATUS", 7, 1)
                .WithReservedBits(4, 3)
                .WithTag("RAW_DATA_RDY", 0, 4)
            ;

            GyroAccelUserBank0Registers.FifoConfig.Define(gyroAccelUserBank0Registers)
                .WithReservedBits(1, 7)
                .WithTag("FIFO_CFG", 0, 1)
            ;

            DefineBankSelectRegister(gyroAccelUserBank0Registers);
        }

        private IFlagRegisterField wakeOnFSYNCInterruptEnabled;
        private IFlagRegisterField wakeOnMotionInterruptEnabled;
        private IFlagRegisterField pllReadyInterruptEnabled;
        private IFlagRegisterField digitalMotionProcessorInterruptEnabled;
        private IFlagRegisterField i2cMasterInterruptEnabled;
        private IFlagRegisterField rawDataReadyInterruptEnabled;
        private IFlagRegisterField fifoOverflowInterruptEnabled;
        private IFlagRegisterField fifoWatermarkInterruptEnabled;
        private IFlagRegisterField wakeOnMotionInterruptStatus;
        private IFlagRegisterField pllReadyInterruptStatus;
        private IFlagRegisterField digitalMotionProcessorInterruptStatus;
        private IFlagRegisterField i2cMasterInterruptStatus;
        private IFlagRegisterField rawDataReadyInterruptStatus;
        private IFlagRegisterField fifoOverflowInterruptStatus;
        private IFlagRegisterField fifoWatermarkInterruptStatus;
        private IValueRegisterField[] externalSlaveSensorData = new IValueRegisterField[NumberOfExternalSlaveSensorDataRegisters];

        private enum GyroAccelUserBank0Registers : byte
        {
            WhoAmI = 0x0,
            UserControl = 0x3,
            LPConfig = 0x5,
            PowerManagement1 = 0x6,
            PowerManagement2 = 0x7,
            InterruptPinConfig = 0xF,
            InterruptEnable = 0x10,
            InterruptEnable1 = 0x11,
            InterruptEnable2 = 0x12,
            InterruptEnable3 = 0x13,
            I2CMstStatus = 0x17,
            InterruptStatus = 0x19,
            InterruptStatus1 = 0x1A,
            InterruptStatus2 = 0x1B,
            InterruptStatus3 = 0x1C,
            DelayTimeHigh = 0x28,
            DelayTimeLow = 0x29,
            AccelerationXOutHigh = 0x2D,
            AccelerationXOutLow = 0x2E,
            AccelerationYOutHigh = 0x2F,
            AccelerationYOutLow = 0x30,
            AccelerationZOutHigh = 0x31,
            AccelerationZOutLow = 0x32,
            GyroscopeXOutHigh = 0x33,
            GyroscopeXOutLow = 0x34,
            GyroscopeYOutHigh = 0x35,
            GyroscopeYOutLow = 0x36,
            GyroscopeZOutHigh = 0x37,
            GyroscopeZOutLow = 0x38,
            TemperatureOutHigh = 0x39,
            TemperatureOutLow = 0x3A,
            ExtSlvSensorData0 = 0x3B,
            ExtSlvSensorData1 = 0x3C,
            ExtSlvSensorData2 = 0x3D,
            ExtSlvSensorData3 = 0x3E,
            ExtSlvSensorData4 = 0x3F,
            ExtSlvSensorData5 = 0x40,
            ExtSlvSensorData6 = 0x41,
            ExtSlvSensorData7 = 0x42,
            ExtSlvSensorData8 = 0x43,
            ExtSlvSensorData9 = 0x44,
            ExtSlvSensorData10 = 0x45,
            ExtSlvSensorData11 = 0x46,
            ExtSlvSensorData12 = 0x47,
            ExtSlvSensorData13 = 0x48,
            ExtSlvSensorData14 = 0x49,
            ExtSlvSensorData15 = 0x4A,
            ExtSlvSensorData16 = 0x4B,
            ExtSlvSensorData17 = 0x4C,
            ExtSlvSensorData18 = 0x4D,
            ExtSlvSensorData19 = 0x4E,
            ExtSlvSensorData20 = 0x4F,
            ExtSlvSensorData21 = 0x50,
            ExtSlvSensorData22 = 0x51,
            ExtSlvSensorData23 = 0x52,
            FifoEnable1 = 0x66,
            FifoEnable2 = 0x67,
            FifoReset = 0x68,
            FifoMode = 0x69,
            FifoCountHigh = 0x70,
            FifoCountLow = 0x71,
            FifoRW = 0x72,
            DataReadyStatus = 0x74,
            FifoConfig = 0x76,
            RegisterBankSelection = 0x7F,
        }
    }
}
