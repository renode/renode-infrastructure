//
// Copyright (c) 2010-2020 Antmicro
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
    public class BMA180 : II2CPeripheral
    {
        public BMA180()
        {
            Reset();
        }

        // TODO: Implement EEPROM snapshot on disk for storing changes to register values (WriteEEPROM)

        public void Reset()
        {
            this.Log(LogLevel.Noisy, "Reset registers");
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
            ctrlReg0 = 0;
            ctrlReg1 = 0;
            ctrlReg2 = 0;
            // Read imaged register values from EEPROM
            UpdateImage();
            // Used internally
            state = (uint)States.Idle;
            registerAddress = 0;
            registerData = 0;
            sensorFlipState = (byte)SensorFlipStates.XYZ_111;
        }

        private void UpdateImage()
        {
            // The following register values (0x20-0x3A) are images of EEPROM 
            // registers (0x40-0x5A). Values are imaged from EEPROM at Power On Reset, 
            // Soft Reset or when control bit update_image is set to ‘1’.
            bw_tcs = 0x4;
            ctrlReg4 = 0;
            hy = 0;
            slopeTapSens = 0;
            highLowInfo = 0;
            lowDur = 0x50;
            highDur = 0x32;
            tapSensTh = 0;
            lowTh = 0x17;
            highTh = 0x50;
            slopeTh = 0;
            customerData1 = 0;
            customerData2 = 0;
            offset_lsb1 = 0;
            offset_lsb2 = 0;
            tco_x = 0;
            tco_y = 0;
            tco_z = 0;
            gain_t = 0;
            gain_x = 0;
            gain_y = 0;
            gain_z = 0;
            offset_t = 0;
            offset_x = 0;
            offset_y = 0;
            offset_z = 0;
        }

        public void Write(byte[] data)
        {
            // Parse the list bytes
            if(data.Length < 2)
            {
                // Must always have mode and register address in list
                this.Log(LogLevel.Noisy, "Write - too few elements in list ({0}) - must be at least two", data.Length);
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
            // TODO - handle writing of calibration data
            if(data.Length > 3)
            {

            }
            switch((States)state)
            {
            case States.ReceivingData:
                switch((Registers)registerAddress)
                {
                case Registers.StatusReg1:
                    // Allow write for testing, read-only address
                    statusReg1 = registerData;
                    break;
                case Registers.StatusReg2:
                    // Allow write for testing, read-only address
                    statusReg2 = registerData;
                    break;
                case Registers.StatusReg3:
                    // Allow write for testing, read-only address
                    statusReg3 = registerData;
                    break;
                case Registers.StatusReg4:
                    // Allow write for testing, read-only address
                    statusReg4 = registerData;
                    break;
                case Registers.CtrlReg0:
                    ctrlReg0 = registerData;
                    // If bit 4 is set (ee_w) write to EEPROM if it is unlocked
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        if((ctrlReg2 & 0xF0) == 0xF0)
                        {
                            WriteEEPROM();
                        }
                    }
                    // If bit 5 is set (update_image) update image values and clear bit
                    if((ctrlReg0 & (byte)CtrlReg0Value.updateImage) == (byte)CtrlReg0Value.updateImage)
                    {
                        UpdateImage();
                        ctrlReg0 &= 0xDF;
                    }
                    break;
                case Registers.CtrlReg1:
                    ctrlReg1 = registerData;
                    break;
                case Registers.CtrlReg2:
                    ctrlReg2 = registerData;
                    break;
                case Registers.SoftReset:
                    softReset = registerData;
                    if(softReset == 0xB6) // Same reset value for Bosch BMA180 and BMP180
                    {
                        Reset();
                    }
                    break;
                // For register addresses 0x20-0x3F ee_w bit in ctrlReg0 must be set
                case Registers.BW_TCS:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        bw_tcs = registerData;
                    }
                    break;
                case Registers.CtrlReg3:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        ctrlReg3 = registerData;
                    }
                    break;
                case Registers.CtrlReg4:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        ctrlReg4 = registerData;
                    }
                    break;
                case Registers.HY:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        hy = registerData;
                    }
                    break;
                case Registers.SlopeTapSens:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        slopeTapSens = registerData;
                    }
                    break;
                case Registers.HighLowInfo:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        highLowInfo = registerData;
                    }
                    break;
                case Registers.LowDur:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        lowDur = registerData;
                    }
                    break;
                case Registers.HighDur:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        highDur = registerData;
                    }
                    break;
                case Registers.TapSensTh:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        tapSensTh = registerData;
                    }
                    break;
                case Registers.LowTh:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        lowTh = registerData;
                    }
                    break;
                case Registers.HighTh:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        highTh = registerData;
                    }
                    break;
                case Registers.SlopeTh:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        slopeTh = registerData;
                    }
                    break;
                case Registers.CustomData1:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        customerData1 = registerData;
                    }
                    break;
                case Registers.CustomData2:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        customerData2 = registerData;
                    }
                    break;
                case Registers.TCO_X:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        tco_x = registerData;
                    }
                    break;
                case Registers.TCO_Y:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        tco_y = registerData;
                    }
                    break;
                case Registers.TCO_Z:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        tco_z = registerData;
                    }
                    break;
                case Registers.Gain_T:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        gain_t = registerData;
                    }
                    break;
                case Registers.Gain_X:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        gain_x = registerData;
                    }
                    break;
                case Registers.Gain_Y:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        gain_y = registerData;
                    }
                    break;
                case Registers.Gain_Z:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        gain_z = registerData;
                    }
                    break;
                case Registers.Offset_LSB1:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_lsb1 = registerData;
                    }
                    break;
                case Registers.Offset_LSB2:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_lsb2 = registerData;
                    }
                    break;
                case Registers.Offset_T:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_t = registerData;
                    }
                    break;
                case Registers.Offset_X:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_x = registerData;
                    }
                    break;
                case Registers.Offset_Y:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_y = registerData;
                    }
                    break;
                case Registers.Offset_Z:
                    if((ctrlReg0 & (byte)CtrlReg0Value.ee_w) == (byte)CtrlReg0Value.ee_w)
                    {
                        offset_z = registerData;
                    }
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
                    result[4] = acc_y_lsb;
                    result[5] = acc_y_msb;
                    // Read clears new_data_xyz bits
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
                    break;
                case Registers.ChipID:
                    result[0] = 0x3;
                    break;
                case Registers.Version:
                    result[0] = 0;
                    break;
                case Registers.Temperature:
                    GetTemperature();
                    result[0] = temperature;
                    break;
                case Registers.SoftReset:
                    result[0] = 0;
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
                case Registers.CtrlReg0:
                    result[0] = ctrlReg0;
                    break;
                case Registers.CtrlReg1:
                    result[0] = ctrlReg1;
                    break;
                case Registers.CtrlReg2:
                    result[0] = ctrlReg2;
                    break;
                case Registers.CtrlReg3:
                    result[0] = ctrlReg3;
                    break;
                case Registers.CtrlReg4:
                    result[0] = ctrlReg4;
                    break;
                case Registers.LowDur:
                    result[0] = lowDur;
                    break;
                case Registers.HighDur:
                    result[0] = highDur;
                    break;
                case Registers.LowTh:
                    result[0] = lowTh;
                    break;
                case Registers.HighTh:
                    result[0] = highTh;
                    break;
                case Registers.BW_TCS:
                case Registers.EE_BW_TCS:
                    result[0] = bw_tcs;
                    break;
                case Registers.HY:
                case Registers.EE_HY:
                    result[0] = hy;
                    break;
                case Registers.SlopeTapSens:
                case Registers.EE_SlopeTapSens:
                    result[0] = slopeTapSens;
                    break;
                case Registers.HighLowInfo:
                case Registers.EE_HighLowInfo:
                    result[0] = highLowInfo;
                    break;
                case Registers.TapSensTh:
                case Registers.EE_TapSensTh:
                    result[0] = tapSensTh;
                    break;
                case Registers.SlopeTh:
                case Registers.EE_SlopeTh:
                    result[0] = slopeTh;
                    break;
                case Registers.CustomData1:
                case Registers.EE_CustomData1:
                    result[0] = customerData1;
                    break;
                case Registers.CustomData2:
                case Registers.EE_CustomData2:
                    result[0] = customerData2;
                    break;
                case Registers.TCO_X:
                case Registers.EE_TCO_X:
                    result[0] = tco_x;
                    break;
                case Registers.TCO_Y:
                case Registers.EE_TCO_Y:
                    result[0] = tco_y;
                    break;
                case Registers.TCO_Z:
                case Registers.EE_TCO_Z:
                    result[0] = tco_z;
                    break;
                case Registers.Gain_T:
                case Registers.EE_Gain_T:
                    result[0] = gain_t;
                    break;
                case Registers.Gain_X:
                case Registers.EE_Gain_X:
                    result[0] = gain_x;
                    break;
                case Registers.Gain_Y:
                case Registers.EE_Gain_Y:
                    result[0] = gain_y;
                    break;
                case Registers.Gain_Z:
                case Registers.EE_Gain_Z:
                    result[0] = gain_z;
                    break;
                case Registers.Offset_LSB1:
                case Registers.EE_Offset_LSB1:
                    result[0] = offset_lsb1;
                    break;
                case Registers.Offset_LSB2:
                case Registers.EE_Offset_LSB2:
                    result[0] = offset_lsb2;
                    break;
                case Registers.Offset_T:
                case Registers.EE_Offset_T:
                    result[0] = offset_t;
                    break;
                case Registers.Offset_X:
                case Registers.EE_Offset_X:
                    result[0] = offset_x;
                    break;
                case Registers.Offset_Y:
                case Registers.EE_Offset_Y:
                    result[0] = offset_y;
                    break;
                case Registers.Offset_Z:
                case Registers.EE_Offset_Z:
                    result[0] = offset_z;
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

        public void FinishTransmission()
        {
        }

        private void WriteEEPROM()
        {
            // Currently not implemented
            // Need an EEPROM snapshot on disk to be able to store changes 
            // to registers 0x20-0x3A (see UpdateImage) in a non-volatile manner
            //
            // Set ee_write bit during operation - it inhibits writes to registers
            statusReg1 &= 0x1;
            // Write to EEPROM is always followed by soft reset
            Reset();
        }

        public byte[] Read(int count = 0)
        {
            this.NoisyLog("Read {0}", sendData.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
            return sendData;
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
            temperature = (byte)(Convert.ToUInt16(Math.Round(preciseTemperature)) & 0xFF);
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
            // 14-bit ADCs --> value in {0, 16383}
            Int16 accelerometerX = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
                accelerometerX = Convert.ToInt16(Math.Round(SensorData(16000.0, 50.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                accelerometerX = Convert.ToInt16(Math.Round(SensorData(300.0, 50.0)));
                break;
            default:
                break;
            }
            accelerometerX &= 0x3FFF;
            // MSB is bits 13:6
            acc_x_msb = (byte)((accelerometerX >> 6) & 0xFF);
            // LSB is bits 5:0 | 0 | new_data_x (bit 0 shows if data has been read out or is new)
            acc_x_lsb = (byte)((accelerometerX & 0x3F) << 2);
            acc_x_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_X_MSB: " + acc_x_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_X_LSB: " + acc_x_lsb.ToString());
        }

        private void GetAccelerometerY()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // Either close to +1g or -1g
            // 14-bit ADCs --> value in {0, 16383}
            Int16 accelerometerY = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_011:
                accelerometerY = Convert.ToInt16(Math.Round(SensorData(16000.0, 50.0)));
                break;
            case SensorFlipStates.XYZ_000:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_100:
                accelerometerY = Convert.ToInt16(Math.Round(SensorData(300.0, 50.0)));
                break;
            default:
                break;
            }
            accelerometerY &= 0x3FFF;
            // MSB is bits 13:6
            acc_y_msb = (byte)((accelerometerY >> 6) & 0xFF);
            // LSB is bits 5:0 | 0 | new_data_y (bit 0 shows if data has been read out or is new)
            acc_y_lsb = (byte)((accelerometerY & 0x3F) << 2);
            acc_y_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_Y_MSB: " + acc_y_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_Y_LSB: " + acc_y_lsb.ToString());
        }

        private void GetAccelerometerZ()
        {
            // TODO: Should also handle different modes
            // Use the sensor flip state to determine mean for data generation
            // Either close to +1g or -1g
            // 14-bit ADCs --> value in {0, 16383}
            Int16 accelerometerZ = 0;
            switch((SensorFlipStates)sensorFlipState)
            {
            case SensorFlipStates.XYZ_111:
            case SensorFlipStates.XYZ_001:
            case SensorFlipStates.XYZ_011:
                accelerometerZ = Convert.ToInt16(Math.Round(SensorData(16000.0, 50.0)));
                break;
            case SensorFlipStates.XYZ_110:
            case SensorFlipStates.XYZ_100:
            case SensorFlipStates.XYZ_000:
                accelerometerZ = Convert.ToInt16(Math.Round(SensorData(300.0, 50.0)));
                break;
            default:
                break;
            }
            accelerometerZ &= 0x3FFF;
            // MSB is bits 13:6
            acc_z_msb = (byte)((accelerometerZ >> 6) & 0xFF);
            // LSB is bits 5:0 | 0 | new_data_z (bit 0 shows if data has been read out or is new)
            acc_z_lsb = (byte)((accelerometerZ & 0x3F) << 2);
            acc_z_lsb |= 0x1; // Set bit for new data
            this.Log(LogLevel.Noisy, "Acc_Z_MSB: " + acc_z_msb.ToString());
            this.Log(LogLevel.Noisy, "Acc_Z_LSB: " + acc_z_lsb.ToString());
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
        private byte ctrlReg0;
        private byte ctrlReg1;
        private byte ctrlReg2;

        private byte bw_tcs;
        private byte ctrlReg3;
        private byte ctrlReg4;
        private byte hy;
        private byte slopeTapSens;
        private byte highLowInfo;
        private byte lowDur;
        private byte highDur;
        private byte tapSensTh;
        private byte lowTh;
        private byte highTh;
        private byte slopeTh;
        private byte customerData1;
        private byte customerData2;
        private byte tco_x;
        private byte tco_y;
        private byte tco_z;
        private byte gain_t;
        private byte gain_x;
        private byte gain_y;
        private byte gain_z;
        private byte offset_lsb1;
        private byte offset_lsb2;
        private byte offset_t;
        private byte offset_x;
        private byte offset_y;
        private byte offset_z;

        private byte softReset;
        // Internal use only
        private uint state;
        private byte registerAddress;
        private byte registerData;
        private byte[] sendData;
        private byte sensorFlipState;

        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

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

        private enum StatusReg4Value
        {
            tapsens_sign_z_int = 0x04,
            tapsens_sign_y_int = 0x08,
            tapsens_sign_x_int = 0x10,
            high_sign_z_int = 0x20,
            high_sign_y_int = 0x40,
            high_sign_x_int = 0x80
        }

        private enum StatusReg3Value
        {
            z_first_int = 0x01,
            y_first_int = 0x02,
            x_first_int = 0x04,
            tapsens_int = 0x10,
            slope_int_s = 0x20,
            low_th_int = 0x40,
            high_th_int = 0x80
        }

        private enum StatusReg2Value
        {
            low_sign_z_int = 0x01,
            low_sign_y_int = 0x02,
            low_sign_x_int = 0x04,
            tapsens_s = 0x10,
            slope_s = 0x20,
            low_th_s = 0x40,
            high_th_s = 0x80
        }

        private enum StatusReg1Value
        {
            ee_write = 0x01,
            offset_st_s = 0x02,
            slope_sign_z_int = 0x04,
            slope_sign_y_int = 0x08,
            slope_sign_x_int = 0x10,
            alert = 0x20,
            first_tapsens_s = 0x80
        }

        private enum CtrlReg0Value
        {
            disWakeUp = 0x01,
            sleep = 0x02,
            st0 = 0x04,
            st1 = 0x08,
            ee_w = 0x10,
            updateImage = 0x20,
            resetInt = 0x40
        }

        private enum States
        {
            Idle = 0x0,
            ReceivingData = 0xFD,
            SendingData = 0xFC,
        }

        private enum Registers
        {
            ChipID = 0x00, // Read-Only
            Version = 0x01, // Read-Only
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
            CtrlReg0 = 0x0D, // Read-Write
            CtrlReg1 = 0x0E, // Read-Write
            CtrlReg2 = 0x0F, // Read-Write
            SoftReset = 0x10, // Write-Only
            Reserved1 = 0x11, // Unused
            Reserved2 = 0x12, // Unused
            Reserved3 = 0x13, // Unused
            Reserved4 = 0x14, // Unused
            Reserved5 = 0x15, // Unused
            Reserved6 = 0x16, // Unused
            Reserved7 = 0x17, // Unused
            Reserved8 = 0x18, // Unused
            Reserved9 = 0x19, // Unused
            Reserved10 = 0x1A, // Unused
            Reserved11 = 0x1B, // Unused
            Reserved12 = 0x1C, // Unused
            Reserved13 = 0x1D, // Unused
            Reserved14 = 0x1E, // Unused
            Reserved15 = 0x1F, // Unused
            BW_TCS = 0x20, // Read-Write
            CtrlReg3 = 0x21, // Read-Write
            CtrlReg4 = 0x22, // Read-Write
            HY = 0x23, // Read-Write
            SlopeTapSens = 0x24, // Read-Write
            HighLowInfo = 0x25, // Read-Write
            LowDur = 0x26, // Read-Write
            HighDur = 0x27, // Read-Write
            TapSensTh = 0x28, // Read-Write
            LowTh = 0x29, // Read-Write
            HighTh = 0x2A, // Read-Write
            SlopeTh = 0x2B, // Read-Write
            CustomData1 = 0x2C, // Read-Write
            CustomData2 = 0x2D, // Read-Write
            TCO_X = 0x2E, // Read-Write
            TCO_Y = 0x2F, // Read-Write
            TCO_Z = 0x30, // Read-Write
            Gain_T = 0x31, // Read-Write
            Gain_X = 0x32, // Read-Write
            Gain_Y = 0x33, // Read-Write
            Gain_Z = 0x34, // Read-Write
            Offset_LSB1 = 0x35, // Read-Write
            Offset_LSB2 = 0x36, // Read-Write
            Offset_T = 0x37, // Read-Write
            Offset_X = 0x38, // Read-Write
            Offset_Y = 0x39, // Read-Write
            Offset_Z = 0x3A, // Read-Write
            Reserved16 = 0x3B, // Bosch Reserved
            Reserved17 = 0x3C, // Bosch Reserved
            Reserved18 = 0x3D, // Bosch Reserved
            Reserved19 = 0x3E, // Bosch Reserved
            Reserved20 = 0x3F, // Bosch Reserved
            EE_BW_TCS = 0x40, // Read-Only
            EE_CtrlReg3 = 0x41, // Read-Write
            EE_CtrlReg4 = 0x42, // Read-Write
            EE_HY = 0x43, // Read-Write
            EE_SlopeTapSens = 0x44, // Read-Write
            EE_HighLowInfo = 0x45, // Read-Write
            EE_LowDur = 0x46, // Read-Write
            EE_HighDur = 0x47, // Read-Write
            EE_TapSensTh = 0x48, // Read-Write
            EE_LowTh = 0x49, // Read-Write
            EE_HighTh = 0x4A, // Read-Write
            EE_SlopeTh = 0x4B, // Read-Write
            EE_CustomData1 = 0x4C, // Read-Write
            EE_CustomData2 = 0x4D, // Read-Write
            EE_TCO_X = 0x4E, // Read-Write
            EE_TCO_Y = 0x4F, // Read-Write
            EE_TCO_Z = 0x50, // Read-Write
            EE_Gain_T = 0x51, // Read-Write
            EE_Gain_X = 0x52, // Read-Write
            EE_Gain_Y = 0x53, // Read-Write
            EE_Gain_Z = 0x54, // Read-Write
            EE_Offset_LSB1 = 0x55, // Read-Write
            EE_Offset_LSB2 = 0x56, // Read-Write
            EE_Offset_T = 0x57, // Read-Write
            EE_Offset_X = 0x58, // Read-Write
            EE_Offset_Y = 0x59, // Read-Write
            EE_Offset_Z = 0x5A, // Read-Write
            EE_CRC = 0x5B, // Read-Only
            Reserved21 = 0x5C, // Bosch Reserved
            Reserved22 = 0x5D, // Bosch Reserved
            Reserved23 = 0x5E, // Bosch Reserved
            Reserved24 = 0x5F  // Bosch Reserved
            // 0x60 - 0x8F are Bosch Reserved
        }
    }
}

