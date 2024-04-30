//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948
    {
        private bool TryGetI2CPeripherial(out II2CPeripheral pheriperial)
        {
            if(!i2cContainer.TryGetByAddress((int)slaveAddress0.Value, out pheriperial))
            {
                this.WarningLog("Selecting unconnected IIC slave address: 0x{0:X}", slaveAddress0.Value);
                return false;
            }
            this.DebugLog("Selected slave address 0x{0:X}", slaveAddress0.Value);
            return true;
        }

        private void WriteI2CPeripherial(II2CPeripheral selectedI2CSlave, byte[] data)
        {
            selectedI2CSlave.Write(data);
            selectedI2CSlave.FinishTransmission();
        }

        private void ReadI2CPeripherial(II2CPeripheral selectedI2CSlave, int length)
        {
            if(length > externalSlaveSensorData.Length)
            {
                this.Log(LogLevel.Warning, "Requested read length is greater than the number of available external sensor data registers. Clamping to the maximum available length.");
                length = externalSlaveSensorData.Length;
            }
            byte[] data = selectedI2CSlave.Read(length);
            selectedI2CSlave.FinishTransmission();
            for(int i = 0; i < data.Length; i++)
            {
                externalSlaveSensorData[i].Value = data[i];
            }
        }

        private void ReadSensorData()
        {
            if(!TryGetI2CPeripherial(out var selectedI2CSlave))
            {
                return;
            }
            WriteI2CPeripherial(selectedI2CSlave, new byte[] { (byte)slaveTransactionRegisterAddress0.Value } );
            ReadI2CPeripherial(selectedI2CSlave, (int)slaveTransferLength0.Value );
        }

        private void DefineGyroAccelUserBank3Registers()
        {
            GyroAccelUserBank3Registers.I2CIDRMasterConfig.Define(gyroAccelUserBank3Registers)
                .WithReservedBits(4, 4)
                .WithTag("I2C_MST_ODR_CONFIG", 0, 4)
            ;

            GyroAccelUserBank3Registers.I2CMasterControl.Define(gyroAccelUserBank3Registers)
                .WithTag("MULT_MST_EN", 7, 1)
                .WithReservedBits(5, 2)
                .WithTag("I2C_MST_P_NSR", 4, 1)
                .WithTag("I2C_MST_CLK", 0, 4)
            ;

            GyroAccelUserBank3Registers.I2CMasterDelayControl.Define(gyroAccelUserBank3Registers)
                .WithTag("DELAY_ES_SHADOW", 7, 1)
                .WithReservedBits(5, 2)
                .WithTag("I2C_SLV4_DELAY_EN", 4, 1)
                .WithTag("I2C_SLV3_DELAY_EN", 3, 1)
                .WithTag("I2C_SLV2_DELAY_EN", 2, 1)
                .WithTag("I2C_SLV1_DELAY_EN", 1, 1)
                .WithTag("I2C_SLV0_DELAY_EN", 0, 1)
            ;

            GyroAccelUserBank3Registers.I2CSlave0Address.Define(gyroAccelUserBank3Registers)
                .WithFlag(7, out slaveRWBit0, name: "I2C_SLV0_RNW")
                .WithValueField(0, 7, out slaveAddress0, name: "I2C_ID_0")
            ;

            GyroAccelUserBank3Registers.I2CSlave0Register.Define(gyroAccelUserBank3Registers)
                .WithValueField(0, 8, out slaveTransactionRegisterAddress0, name: "I2C_SLV0_REG")
            ;

            GyroAccelUserBank3Registers.I2CSlave0Control.Define(gyroAccelUserBank3Registers)
                .WithFlag(7, out var slaveEnable0, name: "I2C_SLV0_EN",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                magFeederThread?.Stop();
                                return;
                            }
                            magFeederThread?.Stop();
                            var machine = this.GetMachine();
                            Action feedSample = () =>
                            {
                                ReadSensorData();
                            };
                            Func<bool> stopCondition = () => false;
                            magFeederThread = machine.ObtainManagedThread(feedSample, (uint)InternalSampleRateHz, "Magnetometer stream thread", this, stopCondition);
                            this.Log(LogLevel.Debug, "Starting reading magnetometer samples at frequency {0}Hz", InternalSampleRateHz);
                            magFeederThread.Start();
                        })
                .WithTag("I2C_SLV0_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV0_REG_DIS", 5, 1)
                .WithTag("I2C_SLV0_GRP", 4, 1)
                .WithValueField(0, 4, out slaveTransferLength0, name: "I2C_SLV0_LENG")
            ;

            GyroAccelUserBank3Registers.I2CSlave0DataOut.Define(gyroAccelUserBank3Registers)
                .WithValueField(0, 8, out slaveDataOut0, name: "I2C_SLV0_DO",
                    writeCallback: (_, val) =>
                    {
                        if(!TryGetI2CPeripherial(out var selectedI2CSlave))
                        {
                            return;
                        }
                        WriteI2CPeripherial(selectedI2CSlave, new byte[] { (byte)slaveTransactionRegisterAddress0.Value, (byte)val } );
                    })
            ;

            GyroAccelUserBank3Registers.I2CSlave1Address.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV1_RNW", 7, 1)
                .WithTag("I2C_ID_1", 0, 7)
            ;

            GyroAccelUserBank3Registers.I2CSlave1Register.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV1_REG", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave1Control.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV1_EN", 7, 1)
                .WithTag("I2C_SLV1_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV1_REG_DIS", 5, 1)
                .WithTag("I2C_SLV1_GRP", 4, 1)
                .WithTag("I2C_SLV1_LENG", 0, 4)
            ;

            GyroAccelUserBank3Registers.I2CSlave1DataOut.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV1_DO", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave2Address.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV2_RNW", 7, 1)
                .WithTag("I2C_ID_2", 0, 7)
            ;

            GyroAccelUserBank3Registers.I2CSlave2Register.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV2_REG", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave2Control.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV2_EN", 7, 1)
                .WithTag("I2C_SLV2_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV2_REG_DIS", 5, 1)
                .WithTag("I2C_SLV2_GRP", 4, 1)
                .WithTag("I2C_SLV2_LENG", 0, 4)
            ;

            GyroAccelUserBank3Registers.I2CSlave2DataOut.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV2_DO", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave3Address.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV3_RNW", 7, 1)
                .WithTag("I2C_ID_3", 0, 7)
            ;

            GyroAccelUserBank3Registers.I2CSlave3Register.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV3_REG", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave3Control.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV3_EN", 7, 1)
                .WithTag("I2C_SLV3_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV3_REG_DIS", 5, 1)
                .WithTag("I2C_SLV3_GRP", 4, 1)
                .WithTag("I2C_SLV3_LENG", 0, 4)
            ;

            GyroAccelUserBank3Registers.I2CSlave3DataOut.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV3_DO", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave4Address.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV4_RNW", 7, 1)
                .WithTag("I2C_ID_4", 0, 7)
            ;

            GyroAccelUserBank3Registers.I2CSlave4Register.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV4_REG", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave4Control.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV4_EN", 7, 1)
                .WithTag("I2C_SLV4_BYTE_SW", 6, 1)
                .WithTag("I2C_SLV4_REG_DIS", 5, 1)
                .WithTag("I2C_SLV4_DLY", 0, 5)
            ;

            GyroAccelUserBank3Registers.I2CSlave4DataOut.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV4_DO", 0, 8)
            ;

            GyroAccelUserBank3Registers.I2CSlave4DataIn.Define(gyroAccelUserBank3Registers)
                .WithTag("I2C_SLV4_DI", 0, 8)
            ;

            DefineBankSelectRegister(gyroAccelUserBank3Registers);
        }

        private IManagedThread magFeederThread;

        private IValueRegisterField slaveAddress0;
        private IFlagRegisterField slaveRWBit0;
        private IValueRegisterField slaveTransactionRegisterAddress0;
        private IValueRegisterField slaveTransferLength0;
        private IValueRegisterField slaveDataOut0;

        private enum GyroAccelUserBank3Registers : byte
        {
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

        private enum I2CTransactionDirection
        {
            Unset,
            Read,
            Write,
        }
    }
}
