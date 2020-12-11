//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class BMP180 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public BMP180()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            registerAddress = 0;
            this.Log(LogLevel.Noisy, "Reset registers");
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            this.Log(LogLevel.Noisy, "Write with {0} bytes of data: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            registerAddress = (Registers)data[0];

            if(data.Length > 1)
            {
                // skip the first byte as it contains register address
                foreach(var b in data.Skip(1))
                {
                    this.Log(LogLevel.Noisy, "Writing 0x{0:X} to register {1} (0x{1:X})", b, registerAddress);
                    RegistersCollection.Write((byte)registerAddress, b);
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Preparing to read register {0} (0x{0:X})", registerAddress);
            }
        }

        public byte[] Read(int count)
        {
            this.Log(LogLevel.Noisy, "Reading {0} bytes from register {1} (0x{1:X})", count, registerAddress);
            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = RegistersCollection.Read((byte)registerAddress);
                this.Log(LogLevel.Noisy, "Read value {0} from register {1} (0x{1:X})", result[i], registerAddress);
                RegistersAutoIncrement();
            }
            return result;
        }

        public void FinishTransmission()
        {
        }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(value < MinTemperature | value > MaxTemperature)
                {
                    this.Log(LogLevel.Warning, "Temperature is out of range. Supported range: {0} - {1}", MinTemperature, MaxTemperature);
                }
                else
                {
                    temperature = value;
                    this.Log(LogLevel.Noisy, "Sensor temperature set to {0}", temperature);
                }
            }
        }

        public int UncompensatedPressure { get; set; }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.CoefficientCalibrationAA.Define(this, 0x1B); //RO
            Registers.CoefficientCalibrationAB.Define(this, 0xCB); //RO
            Registers.CoefficientCalibrationAC.Define(this, 0xFB); //RO
            Registers.CoefficientCalibrationAD.Define(this, 0xCB); //RO
            Registers.CoefficientCalibrationAE.Define(this, 0xC6); //RO
            Registers.CoefficientCalibrationAF.Define(this, 0x91); //RO
            Registers.CoefficientCalibrationB0.Define(this, 0x7B); //RO
            Registers.CoefficientCalibrationB1.Define(this, 0xA8); //RO

            Registers.CoefficientCalibrationB2.Define(this, 0x7F)
                .WithValueField(0, 8, out coeffCalibB2, FieldMode.Read, name: "AC5[15-8]");

            Registers.CoefficientCalibrationB3.Define(this, 0x75)
                .WithValueField(0, 8, out coeffCalibB3, FieldMode.Read, name: "AC5[7-0]");

            Registers.CoefficientCalibrationB4.Define(this, 0x5A)
                .WithValueField(0, 8, out coeffCalibB4, FieldMode.Read, name: "AC6[15-8]");

            Registers.CoefficientCalibrationB5.Define(this, 0x71)
                .WithValueField(0, 8, out coeffCalibB5, FieldMode.Read, name: "AC6[7-0]");

            Registers.CoefficientCalibrationB6.Define(this, 0x15); //RO
            Registers.CoefficientCalibrationB7.Define(this, 0x7A); //RO
            Registers.CoefficientCalibrationB8.Define(this, 0x0); //RO
            Registers.CoefficientCalibrationB9.Define(this, 0x38); //RO
            Registers.CoefficientCalibrationBA.Define(this, 0x80); //RO
            Registers.CoefficientCalibrationBB.Define(this, 0x0); //RO

            Registers.CoefficientCalibrationBC.Define(this, unchecked((byte)(calibMB >> 8)))
                .WithValueField(0, 8, out coeffCalibBC, FieldMode.Read, name: "MC[15-8]");

            Registers.CoefficientCalibrationBD.Define(this, unchecked((byte)calibMB))
                .WithValueField(0, 8, out coeffCalibBD, FieldMode.Read, name: "MC[7-0]");

            Registers.CoefficientCalibrationBE.Define(this, 0x0B)
                .WithValueField(0, 8, out coeffCalibBE, FieldMode.Read, name: "MD[15-8]");

            Registers.CoefficientCalibrationBF.Define(this, 0x34)
                .WithValueField(0, 8, out coeffCalibBF, FieldMode.Read, name: "MD[7-0]");

            Registers.ChipID.Define(this, 0x55); //RO

            Registers.SoftReset.Define(this, 0x0) //WO
                .WithWriteCallback((_, val) =>
                {
                    if(val == resetCommand)
                    {
                        Reset();
                    }
                });

            Registers.CtrlMeasurement.Define(this, 0x0) //RW
                .WithValueField(0, 5, out ctrlMeasurement , name: "CTRL_MEAS")
                .WithFlag(5, out startConversion, name: "SCO")
                .WithValueField(6, 2, out controlOversampling, name: "OSS")
                .WithWriteCallback((_, __) => HandleMeasurement());

            Registers.OutMSB.Define(this, 0x80)
                .WithValueField(0, 8, out outMSB, FieldMode.Read, name: "OUT_MSB");

            Registers.OutLSB.Define(this, 0x0)
                .WithValueField(0, 8, out outLSB, FieldMode.Read, name: "OUT_LSB");

            Registers.OutXLSB.Define(this, 0x0)
                .WithValueField(0, 8, out outXLSB, FieldMode.Read, name: "OUT_XLSB");
        }

        private void RegistersAutoIncrement()
        {
            if((registerAddress >= Registers.CoefficientCalibrationAA &&
                registerAddress < Registers.CoefficientCalibrationBF) ||
               (registerAddress >= Registers.OutMSB && registerAddress < Registers.OutXLSB))
            {
                registerAddress = (Registers)((int)registerAddress + 1);
                this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", registerAddress);
            }
        }

        private int GetUncompensatedTemperature()
        {
            ushort ac5 = (ushort)((coeffCalibB2.Value << 8) + coeffCalibB3.Value);
            ushort ac6 = (ushort)((coeffCalibB4.Value << 8) + coeffCalibB5.Value);
            short mc = (short)((coeffCalibBC.Value << 8) + coeffCalibBD.Value);
            short md = (short)((coeffCalibBE.Value << 8) + coeffCalibBF.Value);
            // T = (B5+8)/2^4 => B5 = 16T-8
            int b5 = (int)(((uint)(temperature * 10) << 4) - 8);
            // B5 = X1 + X2 => X1 = B5-X2
            // X2 = (MC*2^11)/(X1+MD) = (MC*2^11)/(B5-X2+MD)
            // X2^2+X2(-B5-MD)+2^11MC = 0 => delta = (-B5-MD)^2-2^13MC
            int delta = (int)(Math.Pow(-b5 - md, 2) - (mc << 13));
            // X2 = (-(-B5-MD)+sqrt(delta))/2 = (B5+MD)+sqrt(delta))/2
            int x2 = (int)((int)(b5 + md + Math.Sqrt(delta)) >> 1);
            // X1 = B5-X2
            // X1 = (UT-AC6)*AC5/2^15 => UT = ((2^15X1)/AC5)+AC6 = (2^15(B5-X2)/AC5)+AC6
            return (int)((((b5-x2) << 15)/ac5)+ac6);
        }

        private void HandleMeasurement()
        {
            this.Log(LogLevel.Noisy, "HandleMeasurement set {0}", (MeasurementModes)ctrlMeasurement.Value);
            switch((MeasurementModes)ctrlMeasurement.Value)
            {
                case MeasurementModes.Temperature:
                    var uncompensatedTemp = GetUncompensatedTemperature();
                    outMSB.Value = (byte)((uncompensatedTemp >> 8) & 0xFF);
                    outLSB.Value = (byte)(uncompensatedTemp & 0xFF);
                    break;
                case MeasurementModes.Pressure:
                    var uPressure = UncompensatedPressure << (byte)(8 - controlOversampling.Value);
                    outMSB.Value = (byte)((uPressure >> 16) & 0xFF);
                    outLSB.Value = (byte)((uPressure >> 8) & 0xFF);
                    outXLSB.Value = (byte)(uPressure & 0xFF);
                    break;
                default:
                    break;
            }
            // Clear SCO bit (start of conversion)
            startConversion.Value = false;
            this.Log(LogLevel.Noisy, "Conversion is complete");
        }

        private IFlagRegisterField startConversion;
        private IValueRegisterField controlOversampling;
        private IValueRegisterField outMSB;
        private IValueRegisterField outLSB;
        private IValueRegisterField outXLSB;
        private IValueRegisterField ctrlMeasurement;
        private Registers registerAddress;

        private IValueRegisterField coeffCalibB2;
        private IValueRegisterField coeffCalibB3;
        private IValueRegisterField coeffCalibB4;
        private IValueRegisterField coeffCalibB5;
        private IValueRegisterField coeffCalibBC;
        private IValueRegisterField coeffCalibBD;
        private IValueRegisterField coeffCalibBE;
        private IValueRegisterField coeffCalibBF;

        private decimal temperature;
        private const decimal MinTemperature = -40;
        private const decimal MaxTemperature = 85;
        private const byte resetCommand = 0xB6;
        private const short calibMB = -8711;

        private enum MeasurementModes
        {
            Temperature = 0x0E,
            Pressure    = 0x14,
        }

        private enum Registers
        {
            CoefficientCalibrationAA = 0xAA, // Read-Only
            CoefficientCalibrationAB = 0xAB,
            CoefficientCalibrationAC = 0xAC,
            CoefficientCalibrationAD = 0xAD,
            CoefficientCalibrationAE = 0xAE,
            CoefficientCalibrationAF = 0xAF,
            CoefficientCalibrationB0 = 0xB0,
            CoefficientCalibrationB1 = 0xB1,
            CoefficientCalibrationB2 = 0xB2,
            CoefficientCalibrationB3 = 0xB3,
            CoefficientCalibrationB4 = 0xB4,
            CoefficientCalibrationB5 = 0xB5,
            CoefficientCalibrationB6 = 0xB6,
            CoefficientCalibrationB7 = 0xB7,
            CoefficientCalibrationB8 = 0xB8,
            CoefficientCalibrationB9 = 0xB9,
            CoefficientCalibrationBA = 0xBA,
            CoefficientCalibrationBB = 0xBB,
            CoefficientCalibrationBC = 0xBC,
            CoefficientCalibrationBD = 0xBD,
            CoefficientCalibrationBE = 0xBE,
            CoefficientCalibrationBF = 0xBF,
            ChipID = 0xD0, // Read-Only
            SoftReset = 0xE0, // Write-Only
            CtrlMeasurement = 0xF4, // Read-Write
            OutMSB = 0xF6,  // Read-Only
            OutLSB = 0xF7,  // Read-Only
            OutXLSB = 0xF8  // Read-Only
        }
    }
}

