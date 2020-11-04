//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LSM303DLHC_Gyroscope : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor, ITemperatureSensor
    {
        public LSM303DLHC_Gyroscope()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void FinishTransmission()
        {
            regAddress = 0;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            regAddress = 0;
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            this.Log(LogLevel.Noisy, "Write with {0} bytes of data", data.Length);
            regAddress = (Registers)data[0];

            if(data.Length > 1)
            {
                // skip the first byte as it contains register address
                foreach(var b in data.Skip(1))
                {
                    this.Log(LogLevel.Debug,"Writing 0x{0:X} to register {1} (0x{1:X})", b, regAddress);
                    RegistersCollection.Write((byte)regAddress, b);
                    regAddress = (Registers)((int)regAddress + 1);
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Preparing to read register {0} (0x{0:X})", regAddress);
                dataReady.Value = IsInSleepMode();
                dataLock.Value = false;
            }
        }

        public byte[] Read(int count)
        {
            this.Log(LogLevel.Debug, "Reading {0} bytes from register {1} (0x{1:X})", count, regAddress);
            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                if(regAddress == Registers.DataOutXH)
                {
                   dataLock.Value = true;
                }
                result[i] = RegistersCollection.Read((byte)regAddress);
                this.Log(LogLevel.Noisy, "Read value {0}", result[i]);
                regAddress = (Registers)((int)regAddress + 1);
            }
            return result;
        }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(value < MinTemperature || value > MaxTemperature)
                {
                    this.Log(LogLevel.Warning, "Temperature is out of range");
                }
                else
                {
                    temperature = value;
                    this.Log(LogLevel.Noisy, "Sensor temperature set to {0}", temperature);
                }
            }
        }

        public decimal MagneticFieldX
        {
            get => magneticFieldX;
            set
            {
                if(!IsMagneticFieldOutOfRange(value))
                {
                    magneticFieldX = value;
                    this.Log(LogLevel.Noisy, "MagneticFieldX set to {0}", magneticFieldX);
                }
            }
        }

        public decimal MagneticFieldY
        {
            get => magneticFieldY;
            set
            {
                if(!IsMagneticFieldOutOfRange(value))
                {
                    magneticFieldY = value;
                    this.Log(LogLevel.Noisy, "MagneticFieldY set to {0}", magneticFieldY);
                }
            }
        }

        public decimal MagneticFieldZ
        {
            get => magneticFieldZ;
            set
            {
                if(!IsMagneticFieldOutOfRange(value))
                {
                    magneticFieldZ = value;
                    this.Log(LogLevel.Noisy, "MagneticFieldZ set to {0}", magneticFieldZ);
                }
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.ConfigA.Define(this) //RW
                .WithTag("PREREQ1", 0, 2)
                .WithTag("DATA_RATE_BITS", 2, 3)
                .WithTag("PREREQ2", 5, 2)
                .WithFlag(7, out temperatureSensorEnable, name: "TEMP_SENSOR_ENABLE");

            Registers.ConfigB.Define(this) //RW
                .WithTag("PREREQ", 0, 5)
                .WithValueField(5, 3, out gainConfiguration, name: "GAIN");

            Registers.Mode.Define(this) //RW
                .WithValueField(0, 2, out sensorOperatingMode, name: "MODE_SELECT")
                .WithTag("PREREQ", 2, 6);

            Registers.DataOutXH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X_MAGNETIC_FIELD_DATA[15:8]", valueProviderCallback: _ => Convert(MagneticFieldX, upperByte: true));

            Registers.DataOutXL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X_MAGNETIC_FIELD_DATA[7:0]", valueProviderCallback: _ => Convert(MagneticFieldX, upperByte: false));

            Registers.DataOutZH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z_MAGNETIC_FIELD_DATA[15:8]", valueProviderCallback: _ => Convert(MagneticFieldZ, upperByte: true));

            Registers.DataOutZL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z_MAGNETIC_FIELD_DATA[7:0]", valueProviderCallback: _ => Convert(MagneticFieldZ, upperByte: false));

            Registers.DataOutYH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y_MAGNETIC_FIELD_DATA[15:8]", valueProviderCallback: _ => Convert(MagneticFieldY, upperByte: true));

            Registers.DataOutYL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y_MAGNETIC_FIELD_DATA[7:0]", valueProviderCallback: _ => Convert(MagneticFieldY, upperByte: false));

            Registers.DataReady.Define(this, 0x01) //RO
                .WithFlag(0, out dataReady, FieldMode.Read, name: "DATA_READY")
                .WithFlag(1, out dataLock, FieldMode.Read, name: "DATA_LOCK")
                .WithReservedBits(2, 6);

            Registers.TemperatureDataOutH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "TEMP_DATA[11:4]", valueProviderCallback: _ => ConvertTemperature(temperature, upperByte: true));

            Registers.TemperatureDataOutL.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 4, FieldMode.Read, name: "TEMP_DATA[3:0]", valueProviderCallback: _ => ConvertTemperature(temperature, upperByte: false));
        }

        private bool IsInSleepMode()
        {
            if(sensorOperatingMode.Value == (uint)OperatingModes.SleepMode0 ||
               sensorOperatingMode.Value == (uint)OperatingModes.SleepMode1)
            {
                this.Log(LogLevel.Debug, "Sensor is placed in sleep mode");
                return false;
            }
            else
            {
                return true;
            }
        }

        private ushort GetGain()
        {
            ushort gain = 0; // [LSB/Gauss]
            switch(gainConfiguration.Value)
            {
                case 0:
                    gain = 980;
                    break;
                case 1:
                    gain = 1100;
                    break;
                case 2:
                    gain = 760;
                    break;
                case 3:
                    gain = 600;
                    break;
                case 4:
                    gain = 400;
                    break;
                case 5:
                    gain = 355;
                    break;
                case 6:
                    gain = 295;
                    break;
                case 7:
                    gain = 205;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported value of sensor gain.");
                    break;
            }
            return gain;
        }

        private bool IsMagneticFieldOutOfRange(decimal magneticField)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if(magneticField < MinMagneticField || magneticField > MaxMagneticField)
            {
                this.Log(LogLevel.Warning, "MagneticField is out of range, use value from the range <{0};{1}>",
                                            MinMagneticField, MaxMagneticField);
                return true;
            }
            return false;
        }

        private byte Convert(decimal value, bool upperByte)
        {
            decimal convertedValue = (decimal)(value * GetGain());
            short convertedValueAsShort = (short)convertedValue;
            return upperByte ? (byte)(convertedValueAsShort >> 8) : (byte)convertedValueAsShort;
        }

        private byte ConvertTemperature(decimal value, bool upperByte)
        {
            if(!temperatureSensorEnable.Value)
            {
                this.Log(LogLevel.Warning, "Temperature sensor disable");
                return 0x00;
            }
            return upperByte ? (byte)((short)value >> 8) : (byte)((short)value >> 4) ;
        }

        private decimal temperature;
        private Registers regAddress;
        private IFlagRegisterField temperatureSensorEnable;
        private IFlagRegisterField dataReady;
        private IFlagRegisterField dataLock;
        private IValueRegisterField sensorOperatingMode;
        private IValueRegisterField gainConfiguration;
        private decimal magneticFieldX;
        private decimal magneticFieldY;
        private decimal magneticFieldZ;

        private const decimal MinMagneticField = -29.5m;
        private const decimal MaxMagneticField = 29.5m;
        private const decimal MinTemperature = -40;
        private const decimal MaxTemperature = 85;

        private enum OperatingModes : byte
        {
            ContiniousConversionMode = 0x0,
            SingleConversionMode = 0x1,
            SleepMode0 = 0x2,
            SleepMode1 = 0x3,
        }

        private enum Registers
        {
            // Magnetic field sensing registers:
            ConfigA = 0x00,
            ConfigB = 0x01,
            Mode = 0x02,
            DataOutXH = 0x03,
            DataOutXL = 0x04,
            DataOutZH = 0x05,
            DataOutZL = 0x06,
            DataOutYH = 0x07,
            DataOutYL = 0x08,
            DataReady = 0x09,
            InterruptA = 0x0A,
            InterruptB = 0x0B,
            InterruptC = 0x0C,
            // Reserved: 0x0D - 0x30
            TemperatureDataOutH = 0x31,
            TemperatureDataOutL = 0x32,
            // Reserved: 0x33 - 0x3A
        }
    }
}
