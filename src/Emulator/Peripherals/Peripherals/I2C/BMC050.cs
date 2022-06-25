//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class BMC050 : II2CPeripheral
    {
        public BMC050()
        {
            Reset();
        }

        // TODO: how do call the generate sensor data functions?
        // Now: After receiving register address to read. 
        // Step through after read and init?
        // Thread always running which sleeps with given frequencies?

        public void Reset()
        {
            this.Log(LogLevel.Noisy, "Reset registers");
            // TODO: Control, status and image registers are reset to values stored in the EEPROM.
            acc_z_msb = 0;
            acc_z_lsb = 0;
            acc_y_msb = 0;
            acc_y_lsb = 0;
            acc_x_msb = 0;
            acc_x_lsb = 0;
            temperature = 0;
            statusReg1 = 0;
            statusReg2 = 0;
            statusReg3 = 0;
            statusReg4 = 0;
            gRange = g_RangeModes.Range2g;
            bandwidth = BandwidthModes.Bandwidth8 | BandwidthModes.Bandwidth9;
            power = 0;
            daqCtrl = 0;
            irqCtrl1 = 0;
            irqCtrl2 = 0;
            irqMap1 = 0;
            irqMap2 = 0;
            irqMap3 = 0;
            irqSource = 0;
            irqBehaviour = 0;
            irqMode = 0;
            lowDur = 0x09;
            lowTh = 0x30;
            hy = 0x81;
            highDur = 0x0F;
            highTh = 0xC0;
            slopeDur = 0;
            slopeTh = 0x14;
            tapConfig1 = 0x04;
            tapConfig2 = 0x0A;
            orientConfig1 = 0x18;
            orientConfig2 = 0x08;
            flatConfig1 = 0x08;
            flatConfig2 = 0x10;
            selfTest = SelfTestModes.Idle;
            eepromCtrl = 0x04;
            interfaceConfig = 0;
            offsetCompensation = 0;
            offsetTarget = 0;
            offsetFilteredX = 0;
            offsetFilteredY = 0;
            offsetFilteredZ = 0;
            offsetUnfilteredX = 0;
            offsetUnfilteredY = 0;
            offsetUnfilteredZ = 0;
            mag_z_msb = 0;
            mag_z_lsb = 0x01; // Default for self test result bit 0 is 1
            mag_y_msb = 0;
            mag_y_lsb = 0x01; // Default for self test result bit 0 is 1
            mag_x_msb = 0;
            mag_x_lsb = 0x01; // Default for self test result bit 0 is 1
            mag_rhall_lsb = 0;
            mag_rhall_msb = 0;
            magIntrStatus = 0;
            magCtrl = 0x01;
            magOpModeCtrl = 0x6;
            magIntrCtrl1 = 0x3F;
            magIntrCtrl2 = 0x07;
            magLowTh = 0;
            magHighTh = 0;
            magRepXY = 0;
            magRepZ = 0;
            state = (uint)States.Idle;
            registerAddress = 0;
            registerData = 0;
            sensorFlipState = (byte)SensorFlipStates.XYZ_111;
        }

        public void Write(byte[] data)
        {
            // Parse the list bytes
            if(data.Length < 2)
            {
                // Must always have mode and register address in list
                this.Log(LogLevel.Warning, "Write - too few elements in list ({0}) - must be at least two", data.Length);
                return;
            }
            this.NoisyLog("Write {0}", data.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
            // First byte sets the device state
            state = data[0];
            this.Log(LogLevel.Noisy, "State changed to {0}", (States)state);
            // Second byte is always register address
            registerAddress = data[1];
            if(data.Length == 3)
            {
                registerData = data[2];
            }
            switch((States)state)
            {
            case States.ReceivingData:
                switch((Registers)registerAddress)
                {
                case Registers.SoftReset:
                    // Soft Reset command for Bosch BMC050, BMA180 and BMP180
                    if(registerData == 0xB6)
                    {
                        Reset();
                    }
                    break;
                case Registers.g_Range:
                    switch((g_RangeModes)registerData)
                    {
                    case g_RangeModes.Range2g:
                    case g_RangeModes.Range4g:
                    case g_RangeModes.Range8g:
                    case g_RangeModes.Range16g:
                        gRange = (g_RangeModes)registerData;
                        break;
                    default:
                        // Other values are not allowed ans shall default to 2g selection
                        gRange = g_RangeModes.Range2g;
                        break;
                    }
                    break;
                case Registers.Bandwidth:
                    bandwidth = (BandwidthModes)registerData;
                    break;
                case Registers.Power:
                    // TODO: Power functionality not yet implemented (Suspend, Sleep duration etc)
                    power = registerData;
                    break;
                case Registers.DaqCtrl:
                    // TODO: DAQ control functionality not implemented
                    daqCtrl = registerData;
                    break;
                case Registers.IrqCtrl1:
                    // TODO: interrupt functionality not implemented
                    irqCtrl1 = registerData;
                    break;
                case Registers.IrqCtrl2:
                    // TODO: interrupt functionality not implemented
                    irqCtrl2 = registerData;
                    break;
                case Registers.IrqMap1:
                    // TODO: interrupt functionality not implemented
                    irqMap1 = registerData;
                    break;
                case Registers.IrqMap2:
                    // TODO: interrupt functionality not implemented
                    irqMap2 = registerData;
                    break;
                case Registers.IrqMap3:
                    // TODO: interrupt functionality not implemented
                    irqMap3 = registerData;
                    break;
                case Registers.IrqSource:
                    // TODO: interrupt functionality not implemented
                    irqSource = registerData;
                    break;
                case Registers.IrqBehaviour:
                    // TODO: interrupt functionality not implemented
                    irqBehaviour = registerData;
                    break;
                case Registers.IrqMode:
                    // TODO: interrupt functionality not implemented
                    irqMode = registerData;
                    break;
                case Registers.LowDur:
                    // TODO: interrupt functionality not implemented
                    lowDur = registerData;
                    break;
                case Registers.LowTh:
                    // TODO: interrupt functionality not implemented
                    lowTh = registerData;
                    break;
                case Registers.Hy:
                    // TODO: interrupt functionality not implemented
                    hy = registerData;
                    break;
                case Registers.HighDur:
                    // TODO: interrupt functionality not implemented
                    highDur = registerData;
                    break;
                case Registers.HighTh:
                    // TODO: interrupt functionality not implemented
                    highTh = registerData;
                    break;
                case Registers.SlopeDur:
                    // TODO: interrupt functionality not implemented
                    slopeDur = registerData;
                    break;
                case Registers.SlopeTh:
                    // TODO: interrupt functionality not implemented
                    slopeTh = registerData;
                    break;
                case Registers.TapConfig1:
                    // TODO: interrupt functionality not implemented
                    tapConfig1 = registerData;
                    break;
                case Registers.TapConfig2:
                    // TODO: interrupt functionality not implemented
                    tapConfig2 = registerData;
                    break;
                case Registers.OrientConfig1:
                    // TODO: interrupt functionality not implemented
                    orientConfig1 = registerData;
                    break;
                case Registers.OrientConfig2:
                    // TODO: interrupt functionality not implemented
                    orientConfig2 = registerData;
                    break;
                case Registers.FlatConfig1:
                    // TODO: interrupt functionality not implemented
                    flatConfig1 = registerData;
                    break;
                case Registers.FlatConfig2:
                    // TODO: interrupt functionality not implemented
                    flatConfig2 = registerData;
                    break;
                case Registers.SelfTest:
                    // TODO: self test not implemented
                    selfTest = (SelfTestModes)registerData;
                    break;
                case Registers.EEPROMCtrl:
                    // TODO: EEPROM handling not implemented
                    eepromCtrl = registerData;
                    break;
                case Registers.InterfaceConfig:
                    // TODO: watchdog on interfaces not implemented
                    interfaceConfig = registerData;
                    break;
                case Registers.OffsetCompensation:
                    // Setting bit 7 resets the register to value 0 
                    if((registerData & 0x80) == 0x80)
                    {
                        offsetCompensation = 0;
                    }
                    else
                    {
                        offsetCompensation = registerData;
                    }
                    break;
                case Registers.OffsetTarget:
                    offsetTarget = registerData;
                    break;
                case Registers.OffsetFilteredX:
                    offsetFilteredX = registerData;
                    break;
                case Registers.OffsetFilteredY:
                    offsetFilteredY = registerData;
                    break;
                case Registers.OffsetFilteredZ:
                    offsetFilteredZ = registerData;
                    break;
                case Registers.OffsetUnfilteredX:
                    offsetUnfilteredX = registerData;
                    break;
                case Registers.OffsetUnfilteredY:
                    offsetUnfilteredY = registerData;
                    break;
                case Registers.OffsetUnfilteredZ:
                    offsetUnfilteredZ = registerData;
                    break;
                case Registers.MagCtrl:
                    magCtrl = registerData;
                    // Enforce fixed zero bits
                    magCtrl &= 0x87;
                    break;
                case Registers.MagOpModeCtrl:
                    magOpModeCtrl = registerData;
                    break;
                case Registers.MagIntrCtrl1:
                    magIntrCtrl1 = registerData;
                    break;
                case Registers.MagIntrCtrl2:
                    magIntrCtrl2 = registerData;
                    break;
                case Registers.MagLowTh:
                    magLowTh = registerData;
                    break;
                case Registers.MagHighTh:
                    magHighTh = registerData;
                    break;
                case Registers.MagRepXY:
                    magRepXY = registerData;
                    break;
                case Registers.MagRepZ:
                    magRepZ = registerData;
                    break;
                default:
                    this.Log(LogLevel.Noisy, "Register address invalid - no action");
                    break;
                }
                state = (uint)States.Idle;
                this.Log(LogLevel.Noisy, "State changed to Idle");
                break;
            case States.SendingData:
                byte[] result = new byte[1] { 0 };
                switch((Registers)registerAddress)
                {
                case Registers.Acc_X_LSB:
                case Registers.Acc_X_MSB:
                    GetAccelerometerX();
                    GetAccelerometerY();
                    GetAccelerometerZ();
                    result = new byte[6] { 0, 0, 0, 0, 0, 0 };
                    result[0] = acc_x_lsb;
                    result[1] = acc_x_msb;
                    result[2] = acc_y_lsb;
                    result[3] = acc_y_msb;
                    result[4] = acc_z_lsb;
                    result[5] = acc_z_msb;
                    // Read clears new_data_xyz bit
                    acc_x_lsb &= 0xFE;
                    acc_y_lsb &= 0xFE;
                    acc_z_lsb &= 0xFE;
                    // Only flip the sensor after reading ACC_Z registers
                    // Assuming reads of all three axis for each turn
                    FlipSensor();
                    break;
                case Registers.Acc_Y_LSB:
                case Registers.Acc_Y_MSB:
                    GetAccelerometerY();
                    result = new byte[2] { 0, 0 };
                    result[0] = acc_y_lsb;
                    result[1] = acc_y_msb;
                    // Read clears new_data_y bit
                    acc_y_lsb &= 0xFE;
                    break;
                case Registers.Acc_Z_LSB:
                case Registers.Acc_Z_MSB:
                    GetAccelerometerZ();
                    result = new byte[2] { 0, 0 };
                    result[0] = acc_z_lsb;
                    result[1] = acc_z_msb;
                    // Read clears new_data_z bit
                    acc_z_lsb &= 0xFE;
                    // Only flip the sensor after reading ACC_Z registers
                    // Assuming reads of all three axis for each turn
                    FlipSensor();
                    break;
                case Registers.ChipID:
                    // Same Chip ID for Bosch BMC050 and BMA180 
                    result[0] = 0x3;
                    break;
                case Registers.Reserved1:
                    // Reserved register, default value = 0x21, in BMC050 data sheet (aka BMA150 data sheet v1.4)
                    result[0] = 0x21;
                    break;
                case Registers.Reserved2:
                case Registers.Reserved3:
                case Registers.Reserved4:
                case Registers.Reserved5:
                case Registers.Reserved6:
                case Registers.Reserved7:
                case Registers.Reserved8:
                case Registers.Reserved9:
                case Registers.Reserved10:
                case Registers.Reserved11:
                case Registers.Reserved12:
                case Registers.Reserved13:
                case Registers.Reserved14:
                case Registers.Reserved15:
                    // Reserved registers, default value = 0x0, in BMC050 data sheet (aka BMA150 data sheet v1.4)
                    result[0] = 0x0;
                    break;
                case Registers.Temperature:
                    GetTemperature();
                    result[0] = temperature;
                    break;
                case Registers.StatusReg1:
                    result[0] = statusReg1;
                    break;
                case Registers.StatusReg2:
                    result[0] = statusReg2;
                    break;
                case Registers.StatusReg3:
                    result[0] = statusReg3;
                    break;
                case Registers.StatusReg4:
                    result[0] = statusReg4;
                    break;
                case Registers.g_Range:
                    result[0] = (byte)gRange;
                    break;
                case Registers.Bandwidth:
                    result[0] = (byte)bandwidth;
                    break;
                case Registers.Power:
                    result[0] = power;
                    break;
                case Registers.DaqCtrl:
                    result[0] = daqCtrl;
                    break;
                case Registers.SoftReset:
                    result[0] = 0;
                    break;
                case Registers.IrqCtrl1:
                    result[0] = irqCtrl1;
                    break;
                case Registers.IrqCtrl2:
                    result[0] = irqCtrl2;
                    break;
                case Registers.IrqMap1:
                    result[0] = irqMap1;
                    break;
                case Registers.IrqMap2:
                    result[0] = irqMap2;
                    break;
                case Registers.IrqMap3:
                    result[0] = irqMap3;
                    break;
                case Registers.IrqSource:
                    result[0] = irqSource;
                    break;
                case Registers.IrqBehaviour:
                    result[0] = irqBehaviour;
                    break;
                case Registers.IrqMode:
                    result[0] = irqMode;
                    break;
                case Registers.LowDur:
                    result[0] = lowDur;
                    break;
                case Registers.LowTh:
                    result[0] = lowTh;
                    break;
                case Registers.Hy:
                    result[0] = hy;
                    break;
                case Registers.HighDur:
                    result[0] = highDur;
                    break;
                case Registers.HighTh:
                    result[0] = highTh;
                    break;
                case Registers.SlopeDur:
                    result[0] = slopeDur;
                    break;
                case Registers.SlopeTh:
                    result[0] = slopeTh;
                    break;
                case Registers.TapConfig1:
                    result[0] = tapConfig1;
                    break;
                case Registers.TapConfig2:
                    result[0] = tapConfig2;
                    break;
                case Registers.OrientConfig1:
                    result[0] = orientConfig1;
                    break;
                case Registers.OrientConfig2:
                    result[0] = orientConfig2;
                    break;
                case Registers.FlatConfig1:
                    result[0] = flatConfig1;
                    break;
                case Registers.FlatConfig2:
                    result[0] = flatConfig2;
                    break;
                case Registers.SelfTest:
                    result[0] = (byte)selfTest;
                    break;
                case Registers.EEPROMCtrl:
                    result[0] = eepromCtrl;
                    break;
                case Registers.InterfaceConfig:
                    result[0] = interfaceConfig;
                    break;
                case Registers.OffsetCompensation:
                    result[0] = offsetCompensation;
                    break;
                case Registers.OffsetTarget:
                    result[0] = offsetTarget;
                    break;
                case Registers.OffsetFilteredX:
                    result[0] = offsetFilteredX;
                    break;
                case Registers.OffsetFilteredY:
                    result[0] = offsetFilteredY;
                    break;
                case Registers.OffsetFilteredZ:
                    result[0] = offsetFilteredZ;
                    break;
                case Registers.OffsetUnfilteredX:
                    result[0] = offsetUnfilteredX;
                    break;
                case Registers.OffsetUnfilteredY:
                    result[0] = offsetUnfilteredY;
                    break;
                case Registers.OffsetUnfilteredZ:
                    result[0] = offsetUnfilteredZ;
                    break;
                case Registers.MagChipID:
                    // Magnetometer Chip ID can only be read if power ctrl bit is set in MagCtrl (0x4B)
                    if((magCtrl & 0x1) == 0x1)
                    {
                        result[0] = 0x32;
                    }
                    else
                    {
                        result[0] = 0x0;
                    }
                    break;
                case Registers.Mag_X_LSB:
                case Registers.Mag_X_MSB:
                    GetMagnetometerX();
                    GetMagnetometerY();
                    GetMagnetometerZ();
                    GetHallResistance();
                    result = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
                    result[0] = mag_x_lsb;
                    result[1] = mag_x_msb;
                    result[2] = mag_y_lsb;
                    result[3] = mag_y_msb;
                    result[4] = mag_z_lsb;
                    result[5] = mag_z_msb;
                    result[6] = mag_rhall_lsb;
                    result[7] = mag_rhall_msb;
                    break;
                case Registers.Mag_Y_LSB:
                case Registers.Mag_Y_MSB:
                    GetMagnetometerY();
                    result = new byte[2] { 0, 0 };
                    result[0] = mag_y_lsb;
                    result[1] = mag_y_msb;
                    break;
                case Registers.Mag_Z_LSB:
                case Registers.Mag_Z_MSB:
                    GetMagnetometerZ();
                    result = new byte[2] { 0, 0 };
                    result[0] = mag_z_lsb;
                    result[1] = mag_z_msb;
                    break;
                case Registers.Mag_RHALL_LSB:
                case Registers.Mag_RHALL_MSB:
                    GetHallResistance();
                    result = new byte[2] { 0, 0 };
                    result[0] = mag_rhall_lsb;
                    result[1] = mag_rhall_msb;
                    break;
                case Registers.MagIntrStatus:
                    result[0] = magIntrStatus;
                    break;
                case Registers.MagCtrl:
                    result[0] = magCtrl;
                    break;
                case Registers.MagOpModeCtrl:
                    result[0] = magOpModeCtrl;
                    break;
                case Registers.MagIntrCtrl1:
                    result[0] = magIntrCtrl1;
                    break;
                case Registers.MagIntrCtrl2:
                    result[0] = magIntrCtrl2;
                    break;
                case Registers.MagLowTh:
                    result[0] = magLowTh;
                    break;
                case Registers.MagHighTh:
                    result[0] = magHighTh;
                    break;
                case Registers.MagRepXY:
                    result[0] = magRepXY;
                    break;
                case Registers.MagRepZ:
                    result[0] = magRepZ;
                    break;
                default:
                    break;
                }
                sendData = new byte[result.Length + 1];
                result.CopyTo(sendData, 0);
                sendData[result.Length] = GetCRC(data, result);
                break;
            default:
                break;
            }
        }

        public byte[] Read(int count = 0)
        {
            if(sendData == null || sendData.Length < count)
            {
                this.Log(LogLevel.Warning, "Attemped to read {0} bytes, while {1} is available", count, sendData == null ? 0 : sendData.Length);
                return new byte[count];
            }
            else
            {
                this.NoisyLog("Read {0}", sendData.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
                return sendData;
            }
        }

        public void FinishTransmission()
        {
        }

        private double SensorData(double mean, double sigma)
        {
            // mean = mean value of Gaussian (Normal) distribution and sigma = standard deviation
            int sign = random.Next(10);
            double x = 0.0;
            if(sign > 5)
            {
                x = mean * (1.0 + random.NextDouble() / (2 * sigma));
            }
            else
            {
                x = mean * (1.0 - random.NextDouble() / (2 * sigma));
            }
            double z = (x - mean) / sigma;
            double variance = Math.Pow(sigma, 2);
            double exponent = -0.5 * Math.Pow(z, 2) / variance;
            double gaussian = (1 / (sigma * Math.Sqrt(2 * Math.PI))) * Math.Pow(Math.E, exponent);
            this.Log(LogLevel.Noisy, "Sensordata x: " + x.ToString());
            this.Log(LogLevel.Noisy, "Probability of x: " + gaussian.ToString());
            return x;
        }

        private void GetTemperature()
        {
            // TODO: Bogus temp data, should return value to be used with calibration data to calculate T
            double preciseTemperature = SensorData(25.0, 1.0);
            temperature = (byte)(Convert.ToInt16(Math.Round(preciseTemperature)) & 0xFF);
            this.Log(LogLevel.Noisy, "Temperature: " + temperature.ToString());
        }

        private void FlipSensor()
        {
            // Flip the sensor 180 degrees around a central axis (x,y or z)
            sensorFlipState <<= 1;
            // Return to zero rotation state after six flips (bit shifts)
            if(sensorFlipState > Convert.ToInt16(SensorFlipStates.XYZ_110))
            {
                sensorFlipState = (byte)SensorFlipStates.XYZ_111;
            }
        }

        private void GetAccelerometerX()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // Either close to +1g or -1g
            // 10-bit ADCs --> value in {0, 1023}
            Int16 accelerometerX = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                accelerometerX = Convert.ToInt16(Math.Round(SensorData(900.0, 10.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                accelerometerX = Convert.ToInt16(Math.Round(SensorData(100.0, 10.0)));
                break;
            default:
                break;
            }
            accelerometerX &= 0x3FF;
            // MSB is bits 9:2
            acc_x_msb = (byte)((accelerometerX >> 2) & 0xFF);
            // LSB is bits 1:0 | 0 | new_data_x (bit 0 shows if data has been read out or is new)
            acc_x_lsb = (byte)((accelerometerX & 0x3) << 6);
            acc_x_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_X_MSB: " + acc_x_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_X_LSB: " + acc_x_lsb.ToString());
        }

        private void GetAccelerometerY()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // Either close to +1g or -1g
            // 10-bit ADCs --> value in {0, 1023}
            Int16 accelerometerY = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                accelerometerY = Convert.ToInt16(Math.Round(SensorData(900.0, 10.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                accelerometerY = Convert.ToInt16(Math.Round(SensorData(100.0, 10.0)));
                break;
            default:
                break;
            }
            accelerometerY &= 0x3FF;
            // MSB is bits 9:2
            acc_y_msb = (byte)((accelerometerY >> 2) & 0xFF);
            // LSB is bits 1:0 | 0 | new_data_y (bit 0 shows if data has been read out or is new)
            acc_y_lsb = (byte)((accelerometerY & 0x3) << 6);
            acc_y_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_Y_MSB: " + acc_y_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_Y_LSB: " + acc_y_lsb.ToString());
        }

        private void GetAccelerometerZ()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // Either close to +1g or -1g
            // 10-bit ADCs --> value in {0, 1023}
            Int16 accelerometerZ = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                accelerometerZ = Convert.ToInt16(Math.Round(SensorData(900.0, 10.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                accelerometerZ = Convert.ToInt16(Math.Round(SensorData(100.0, 10.0)));
                break;
            default:
                break;
            }
            accelerometerZ &= 0x3FF;
            // MSB is bits 9:2
            acc_z_msb = (byte)((accelerometerZ >> 2) & 0xFF);
            // LSB is bits 1:0 | 0 | new_data_z (bit 0 shows if data has been read out or is new)
            acc_z_lsb = (byte)((accelerometerZ & 0x3) << 6);
            acc_z_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_Z_MSB: " + acc_z_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_Z_LSB: " + acc_z_lsb.ToString());
        }

        private void GetMagnetometerX()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // TODO: Different scenario for magnetic field data generation
            // 13-bit ADCs --> value in {0, 8191}
            Int16 magnetometerX = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                magnetometerX = Convert.ToInt16(Math.Round(SensorData(7900.0, 40.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                magnetometerX = Convert.ToInt16(Math.Round(SensorData(290.0, 40.0)));
                break;
            default:
                break;
            }
            magnetometerX &= 0x1FFF;
            // MSB is bits 12:5
            mag_x_msb = (byte)((magnetometerX >> 5) & 0xFF);
            // LSB is bits 4:0 | 0 | self_test_x (bit 0 shows result of self test, default 1)
            mag_x_lsb = (byte)((magnetometerX & 0x1F) << 3);
            // TODO: add result of self test - fault injection possibility
            mag_x_lsb |= 0x1; // Set bit for self test
            this.Log(LogLevel.Noisy, "Mag_X_MSB: " + mag_x_msb.ToString());
            this.Log(LogLevel.Noisy, "Mag_X_LSB: " + mag_x_lsb.ToString());
        }

        private void GetMagnetometerY()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // TODO: Different scenario for magnetic field data generation
            // 13-bit ADCs --> value in {0, 8191}
            Int16 magnetometerY = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                magnetometerY = Convert.ToInt16(Math.Round(SensorData(7900.0, 40.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                magnetometerY = Convert.ToInt16(Math.Round(SensorData(290.0, 40.0)));
                break;
            default:
                break;
            }
            magnetometerY &= 0x1FFF;
            // MSB is bits 12:5
            mag_y_msb = (byte)((magnetometerY >> 5) & 0xFF);
            // LSB is bits 4:0 | 0 | self_test_y (bit 0 shows result of self test, default 1)
            mag_y_lsb = (byte)((magnetometerY & 0x1F) << 3);
            // TODO: add result of self test - fault injection possibility
            mag_y_lsb |= 0x1; // Set bit for self test
            this.Log(LogLevel.Noisy, "Mag_Y_MSB: " + mag_y_msb.ToString());
            this.Log(LogLevel.Noisy, "Mag_Y_LSB: " + mag_y_lsb.ToString());
        }

        private void GetMagnetometerZ()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // TODO: Different scenario for magnetic field data generation
            // 13-bit ADCs --> value in {0, 8191}
            Int16 magnetometerZ = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                magnetometerZ = Convert.ToInt16(Math.Round(SensorData(7900.0, 40.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                magnetometerZ = Convert.ToInt16(Math.Round(SensorData(290.0, 40.0)));
                break;
            default:
                break;
            }
            magnetometerZ &= 0x1FFF;
            // MSB is bits 12:5
            mag_z_msb = (byte)((magnetometerZ >> 5) & 0xFF);
            // LSB is bits 4:0 | 0 | self_test_z (bit 0 shows result of self test, default 1)
            mag_z_lsb = (byte)((magnetometerZ & 0x1F) << 3);
            // TODO: add result of self test - fault injection possibility
            mag_z_lsb |= 0x1; // Set bit for self test
            this.Log(LogLevel.Noisy, "Mag_Z_MSB: " + mag_z_msb.ToString());
            this.Log(LogLevel.Noisy, "Mag_Z_LSB: " + mag_z_lsb.ToString());
        }

        private void GetHallResistance()
        {
            // TODO: Bogus Hall resistance data
            // 14-bit ADC
            // TODO: Should also handle different modes
            Int16 resistanceHall = Convert.ToInt16(Math.Round(SensorData(50.0, 10.0)));
            // MSB is bits 13:6
            mag_rhall_msb = (byte)((resistanceHall >> 6) & 0xFF);
            // LSB is bits 5:0 | 0 | data ready status bit
            mag_rhall_lsb = (byte)((resistanceHall & 0x3F) << 2);
            this.Log(LogLevel.Noisy, "Mag_RHALL_MSB: " + mag_rhall_msb.ToString());
            this.Log(LogLevel.Noisy, "Mag_RHALL_LSB: " + mag_rhall_lsb.ToString());
        }

        private byte GetCRC(byte[] input, byte[] output)
        {
            var crc = input[0];
            for(int i = 1; i < input.Length; i++)
            {
                crc ^= input[i];
            }
            for(int j = 0; j < output.Length; j++)
            {
                crc ^= output[j];
            }
            return (byte)(crc);
        }

        private byte acc_z_msb;
        private byte acc_z_lsb;
        private byte acc_y_msb;
        private byte acc_y_lsb;
        private byte acc_x_msb;
        private byte acc_x_lsb;
        private byte temperature;
        private byte statusReg1;
        private byte statusReg2;
        private byte statusReg3;
        private byte statusReg4;
        private g_RangeModes gRange;
        private BandwidthModes bandwidth;
        private byte power;
        private byte daqCtrl;
        private byte irqCtrl1;
        private byte irqCtrl2;
        private byte irqMap1;
        private byte irqMap2;
        private byte irqMap3;
        private byte irqSource;
        private byte irqBehaviour;
        private byte irqMode;
        private byte lowDur;
        private byte lowTh;
        private byte hy;
        private byte highDur;
        private byte highTh;
        private byte slopeDur;
        private byte slopeTh;
        private byte tapConfig1;
        private byte tapConfig2;
        private byte orientConfig1;
        private byte orientConfig2;
        private byte flatConfig1;
        private byte flatConfig2;
        private SelfTestModes selfTest;
        private byte eepromCtrl;
        private byte interfaceConfig;
        private byte offsetCompensation;
        private byte offsetTarget;
        private byte offsetFilteredX;
        private byte offsetFilteredY;
        private byte offsetFilteredZ;
        private byte offsetUnfilteredX;
        private byte offsetUnfilteredY;
        private byte offsetUnfilteredZ;
        private byte mag_x_lsb;
        private byte mag_x_msb;
        private byte mag_y_lsb;
        private byte mag_y_msb;
        private byte mag_z_lsb;
        private byte mag_z_msb;
        private byte mag_rhall_lsb;
        private byte mag_rhall_msb;
        private byte magIntrStatus;
        private byte magCtrl;
        private byte magOpModeCtrl;
        private byte magIntrCtrl1;
        private byte magIntrCtrl2;
        private byte magLowTh;
        private byte magHighTh;
        private byte magRepXY;
        private byte magRepZ;

        // Internal use only
        private uint state;
        private byte registerAddress;
        private byte registerData;
        private byte[] sendData;
        private byte sensorFlipState;

        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private enum g_RangeModes
        {
            // Allowed modes - other values shall result in Range2g setting, bits 0-3
            Range2g = 0x3, // Selects ±2g range, default value
            Range4g = 0x5, // Selects ±4g range
            Range8g = 0x8, // Selects ±8g range
            Range16g = 0xC  // Selects ±16g range
        }

        private enum BandwidthModes
        {
            // Allowed modes - bits 0-4
            Bandwidth0 = 0x7, // A mask, 00xxxb selects 7.81 Hz
            Bandwidth1 = 0x8, // Selects 7.81 Hz
            Bandwidth2 = 0x9, // Selects 15.63 Hz
            Bandwidth3 = 0xA, // Selects 31.25 Hz
            Bandwidth4 = 0xB, // Selects 62.5 Hz
            Bandwidth5 = 0xC, // Selects 125 Hz
            Bandwidth6 = 0xD, // Selects 250 Hz
            Bandwidth7 = 0xE, // Selects 500 Hz
            Bandwidth8 = 0xF, // Selects 1000 Hz
            Bandwidth9 = 0x10, // A mask, 1xxxxb selects 1000 Hz
        }

        private enum SelfTestModes
        {
            Idle = 0x70,
            Pos_X_Axis = 0x71,
            Pos_Y_Axis = 0x72,
            Pos_Z_Axis = 0x73,
            Neg_X_Axis = 0x75,
            Neg_Y_Axis = 0x76,
            Neg_Z_Axis = 0x77
        }

        private enum SensorFlipStates
        {
            // State used for generation of sensor data.
            // The idea is that between each read request the sensor is flipped 180 degrees
            // around one of its three central axis x, y or z from pos.
            // The flip  changes the axis orientation in label: 
            //   1 means positive (+1g), 0 means negative (-1g)
            // Sensor data generation sequence is 
            // 1) X: +1g -> -1g 
            // 2) Y: +1g -> -1g 
            // 3) Z: +1g -> -1g 
            // 4) X: -1g -> +1g 
            // 5) Y: -1g -> +1g 
            // 6) Z: -1g -> +1g
            // where number indicates bit number flipped in state tracking byte
            XYZ_111 = 0x01,
            XYZ_011 = 0x02,
            XYZ_001 = 0x04,
            XYZ_000 = 0x08,
            XYZ_100 = 0x10,
            XYZ_110 = 0x20
        }

        private enum States
        {
            Idle = 0x0,
            ReceivingData = 0xFD,
            SendingData = 0xFC
        }

        private enum Registers
        {
            ChipID = 0x00, // Read-Only
            Reserved1 = 0x01, // Reserved
            Acc_X_LSB = 0x02, // Read-Only
            Acc_X_MSB = 0x03, // Read-Only
            Acc_Y_LSB = 0x04, // Read-Only
            Acc_Y_MSB = 0x05, // Read-Only
            Acc_Z_LSB = 0x06, // Read-Only
            Acc_Z_MSB = 0x07, // Read-Only
            Temperature = 0x08, // Read-Only
            StatusReg1 = 0x09, // Read-Only
            StatusReg2 = 0x0A, // Read-Only
            StatusReg3 = 0x0B, // Read-Only
            StatusReg4 = 0x0C, // Read-Only
            Reserved2 = 0x0D, // Reserved
            Reserved3 = 0x0E, // Reserved
            g_Range = 0x0F, // Read-Write
            Bandwidth = 0x10, // Read-Write
            Power = 0x11, // Read-Write
            Reserved4 = 0x12, // Reserved
            DaqCtrl = 0x13, // Read-Write
            SoftReset = 0x14, // Read-Write
            Reserved5 = 0x15, // Reserved
            IrqCtrl1 = 0x16, // Read-Write
            IrqCtrl2 = 0x17, // Read-Write
            Reserved6 = 0x18, // Reserved
            IrqMap1 = 0x19, // Read-Write
            IrqMap2 = 0x1A, // Read-Write
            IrqMap3 = 0x1B, // Read-Write
            Reserved7 = 0x1C, // Reserved
            Reserved8 = 0x1D, // Reserved
            IrqSource = 0x1E, // Read-Write
            Reserved9 = 0x1F, // Reserved
            IrqBehaviour = 0x20, // Read-Write
            IrqMode = 0x21, // Read-Write
            LowDur = 0x22, // Read-Write
            LowTh = 0x23, // Read-Write
            Hy = 0x24, // Read-Write
            HighDur = 0x25, // Read-Write
            HighTh = 0x26, // Read-Write
            SlopeDur = 0x27, // Read-Write
            SlopeTh = 0x28, // Read-Write
            Reserved10 = 0x29, // Reserved
            TapConfig1 = 0x2A, // Read-Write
            TapConfig2 = 0x2B, // Read-Write
            OrientConfig1 = 0x2C, // Read-Write
            OrientConfig2 = 0x2D, // Read-Write
            FlatConfig1 = 0x2E, // Read-Write
            FlatConfig2 = 0x2F, // Read-Write
            Reserved11 = 0x30, // Reserved
            Reserved12 = 0x31, // Reserved
            SelfTest = 0x32, // Read-Write
            EEPROMCtrl = 0x33, // Read-Write
            InterfaceConfig = 0x34, // Read-Write
            Reserved13 = 0x35, // Reserved
            OffsetCompensation = 0x36, // Read-Write
            OffsetTarget = 0x37, // Read-Write
            OffsetFilteredX = 0x38, // Read-Write
            OffsetFilteredY = 0x39, // Read-Write
            OffsetFilteredZ = 0x3A, // Read-Write
            OffsetUnfilteredX = 0x3B, // Read-Write
            OffsetUnfilteredY = 0x3C, // Read-Write
            OffsetUnfilteredZ = 0x3D, // Read-Write
            Reserved14 = 0x3E, // Reserved
            Reserved15 = 0x3F, // Reserved
            MagChipID = 0x40, // Read-Only
            Reserved16 = 0x41, // Reserved
            Mag_X_LSB = 0x42, // Read-Only
            Mag_X_MSB = 0x43, // Read-Only
            Mag_Y_LSB = 0x44, // Read-Only
            Mag_Y_MSB = 0x45, // Read-Only
            Mag_Z_LSB = 0x46, // Read-Only
            Mag_Z_MSB = 0x47, // Read-Only
            Mag_RHALL_LSB = 0x48, // Read-Only
            Mag_RHALL_MSB = 0x49, // Read-Only
            MagIntrStatus = 0x4A, // Read-Only
            MagCtrl = 0x4B, // Read-Write
            MagOpModeCtrl = 0x4C, // Read-Write
            MagIntrCtrl1 = 0x4D, // Read-Write
            MagIntrCtrl2 = 0x4E, // Read-Write
            MagLowTh = 0x4F, // Read-Write
            MagHighTh = 0x50, // Read-Write
            MagRepXY = 0x51, // Read-Write
            MagRepZ = 0x52  // Read-Write
            // Register addresses 0x53-0x71 are reserved and N/A
            // Register addresses 0x53-0x54 are images of registers on EEPROM
            // Offset registers 0x38-0x3D are mirrored on EEPROM and restored on reset
        }
    }
}

