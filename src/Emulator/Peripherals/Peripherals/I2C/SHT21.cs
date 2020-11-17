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
    public class SHT21 : II2CPeripheral
    {
        public double MeanHumidity { get; set; }
        public SHT21()
        {
            MeanHumidity = 75.0;
            Reset();
        }

        public void Reset()
        {
            this.Log(LogLevel.Noisy, "Reset registers");
            user = 0x3A;
            state = (uint)States.Idle;
            registerAddress = 0;
            registerData = 0;
            resultArray[0] = 0;
            resultArray[1] = 0;
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
                // Got a value to write to register
                registerData = data[2];
            }
            var result = new byte[3] { 0, 0, 0 };
            switch((States)state)
            {
            case States.ReceivingData:
                switch((Registers)registerAddress)
                {
                case Registers.SoftReset:
                    Reset();
                    break;
                case Registers.UserWrite:
                    user = registerData;
                    break;
                case Registers.UserRead:
                    break;
                case Registers.TemperatureHM:
                    GetTemperature();
                    break;
                case Registers.TemperaturePoll:
                    GetTemperature();
                    // Polling issues Write and then reads directly without writing read command
                    // so it is necessary to prepare send data here 
                    result = new byte[3] { 0, 0, 0 };
                    result[0] = resultArray[0];
                    result[1] = resultArray[1];
                    result[2] = GetSTH21CRC(resultArray, 2);
                    sendData = new byte[result.Length + 1];
                    result.CopyTo(sendData, 0);
                    sendData[result.Length] = GetCRC(data, result);
                    break;
                case Registers.HumidityHM:
                    GetHumidity();
                    break;
                case Registers.HumidityPoll:
                    GetHumidity();
                    // Polling issues Write and then reads directly without writing read command
                    // so it is necessary to prepare send data here 
                    result = new byte[3] { 0, 0, 0 };
                    result[0] = resultArray[0];
                    result[1] = resultArray[1];
                    result[2] = GetSTH21CRC(resultArray, 2);
                    sendData = new byte[result.Length + 1];
                    result.CopyTo(sendData, 0);
                    sendData[result.Length] = GetCRC(data, result);
                    break;
                case Registers.OnChipMemory1:
                    // registerData = 0x0F (on-chip memory address)
                    // Prepare serial number for read
                    // SNB_3, CRC SNB_3, SNB_2, CRC SNB_2, SNB_1, CRC SNB_1, SNB_0, CRC SNB_0
                    break;
                case Registers.OnChipMemory2:
                    // registerData = 0xC9 (on-chip memory address)
                    // Prepare serial number for read
                    // SNC_1, SNC_0, CRC SNC0/1, SNA_1, SNA_0, CRC SNA_0/1
                    break;
                default:
                    this.Log(LogLevel.Noisy, "Register address invalid - no action");
                    break;
                }
                state = (uint)States.Idle;
                this.Log(LogLevel.Noisy, "State changed to Idle");
                break;
            case States.SendingData:
                switch((Registers)registerAddress)
                {
                case Registers.TemperaturePoll:
                // Should not happen - fall through just in case
                case Registers.TemperatureHM:
                    result = new byte[3] { 0, 0, 0 };
                    result[0] = resultArray[0];
                    result[1] = resultArray[1];
                    result[2] = GetSTH21CRC(resultArray, 2);
                    break;
                case Registers.HumidityPoll:
                // Should not happen - fall through just in case
                case Registers.HumidityHM:
                    result = new byte[3] { 0, 0, 0 };
                    result[0] = resultArray[0];
                    result[1] = resultArray[1];
                    result[2] = GetSTH21CRC(resultArray, 2);
                    break;
                case Registers.UserRead:
                    result = new byte[1] { 0 };
                    result[0] = user;
                    break;
                case Registers.OnChipMemory1:
                    // Add serial number for read
                    // SNB_3, CRC SNB_3, SNB_2, CRC SNB_2, SNB_1, CRC SNB_1, SNB_0, CRC SNB_0
                    result = new byte[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    break;
                case Registers.OnChipMemory2:
                    // Add serial number for read
                    // SNC_1, SNC_0, CRC SNC0/1, SNA_1, SNA_0, CRC SNA_0/1
                    result = new byte[7] { 0, 0, 0, 0, 0, 0, 0 };
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
            this.NoisyLog("Read {0}", sendData.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
            return sendData;
        }

        public void FinishTransmission()
        {
        }

        private double SensorData(double mean, double sigma)
        {
            // mean = mean value of Gaussian (Normal) distribution and sigma = standard deviation
            int sign = random.Next(10);
            double x;
            if(sign > 5)
            {
                x = mean * (1.0 + random.NextDouble() / (2 * sigma));
            }
            else
            {
                x = mean * (1.0 - random.NextDouble() / (2 * sigma));
            }
            return x;
        }

        private void GetTemperature()
        {
            // Temperature in degrees Centigrade are calculated as:
            // T = -46.85 + 175.72 * ST/2^16
            // Return ST in two bytes, 14, 12, 13 or 11 bits precision
            // bits 0 and 1 in LSB are status bits
            // bit 1 indicates measurement type, 0 for Temperature, 1 for Humidity
            // bit 0 is currently not assigned - shall be zero
            // bits 2 and 3 unused - shall be zero
            // TODO: T is scaled to avoid truncating since precision < 16 bits 
            // Generated sensor data needs to be verified against real hardware
            double temperature = SensorData(25.0, 1.0);
            double result = (temperature + 46.85) * 65536.0 / 17572.0;
            UInt16 resultInt = Convert.ToUInt16(Math.Round(result));
            // Handle different precision as specified in user register bit7,0
            switch((UserControls)(user & (int)UserControls.ResolutionMask))
            {
            case UserControls.Resolution_12_14BIT:
                resultArray[0] = (byte)((resultInt >> 6) & 0xFF);
                resultArray[1] = (byte)((resultInt & 0x3F) << 2);
                break;
            case UserControls.Resolution_8_12BIT:
                resultArray[0] = (byte)((resultInt >> 4) & 0xFF);
                resultArray[1] = (byte)((resultInt & 0xF) << 4);
                break;
            case UserControls.Resolution_10_13BIT:
                resultArray[0] = (byte)((resultInt >> 5) & 0xFF);
                resultArray[1] = (byte)((resultInt & 0x1F) << 3);
                break;
            case UserControls.Resolution_11_11BIT:
                resultArray[0] = (byte)((resultInt >> 3) & 0xFF);
                resultArray[1] = (byte)((resultInt & 0x7) << 5);
                break;
            default:
                break;
            }
        }

        private void GetHumidity()
        {
            // Relative humidity in percent
            // RH = -6 + 125 * SRH/2^16
            // Return SRH in two bytes, 12, 8, 10 or 11 bits precision
            // bits 0 and 1 in LSB are status bits
            // bit 1 indicates measurement type, 0 for Temperature, 1 for Humidity
            // bit 0 is currently not assigned - shall be zero
            // bits 2 and 3 unused - shall be zero
            double humidity = SensorData(MeanHumidity, 5.0);
            if(humidity > 99.0)
            {
                humidity = 99.0;
            }
            if(humidity < 1.0)
            {
                humidity = 1.0;
            }
            double result = (humidity + 6.0) * 65536.0 / 125;
            UInt16 resultInt = Convert.ToUInt16(Math.Round(result));
            // Handle different precision as specified in user register bit7,0
            switch((UserControls)(user & (int)UserControls.ResolutionMask))
            {
            case UserControls.Resolution_12_14BIT:
                resultArray[0] = (byte)((resultInt >> 4) & 0xFF);
                resultArray[1] = (byte)(((resultInt & 0xF) << 4) + 0x2);
                break;
            case UserControls.Resolution_8_12BIT:
                resultArray[0] = (byte)((resultInt) & 0xFF);
                resultArray[1] = 0x2;
                break;
            case UserControls.Resolution_10_13BIT:
                resultArray[0] = (byte)((resultInt >> 2) & 0xFF);
                resultArray[1] = (byte)(((resultInt & 0x3) << 6) + 0x2);
                break;
            case UserControls.Resolution_11_11BIT:
                resultArray[0] = (byte)(((resultInt) >> 3) & 0xFF);
                resultArray[1] = (byte)(((resultInt & 0x7) << 5) + 0x2);
                break;
            default:
                break;
            }
        }

        private byte GetSTH21CRC(byte[] array, int nrOfBytes)
        {
            const uint POLYNOMIAL = 0x131;  // P(x)=x^8+x^5+x^4+1 = 100110001
            byte crc = 0;
            for(byte i = 0; i < nrOfBytes; ++i)
            {
                crc ^= (array[i]);
                for(byte bit = 8; bit > 0; --bit)
                {
                    if((crc & 0x80) == 0x80)
                    {
                        crc = (byte)(((uint)crc << 1) ^ POLYNOMIAL);
                    }
                    else
                    {
                        crc = (byte)(crc << 1);
                    }
                }
            }
            return crc;
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
            return crc;
        }

        private byte[] resultArray = new byte[2] { 0, 0 };
        private byte user;

        private uint state;
        private byte registerAddress;
        private byte registerData;
        private byte[] sendData;

        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private enum UserControls
        {
            Resolution_12_14BIT = 0x00, // RH=12bit, T=14bit
            Resolution_8_12BIT = 0x01, // RH= 8bit, T=12bit
            Resolution_10_13BIT = 0x80, // RH=10bit, T=13bit
            Resolution_11_11BIT = 0x81, // RH=11bit, T=11bit
            ResolutionMask = 0x81  // Mask for bits 7,0 in user register
        }

        private enum States
        {
            Idle = 0x0,
            ReceivingData = 0xFD,
            SendingData = 0xFC
        }

        private enum Registers
        {
            TemperatureHM = 0xE3, // Read-Write
            HumidityHM = 0xE5, // Read-Write
            UserWrite = 0xE6, // Write-Only
            UserRead = 0xE7, // Read-Only
            TemperaturePoll = 0xF3, // Read-Write
            HumidityPoll = 0xF5, // Read-Write
            OnChipMemory1 = 0xFA, // Read-Only
            OnChipMemory2 = 0xFC, // Read-Only
            SoftReset = 0xFE  // Write-Only
        }
    }
}

