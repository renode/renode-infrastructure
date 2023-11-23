//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ICM20948 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public ICM20948()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Write(byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] Read(int count)
        {
            throw new NotImplementedException();
        }

        public void FinishTransmission()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            GyroAccelRegisters.WhoAmI.Define(this)
                .WithTag("WHO_AM_I", 0, 8);

            GyroAccelRegisters.UserControl.Define(this)
                .WithTag("DMP_EN", 7, 1)
                .WithTag("FIFO_EN", 6, 1)
                .WithTag("I2C_MST_EN", 5, 1)
                .WithTag("I2C_IF_DIS", 4, 1)
                .WithTag("DMP_RST", 3, 1)
                .WithTag("SRAM_RST", 2, 1)
                .WithTag("I2C_MST_RST", 1, 1)
                .WithReservedBits(0, 1);

            GyroAccelRegisters.LPConfig.Define(this)
                .WithReservedBits(7, 1)
                .WithTag("I2C_MST_CYCLE", 6, 1)
                .WithTag("ACCEL_CYCLE", 5, 1)
                .WithTag("GYRO_CYCLE", 4, 1)
                .WithReservedBits(0, 4);

            GyroAccelRegisters.PowerManagement1.Define(this)
                .WithTag("DEVICE_RESET", 7, 1)
                .WithTag("SLEEP", 6, 1)
                .WithTag("LP_EN", 5, 1)
                .WithReservedBits(4, 1)
                .WithTag("TEMP_DIS", 3, 1)
                .WithTag("CLKSEL", 0, 3);

            GyroAccelRegisters.PowerManagement2.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("DISABLE_ACCEL", 3, 3)
                .WithTag("DISABLE_GYRO", 0, 3);

            GyroAccelRegisters.InterruptPinConfig.Define(this)
                .WithTag("INT1_ACTL", 7, 1)
                .WithTag("INT1_OPEN", 6, 1)
                .WithTag("INT1_LATCH_INT_EN", 5, 1)
                .WithTag("INT_ANYRD_2CLEAR", 4, 1)
                .WithTag("ACTL_FSYNC", 3, 1)
                .WithTag("FSYNC_INT_MODE_EN", 2, 1)
                .WithTag("BYPASS_EN", 1, 1)
                .WithReservedBits(0, 1);

            GyroAccelRegisters.InterruptEnable.Define(this)
                .WithTag("REG_WOF_EN", 7, 1)
                .WithReservedBits(4, 3)
                .WithTag("WOM_INT_EN", 3, 1)
                .WithTag("PLL_RDY_EN", 2, 1)
                .WithTag("DMP_INT1_EN", 1, 1)
                .WithTag("I2C_MST_INT_EN", 0, 1);

            GyroAccelRegisters.InterruptEnable1.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("RAW_DATA_0_RDY_EN", 0, 1);

            GyroAccelRegisters.InterruptEnable2.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_OVERFLOW_EN", 0, 5);

            GyroAccelRegisters.InterruptEnable3.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_WM_EN", 0, 5);

            GyroAccelRegisters.I2CMstStatus.Define(this)
                .WithTag("PASS_THROUGH", 7, 1)
                .WithTag("I2C_SLV4_DONE", 6, 1)
                .WithTag("I2C_LOST_ARB", 5, 1)
                .WithTag("I2C_SLV4_NACK", 4, 1)
                .WithTag("I2C_SLV3_NACK", 3, 1)
                .WithTag("I2C_SLV2_NACK", 2, 1)
                .WithTag("I2C_SLV1_NACK", 1, 1)
                .WithTag("I2C_SLV0_NACK", 0, 1);

            GyroAccelRegisters.InterruptStatus.Define(this)
                .WithReservedBits(4, 4)
                .WithTag("WOM_INT", 3, 1)
                .WithTag("PLL_RDY_INT", 2, 1)
                .WithTag("DMP_INT1", 1, 1)
                .WithTag("I2C_MST_INT", 0, 1);

            GyroAccelRegisters.InterruptStatus1.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("RAW_DATA_0_RDY_INT", 0, 1);

            GyroAccelRegisters.InterruptStatus2.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_OVERFLOW_INT", 0, 5);

            GyroAccelRegisters.InterruptStatus3.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_WM_INT", 0, 5);

            GyroAccelRegisters.DelayTimeHigh.Define(this)
                .WithTag("DELAY_TIMEH", 0, 8);

            GyroAccelRegisters.DelayTimeLow.Define(this)
                .WithTag("DELAY_TIMEL", 0, 8);

            GyroAccelRegisters.AccelerationXOutHigh.Define(this)
                .WithTag("ACCEL_XOUT_H", 0, 8);

            GyroAccelRegisters.AccelerationXOutLow.Define(this)
                .WithTag("ACCEL_XOUT_L", 0, 8);

            GyroAccelRegisters.AccelerationYOutHigh.Define(this)
                .WithTag("ACCEL_YOUT_H", 0, 8);

            GyroAccelRegisters.AccelerationYOutLow.Define(this)
                .WithTag("ACCEL_YOUT_L", 0, 8);

            GyroAccelRegisters.AccelerationZOutHigh.Define(this)
                .WithTag("ACCEL_ZOUT_H", 0, 8);

            GyroAccelRegisters.AccelerationZOutLow.Define(this)
                .WithTag("ACCEL_ZOUT_L", 0, 8);

            GyroAccelRegisters.GyroscopeXOutHigh.Define(this)
                .WithTag("GYRO_XOUT_H", 0, 8);

            GyroAccelRegisters.GyroscopeXOutLow.Define(this)
                .WithTag("GYRO_XOUT_L", 0, 8);

            GyroAccelRegisters.GyroscopeYOutHigh.Define(this)
                .WithTag("GYRO_YOUT_H", 0, 8);

            GyroAccelRegisters.GyroscopeYOutLow.Define(this)
                .WithTag("GYRO_YOUT_L", 0, 8);

            GyroAccelRegisters.GyroscopeZOutHigh.Define(this)
                .WithTag("GYRO_ZOUT_H", 0, 8);

            GyroAccelRegisters.GyroscopeZOutLow.Define(this)
                .WithTag("GYRO_ZOUT_L", 0, 8);

            GyroAccelRegisters.TemperatureOutHigh.Define(this)
                .WithTag("TEMP_OUT_H", 0, 8);

            GyroAccelRegisters.TemperatureOutLow.Define(this)
                .WithTag("TEMP_OUT_L", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData0.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_00", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData1.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_01", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData2.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_02", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData3.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_03", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData4.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_04", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData5.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_05", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData6.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_06", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData7.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_07", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData8.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_08", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData9.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_09", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData10.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_10", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData11.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_11", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData12.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_12", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData13.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_13", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData14.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_14", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData15.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_15", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData16.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_16", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData17.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_17", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData18.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_18", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData19.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_19", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData20.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_20", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData21.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_21", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData22.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_22", 0, 8);

            GyroAccelRegisters.ExtSlvSensorData23.Define(this)
                .WithTag("EXT_SLV_SENS_DATA_23", 0, 8);

            GyroAccelRegisters.FifoEnable1.Define(this)
                .WithReservedBits(4, 4)
                .WithTag("SLV_3_FIFO_EN", 3, 1)
                .WithTag("SLV_2_FIFO_EN", 2, 1)
                .WithTag("SLV_1_FIFO_EN", 1, 1)
                .WithTag("SLV_0_FIFO_EN", 0, 1);

            GyroAccelRegisters.FifoEnable2.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("ACCEL_FIFO_EN", 4, 1)
                .WithTag("GYRO_Z_FIFO_EN", 3, 1)
                .WithTag("GYRO_Y_FIFO_EN", 2, 1)
                .WithTag("GYRO_X_FIFO_EN", 1, 1)
                .WithTag("TEMP_FIFO_EN", 0, 1);

            GyroAccelRegisters.FifoReset.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_RESET", 0, 5);

            GyroAccelRegisters.FifoMode.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_MODE", 0, 5);

            GyroAccelRegisters.FifoCountHigh.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("FIFO_CNT", 0, 5);

            GyroAccelRegisters.FifoCountLow.Define(this)
                .WithTag("FIFO_CNT", 0, 8);

            GyroAccelRegisters.FifoRW.Define(this)
                .WithTag("FIFO_R_W", 0, 8);

            GyroAccelRegisters.DataReadyStatus.Define(this)
                .WithTag("WOF_STATUS", 7, 1)
                .WithReservedBits(4, 3)
                .WithTag("RAW_DATA_RDY", 0, 4);

            GyroAccelRegisters.FifoConfig.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("FIFO_CFG", 0, 1);

            GyroAccelRegisters.RegisterBankSelection.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("USER_BANK", 4, 2)
                .WithReservedBits(0, 4);

            GyroAccelRegisters.SelfTestXGyroscope.Define(this)
                .WithTag("XG_ST_DATA", 0, 8);

            GyroAccelRegisters.SelfTestYGyroscope.Define(this)
                .WithTag("YG_ST_DATA", 0, 8);

            GyroAccelRegisters.SelfTestZGyroscope.Define(this)
                .WithTag("ZG_ST_DATA", 0, 8);

            GyroAccelRegisters.SelfTestXAcceleration.Define(this)
                .WithTag("XA_ST_DATA", 0, 8);

            GyroAccelRegisters.SelfTestYAcceleration.Define(this)
                .WithTag("YA_ST_DATA", 0, 8);

            GyroAccelRegisters.SelfTestZAcceleration.Define(this)
                .WithTag("ZA_ST_DATA", 0, 8);

            GyroAccelRegisters.XAxisOffsetHigh.Define(this)
                .WithTag("XA_OFFS", 0, 8);

            GyroAccelRegisters.XAxisOffsetLow.Define(this)
                .WithTag("XA_OFFS", 1, 7)
                .WithReservedBits(0, 1);

            GyroAccelRegisters.YAxisOffsetHigh.Define(this)
                .WithTag("YA_OFFS", 0, 8);

            GyroAccelRegisters.YAxisOffsetLow.Define(this)
                .WithTag("YA_OFFS", 1, 7)
                .WithReservedBits(0, 1);

            GyroAccelRegisters.ZAxisOffsetHigh.Define(this)
                .WithTag("ZA_OFFS", 0, 8);

            GyroAccelRegisters.ZAxisOffsetLow.Define(this)
                .WithTag("ZA_OFFS", 1, 7)
                .WithReservedBits(0, 1);

            GyroAccelRegisters.TimebaseCorrectionPLL.Define(this)
                .WithTag("TBC_PLL", 0, 8);

            GyroAccelRegisters.GyroscopeSampleRateDivider.Define(this)
                .WithTag("GYRO_SMPLRT_DIV", 0, 8);

            GyroAccelRegisters.GyroscopeConfig1.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("GYRO_DLPFCFG", 3, 3)
                .WithTag("GYRO_FS_SEL", 1, 2)
                .WithTag("GYRO_FCHOICE", 0, 1);

            GyroAccelRegisters.GyroscopeConfig2.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("XGYRO_CTEN", 5, 1)
                .WithTag("YGYRO_CTEN", 4, 1)
                .WithTag("ZGYRO_CTEN", 3, 1)
                .WithTag("GYRO_AVGCFG", 0, 3);

            GyroAccelRegisters.GyroscopeXOffsetCancellationHigh.Define(this)
                .WithTag("X_OFFS_USER", 0, 8);

            GyroAccelRegisters.GyroscopeXOffsetCancellationLow.Define(this)
                .WithTag("X_OFFS_USER", 0, 8);

            GyroAccelRegisters.GyroscopeYOffsetCancellationHigh.Define(this)
                .WithTag("Y_OFFS_USER", 0, 8);

            GyroAccelRegisters.GyroscopeYOffsetCancellationLow.Define(this)
                .WithTag("Y_OFFS_USER", 0, 8);

            GyroAccelRegisters.GyroscopeZOffsetCancellationHigh.Define(this)
                .WithTag("Z_OFFS_USER", 0, 8);

            GyroAccelRegisters.GyroscopeZOffsetCancellationLow.Define(this)
                .WithTag("Z_OFFS_USER", 0, 8);

            GyroAccelRegisters.EnableODRStartTimeAlignment.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("ODR_ALIGN_EN", 0, 1);

            GyroAccelRegisters.AccelerationSampleRateDivider1.Define(this)
                .WithReservedBits(4, 4)
                .WithTag("ACCEL_SMPLRT_DIV", 0, 4);

            GyroAccelRegisters.AccelerationSampleRateDivider2.Define(this)
                .WithTag("ACCEL_SMPLRT_DIV", 0, 8);

            GyroAccelRegisters.AccelerationIntelControl.Define(this)
                .WithReservedBits(2, 6)
                .WithTag("ACCEL_INTEL_EN", 1, 1)
                .WithTag("ACCEL_INTEL_MODE_INT", 0, 1);

            GyroAccelRegisters.AccelerationWakeOnMotionThreshold.Define(this)
                .WithTag("WOM_THRESHOLD", 0, 8);

            GyroAccelRegisters.AccelerationConfig.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("ACCEL_DLPFCFG", 3, 3)
                .WithTag("ACCEL_FS_SEL", 1, 2)
                .WithTag("ACCEL_FCHOICE", 0, 1);

            GyroAccelRegisters.AccelerationConfig2.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("AX_ST_EN_REG", 4, 1)
                .WithTag("AY_ST_EN_REG", 3, 1)
                .WithTag("AZ_ST_EN_REG", 2, 1)
                .WithTag("DEC3_CFG", 0, 2);

            GyroAccelRegisters.FSYNCConfig.Define(this)
                .WithTag("DELAY_TIME_EN", 7, 1)
                .WithReservedBits(6, 1)
                .WithTag("WOF_DEGLITCH_EN", 5, 1)
                .WithTag("WOF_EDGE_INT", 4, 1)
                .WithTag("EXT_SYNC_SET", 0, 4);

            GyroAccelRegisters.TemperatureConfig.Define(this)
                .WithReservedBits(3, 5)
                .WithTag("TEMP_DLPFCFG", 0, 3);

            GyroAccelRegisters.ModControl.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("REG_LP_DMP_EN", 0, 1);

            GyroAccelRegisters.I2CIDRMasterConfig.Define(this)
                .WithReservedBits(4, 4)
                .WithTag("I2C_MST_ODR_CONFIG", 0, 4);

            GyroAccelRegisters.I2CMasterControl.Define(this)
                .WithTag("MULT_MST_EN", 7, 1)
                .WithReservedBits(5, 2)
                .WithTag("I2C_MST_P_NSR", 4, 1)
                .WithTag("I2C_MST_CLK", 0, 4);

            GyroAccelRegisters.I2CMasterDelayControl.Define(this)
                .WithTag("DELAY_ES_SHADOW", 7, 1)
                .WithReservedBits(5, 2)
                .WithTag("I2C_SLV4_DELAY_EN", 4, 1)
                .WithTag("I2C_SLV3_DELAY_EN", 3, 1)
                .WithTag("I2C_SLV2_DELAY_EN", 2, 1)
                .WithTag("I2C_SLV1_DELAY_EN", 1, 1)
                .WithTag("I2C_SLV0_DELAY_EN", 0, 1);

            GyroAccelRegisters.I2CSlave0Address.Define(this)
                .WithTag("I2C_SLV0_RNW", 7, 1)
                .WithTag("I2C_ID_0", 0, 7);

            GyroAccelRegisters.I2CSlave0Register.Define(this)
                .WithTag("I2C_SLV0_REG", 0, 8);

            GyroAccelRegisters.I2CSlave0Control.Define(this)
                .WithTag("I2C_SLV0_EN", 7, 1)
                .WithTag("I2C_SLV0_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV0_REG_DIS", 5, 1)
                .WithTag("I2C_SLV0_GRP", 4, 1)
                .WithTag("I2C_SLV0_LENG", 0, 4);

            GyroAccelRegisters.I2CSlave0DataOut.Define(this)
                .WithTag("I2C_SLV0_DO", 0, 8);

            GyroAccelRegisters.I2CSlave1Address.Define(this)
                .WithTag("I2C_SLV1_RNW", 7, 1)
                .WithTag("I2C_ID_1", 0, 7);

            GyroAccelRegisters.I2CSlave1Register.Define(this)
                .WithTag("I2C_SLV1_REG", 0, 8);

            GyroAccelRegisters.I2CSlave1Control.Define(this)
                .WithTag("I2C_SLV1_EN", 7, 1)
                .WithTag("I2C_SLV1_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV1_REG_DIS", 5, 1)
                .WithTag("I2C_SLV1_GRP", 4, 1)
                .WithTag("I2C_SLV1_LENG", 0, 4);

            GyroAccelRegisters.I2CSlave1DataOut.Define(this)
                .WithTag("I2C_SLV1_DO", 0, 8);

            GyroAccelRegisters.I2CSlave2Address.Define(this)
                .WithTag("I2C_SLV2_RNW", 7, 1)
                .WithTag("I2C_ID_2", 0, 7);

            GyroAccelRegisters.I2CSlave2Register.Define(this)
                .WithTag("I2C_SLV2_REG", 0, 8);

            GyroAccelRegisters.I2CSlave2Control.Define(this)
                .WithTag("I2C_SLV2_EN", 7, 1)
                .WithTag("I2C_SLV2_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV2_REG_DIS", 5, 1)
                .WithTag("I2C_SLV2_GRP", 4, 1)
                .WithTag("I2C_SLV2_LENG", 0, 4);

            GyroAccelRegisters.I2CSlave2DataOut.Define(this)
                .WithTag("I2C_SLV2_DO", 0, 8);

            GyroAccelRegisters.I2CSlave3Address.Define(this)
                .WithTag("I2C_SLV3_RNW", 7, 1)
                .WithTag("I2C_ID_3", 0, 7);

            GyroAccelRegisters.I2CSlave3Register.Define(this)
                .WithTag("I2C_SLV3_REG", 0, 8);

            GyroAccelRegisters.I2CSlave3Control.Define(this)
                .WithTag("I2C_SLV3_EN", 7, 1)
                .WithTag("I2C_SLV3_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV3_REG_DIS", 5, 1)
                .WithTag("I2C_SLV3_GRP", 4, 1)
                .WithTag("I2C_SLV3_LENG", 0, 4);

            GyroAccelRegisters.I2CSlave3DataOut.Define(this)
                .WithTag("I2C_SLV3_DO", 0, 8);

            GyroAccelRegisters.I2CSlave4Address.Define(this)
                .WithTag("I2C_SLV4_RNW", 7, 1)
                .WithTag("I2C_ID_4", 0, 7);

            GyroAccelRegisters.I2CSlave4Register.Define(this)
                .WithTag("I2C_SLV4_REG", 0, 8);

            GyroAccelRegisters.I2CSlave4Control.Define(this)
                .WithTag("I2C_SLV4_EN", 7, 1)
                .WithTag("I2C_SLV4_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV4_REG_DIS", 5, 1)
                .WithTag("I2C_SLV4_DLY", 0, 5);

            GyroAccelRegisters.I2CSlave4DataOut.Define(this)
                .WithTag("I2C_SLV4_DO", 0, 8);

            GyroAccelRegisters.I2CSlave4DataIn.Define(this)
                .WithTag("I2C_SLV4_DI", 0, 8);

            MagnetometerRegisters.DevideID.Define(this)
                .WithReservedBits(0, 8);

            MagnetometerRegisters.Status1.Define(this)
                .WithReservedBits(2, 6)
                .WithTag("DOR", 1, 1)
                .WithTag("DRDY", 0, 1);

            MagnetometerRegisters.XAxisMeasurementDataLower.Define(this)
                .WithTag("HX_Low", 0, 8);

            MagnetometerRegisters.XAxisMeasurementDataUpper.Define(this)
                .WithTag("HX_High", 0, 8);

            MagnetometerRegisters.YAxisMeasurementDataLower.Define(this)
                .WithTag("HY_Low", 0, 8);

            MagnetometerRegisters.YAxisMeasurementDataUpper.Define(this)
                .WithTag("HY_High", 0, 8);

            MagnetometerRegisters.ZAxisMeasurementDataLower.Define(this)
                .WithTag("ZY_Low", 0, 8);

            MagnetometerRegisters.ZAxisMeasurementDataUpper.Define(this)
                .WithTag("ZY_High", 0, 8);

            MagnetometerRegisters.Status2.Define(this)
                .WithReservedBits(7, 1)
                .WithTag("RSV30", 6, 1)
                .WithTag("RCV29", 5, 1)
                .WithTag("RSV28", 4, 1)
                .WithTag("HOFL", 3, 1)
                .WithReservedBits(0, 3);

            MagnetometerRegisters.Control2.Define(this)
                .WithReservedBits(5, 3)
                .WithTag("MODE4", 4, 1)
                .WithTag("MODE3", 3, 1)
                .WithTag("MODE2", 2, 1)
                .WithTag("MODE1", 1, 1)
                .WithTag("MODE0", 0, 1);

            MagnetometerRegisters.Control3.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("SRST", 0, 1);

            MagnetometerRegisters.Test1.Define(this)
                .WithReservedBits(0, 8);

            MagnetometerRegisters.Test2.Define(this)
                .WithReservedBits(0, 8);
        }

        private enum GyroAccelRegisters : byte
        {
            // USER BANK 0 REGISTERS
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

            // USER BANK 1 REGISTERS
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

            // USER BANK 2 REGISTERS
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

            // USER BANK 3 REGISTERS
            I2CIDRMasterConfig = 0x0,
            I2CMasterControl = 0x1,
            I2CMasterDelayControl = 0x2,
            I2CSlave0Address = 0x3,
            I2CSlave0Register = 0x4,
            I2CSlave0Control = 0x5,
            I2CSlave0DataOut = 0x6,
            I2CSlave1Address = 0x7,
            I2CSlave1Register = 0x8,
            I2CSlave1Control = 0x9,
            I2CSlave1DataOut = 0xA,
            I2CSlave2Address = 0xB,
            I2CSlave2Register = 0xC,
            I2CSlave2Control = 0xD,
            I2CSlave2DataOut = 0xE,
            I2CSlave3Address = 0xF,
            I2CSlave3Register = 0x10,
            I2CSlave3Control = 0x11,
            I2CSlave3DataOut = 0x12,
            I2CSlave4Address = 0x13,
            I2CSlave4Register = 0x14,
            I2CSlave4Control = 0x15,
            I2CSlave4DataOut = 0x16,
            I2CSlave4DataIn = 0x17,
        }

        private enum MagnetometerRegisters : byte
        {
            DevideID = 0x1,
            Status1 = 0x10,
            XAxisMeasurementDataLower = 0x11,
            XAxisMeasurementDataUpper = 0x12,
            YAxisMeasurementDataLower = 0x13,
            YAxisMeasurementDataUpper = 0x14,
            ZAxisMeasurementDataLower = 0x15,
            ZAxisMeasurementDataUpper = 0x16,
            Status2 = 0x18,
            Control2 = 0x31,
            Control3 = 0x32,
            Test1 = 0x33,
            Test2 = 0x34
        }
    }
}
