//
// Copyright (c) 2010-2018 Antmicro
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
    public class BMP180 : II2CPeripheral
    {
        public BMP180 ()
        {
            Reset ();
        }

        public void Reset ()
        {
            this.Log (LogLevel.Noisy, "Reset registers");
            outMSB = 0x80;
            outLSB = 0x0;
            outXLSB = 0x0;
            softReset = 0x0;
            ctrlMeasurement = 0x0;
            calibAA = 0x1B;
            calibAB = 0xCB;
            calibAC = 0xFB;
            calibAD = 0xCD;
            calibAE = 0xC6;
            calibAF = 0x91;
            calibB0 = 0x7B;
            calibB1 = 0xA8;
            calibB2 = 0x5F;
            calibB3 = 0xE8;
            calibB4 = 0x43;
            calibB5 = 0x35;
            calibB6 = 0x15;
            calibB7 = 0x7A;
            calibB8 = 0x00;
            calibB9 = 0x38;
            calibBA = 0x80;
            calibBB = 0x00;
            calibBC = 0xD4;
            calibBD = 0xBD;
            calibBE = 0x09;
            calibBF = 0x80;
            state = (uint)States.Idle;
            registerAddress = 0;
            registerData = 0;
        }

        public void Write (byte[] data)
        {
            // Parse the list bytes
            if (data.Length < 2)
            {
                // Must always have mode and register address in list
                this.Log (LogLevel.Noisy, "Write - too few elements in list ({0}) - must be at least two", data.Length);
                return;
            }
            this.NoisyLog ("Write {0}", data.Select(x=>x.ToString("X")).Aggregate((x,y)=>x+" "+y));
            // First byte sets the device state
            state = data[0];
            this.Log (LogLevel.Noisy, "State changed to {0}", (States)state);
            // Second byte is always register address
            registerAddress = data[1];
            if(data.Length == 3)
            {
                // Got a value to write to register
                registerData = data [2];
            }
            // Check if we got a write of whole set of calibration parameters (22 values)
            if (((Registers)registerAddress == Registers.CalibAA) && (data.Length == 24)) 
            {
                calibAA = data[2];
                calibAB = data[3];
                calibAC = data[4];
                calibAD = data[5];
                calibAE = data[6];
                calibAF = data[7];
                calibB0 = data[8];
                calibB1 = data[9];
                calibB2 = data[10];
                calibB3 = data[11];
                calibB4 = data[12];
                calibB5 = data[13];
                calibB6 = data[14];
                calibB7 = data[15];
                calibB8 = data[16];
                calibB9 = data[17];
                calibBA = data[18];
                calibBB = data[19];
                calibBC = data[20];
                calibBD = data[21];
                calibBE = data[22];
                calibBF = data[23];
            }
            switch ((States)state) {
            case States.ReceivingData:
                switch ((Registers)registerAddress) {
                case Registers.SoftReset:
                    softReset = registerData;
                    if (softReset == 0xB6) 
                    {
                        Reset ();
                    }
                    break;
                case Registers.CtrlMeasurement:
                    ctrlMeasurement = registerData;
                    HandleMeasurement ();
                    break;
                case Registers.CalibAA:
                    // Separate individual register write from whole 22 byte operation
                    // The latter have already been handled above
                    if(data.Length == 3)
                    {
                        calibAA = registerData;
                    }
                    break;
                case Registers.CalibAB:
                    calibAB = registerData;
                    break;
                case Registers.CalibAC:
                    calibAC = registerData;
                    break;
                case Registers.CalibAD:
                    calibAD = registerData;
                    break;
                case Registers.CalibAE:
                    calibAE = registerData;
                    break;
                case Registers.CalibAF:
                    calibAF = registerData;
                    break;
                case Registers.CalibB0:
                    calibB0 = registerData;
                    break;
                case Registers.CalibB1:
                    calibB1 = registerData;
                    break;
                case Registers.CalibB2:
                    calibB2 = registerData;
                    break;
                case Registers.CalibB3:
                    calibB3 = registerData;
                    break;
                case Registers.CalibB4:
                    calibB4 = registerData;
                    break;
                case Registers.CalibB5:
                    calibB5 = registerData;
                    break;
                case Registers.CalibB6:
                    calibB6 = registerData;
                    break;
                case Registers.CalibB7:
                    calibB7 = registerData;
                    break;
                case Registers.CalibB8:
                    calibB8 = registerData;
                    break;
                case Registers.CalibB9:
                    calibB9 = registerData;
                    break;
                case Registers.CalibBA:
                    calibBA = registerData;
                    break;
                case Registers.CalibBB:
                    calibBB = registerData;
                    break;
                case Registers.CalibBC:
                    calibBC = registerData;
                    break;
                case Registers.CalibBD:
                    calibBD = registerData;
                    break;
                case Registers.CalibBE:
                    calibBE = registerData;
                    break;
                case Registers.CalibBF:
                    calibBF = registerData;
                    break;
                default:
                    this.Log(LogLevel.Noisy, "Register address invalid - no action");
                    break;
                }
                state = (uint)States.Idle;
                this.Log (LogLevel.Noisy, "State changed to Idle");
                break;
            case States.SendingData:
                byte[] result = new byte[1] { 0 };
                switch ((Registers)registerAddress) {
                case Registers.OutMSB:
                    switch((MeasurementModes)ctrlMeasurement){
                    case MeasurementModes.Temperature:
                        result = new byte[2] { 0, 0 };
                        result[0] = outMSB;
                        result[1] = outLSB;
                        break;
                    case MeasurementModes.Pressure0:
                    case MeasurementModes.Pressure1:
                    case MeasurementModes.Pressure2:
                    case MeasurementModes.Pressure3:
                        result = new byte[3] { 0, 0, 0 };
                        result[0] = outMSB;
                        result[1] = outLSB;
                        result[2] = outXLSB;
                        break;
                    default:
                        result[0] = outMSB;
                        break;
                    }
                    break;
                case Registers.OutLSB:
                    result[0] = outLSB;
                    break;
                case Registers.OutXLSB:
                    result[0] = outXLSB;
                    break;
                case Registers.ChipID:
                    result [0] = 0x55;
                    break;
                case Registers.SoftReset:
                    result [0] = softReset;
                    break;
                case Registers.CtrlMeasurement:
                    result [0] = ctrlMeasurement;
                    break;
                case Registers.CalibAA:
                    // Return the whole set of calibration parameters in one go
                    // User can decide how many reads to do - first gives AA, etc
                    result = new byte[22] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    result [0] = calibAA;
                    result [1] = calibAB;
                    result [2] = calibAC;
                    result [3] = calibAD;
                    result [4] = calibAE;
                    result [5] = calibAF;
                    result [6] = calibB0;
                    result [7] = calibB1;
                    result [8] = calibB2;
                    result [9] = calibB3;
                    result [10] = calibB4;
                    result [11] = calibB5;
                    result [12] = calibB6;
                    result [13] = calibB7;
                    result [14] = calibB8;
                    result [15] = calibB9;
                    result [16] = calibBA;
                    result [17] = calibBB;
                    result [18] = calibBC;
                    result [19] = calibBD;
                    result [20] = calibBE;
                    result [21] = calibBF;
                    break;
                case Registers.CalibAB:
                    result [0] = calibAB;
                    break;
                case Registers.CalibAC:
                    result [0] = calibAC;
                    break;
                case Registers.CalibAD:
                    result [0] = calibAD;
                    break;
                case Registers.CalibAE:
                    result [0] = calibAE;
                    break;
                case Registers.CalibAF:
                    result [0] = calibAF;
                    break;
                case Registers.CalibB0:
                    result [0] = calibB0;
                    break;
                case Registers.CalibB1:
                    result [0] = calibB1;
                    break;
                case Registers.CalibB2:
                    result [0] = calibB2;
                    break;
                case Registers.CalibB3:
                    result [0] = calibB3;
                    break;
                case Registers.CalibB4:
                    result [0] = calibB4;
                    break;
                case Registers.CalibB5:
                    result [0] = calibB5;
                    break;
                case Registers.CalibB6:
                    result [0] = calibB6;
                    break;
                case Registers.CalibB7:
                    result [0] = calibB7;
                    break;
                case Registers.CalibB8:
                    result [0] = calibB8;
                    break;
                case Registers.CalibB9:
                    result [0] = calibB9;
                    break;
                case Registers.CalibBA:
                    result [0] = calibBA;
                    break;
                case Registers.CalibBB:
                    result [0] = calibBB;
                    break;
                case Registers.CalibBC:
                    result [0] = calibBC;
                    break;
                case Registers.CalibBD:
                    result [0] = calibBD;
                    break;
                case Registers.CalibBE:
                    result [0] = calibBE;
                    break;
                case Registers.CalibBF:
                    result [0] = calibBF;
                    break;
                default:
                    break;
                }
                sendData = new byte[result.Length + 1];
                result.CopyTo(sendData, 0);
                sendData[result.Length] = GetCRC (data, result);
                break;
            default:
                break;
            }
        }

        public byte[] Read (int count = 0)
        {
            this.NoisyLog ("Read {0}", sendData.Select(x=>x.ToString("X")).Aggregate((x,y)=>x+" "+y));
            return sendData;
        }

        private void HandleMeasurement()
        {
            this.Log (LogLevel.Noisy, "HandleMeasurement set {0}",(MeasurementModes)ctrlMeasurement);
            switch((MeasurementModes)ctrlMeasurement) {
            case MeasurementModes.Temperature:
                GetTemperature ();
                break;
            case MeasurementModes.Pressure0:
                GetPressure ();
                break;
            case MeasurementModes.Pressure1:
                GetPressure ();
                break;
            case MeasurementModes.Pressure2:
                GetPressure ();
                break;
            case MeasurementModes.Pressure3:
                GetPressure ();
                break;
            default:
                break;
            }

        }

        private double SensorData(double mean, double sigma)
        {
            // mean = mean value of Gaussian (Normal) distribution and sigma = standard deviation
            int sign = random.Next (10);
            double x = 0.0;
            if (sign > 5)
            {
                x = mean*(1.0 + random.NextDouble()/(2*sigma));
            }
            else
            {
                x = mean*(1.0 - random.NextDouble()/(2*sigma));
            }   
            double z = (x - mean) / sigma;
            double variance = Math.Pow(sigma, 2);
            double exponent = -0.5 * Math.Pow(z, 2) / variance;
            double gaussian = (1 / (sigma * Math.Sqrt (2 * Math.PI))) * Math.Pow (Math.E, exponent);
            this.Log (LogLevel.Noisy, "Sensordata x: " + x.ToString());
            this.Log (LogLevel.Noisy, "Probability of x: " + gaussian.ToString());
            return x;
        }

        private void GetTemperature()
        {
            // Temperature in scale 0.1 degrees C, ie 25.0 degress C --> value = 250.0
            double temperature = SensorData (250, 10.0);
            // X1  = (UT - AC6)*AC5/2^15
            // X2  = MC*2^11/(X1+MD)
            // T   = (X1 + X2 + 8)/2^4
            // -->
            // 0 = X1^2 + (MD - C1)*X1 + C2
            // C1 = (T*2^4 - 8)
            // C2 = MC*2^11 + MD*C1
            // Quadratic equation, with solutions:
            // -->
            // X1 = 0.5 * [(C1 - MD) ± √((C1 - MD)*(C1 - MD) - 4*C2)]
            // UT = X1*2^15/AC5 + AC6
            // Calculate the UT value from generated temperature T
            Int16 AC5 = (Int16)(((Int16)calibB2 << 8) + (Int16)calibB3);
            Int16 AC6 = (Int16)(((Int16)calibB4 << 8) + (Int16)calibB5);
            Int16 MC = (Int16)(((Int16)calibBC << 8) + (Int16)calibBD);
            Int16 MD = (Int16)(((Int16)calibBE << 8) + (Int16)calibBF);
            Int32 C1 = Convert.ToInt32(Math.Round(temperature)) * 16 - 8;
            Int32 C2 = 0x800 * Convert.ToInt32(MC) + C1 * Convert.ToInt32(MD);
            // Using first solution, with + sign before sqrt part
            Int32 C3 = C1 - Convert.ToInt32(MD);
            Int32 X1 = (C3 + Convert.ToInt32(Math.Sqrt(Convert.ToDouble(C3*C3 - 4 * C2)))) / 2;
            Int32 UT = X1 * 0x8000 / Convert.ToInt32(AC5) + Convert.ToInt32(AC6);
            // As the above code is not working properly:
            // Using a randomized UT around mean value 
            UT = Convert.ToInt32(Math.Round(SensorData (25800, 1000.0)));
            // Construct the two data register bytes
            outMSB = (byte)(((Convert.ToInt16(UT))>>8)& 0xFF);
            outLSB = (byte)(Convert.ToInt16(UT) & 0xFF);
        }

        private void GetPressure()
        {
            // double pressure = SensorData (100000.0, 10.0);
            // TODO: Add calculation of UP from pressure value randomized around mean value?
            // Using a randomized UP around mean value 
            UInt32 UP = Convert.ToUInt32(Math.Round(SensorData (38990.0, 1000.0)));
            // Handle oversampling setting
            double ossBits = ((int)ctrlMeasurement & 0xC0) >> 6;
            UInt32 correctedUP = UP << (8 - Convert.ToByte(ossBits));
            outMSB = (byte)(((correctedUP)>>16)& 0xFF);
            outLSB = (byte)(((correctedUP)>>8)& 0xFF);
            outXLSB = (byte)((correctedUP) & 0xFF);
        }

        private byte GetCRC (byte[] input, byte[] output)
        {
            var crc = input [0];
            for (int i=1; i<input.Length; i++)
            {
                crc ^= input[i];
            }
            for (int j=0; j<output.Length; j++)
            {
                crc ^= output[j];
            }
            return (byte)(crc);
        }

        private byte outMSB;
        private byte outLSB;
        private byte outXLSB;
        private byte softReset;
        private byte ctrlMeasurement;
        private byte calibAA;
        private byte calibAB;
        private byte calibAC;
        private byte calibAD;
        private byte calibAE;
        private byte calibAF;
        private byte calibB0;
        private byte calibB1;
        private byte calibB2;
        private byte calibB3;
        private byte calibB4;
        private byte calibB5;
        private byte calibB6;
        private byte calibB7;
        private byte calibB8;
        private byte calibB9;
        private byte calibBA;
        private byte calibBB;
        private byte calibBC;
        private byte calibBD;
        private byte calibBE;
        private byte calibBF;

        private uint state;
        private byte registerAddress;
        private byte registerData;
        private byte[] sendData;

        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private enum MeasurementModes
        {
            Temperature = 0x2E,
            Pressure0   = 0x34, // Oversampling setting = 0, sample time 4.5 ms 
            Pressure1   = 0x74, // Oversampling setting = 1, sample time 7.5 ms
            Pressure2   = 0xB4, // Oversampling setting = 2, sample time 13.5 ms
            Pressure3   = 0xF4  // Oversampling setting = 3, sample time 25.5 ms
        }

        private enum States
        {
            Idle             = 0x0,
            ReceivingData    = 0xFD, 
            SendingData      = 0xFC,
        }

        private enum Registers
        {
    	  CalibAA = 0xAA, // Read-Only
	      CalibAB = 0xAB, 
	      CalibAC = 0xAC,
	      CalibAD = 0xAD,
	      CalibAE = 0xAE,
    	  CalibAF = 0xAF,
    	  CalibB0 = 0xB0,
    	  CalibB1 = 0xB1,
    	  CalibB2 = 0xB2,
    	  CalibB3 = 0xB3,
    	  CalibB4 = 0xB4,
    	  CalibB5 = 0xB5,
    	  CalibB6 = 0xB6,
    	  CalibB7 = 0xB7,
    	  CalibB8 = 0xB8,
    	  CalibB9 = 0xB9,
    	  CalibBA = 0xBA,
    	  CalibBB = 0xBB,
    	  CalibBC = 0xBC,
    	  CalibBD = 0xBD,
    	  CalibBE = 0xBE,
    	  CalibBF = 0xBF,
          ChipID = 0xD0, // Read-Only
    	  SoftReset = 0xE0, // Write-Only
    	  CtrlMeasurement = 0xF4, // Read-Write
    	  OutMSB = 0xF6,  // Read-Only
    	  OutLSB = 0xF7,  // Read-Only
    	  OutXLSB = 0xF8  // Read-Only
        }
    }
}

