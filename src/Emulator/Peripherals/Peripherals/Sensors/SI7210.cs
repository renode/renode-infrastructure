//
// Copyright (c) 2010-2023 Antmicro
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
    public class SI7210 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public SI7210(IMachine machine, byte offset = 0, byte gain = 0)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();

            temperatureOffset = offset;
            temperatureGain = gain;
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            registerAddress = (Registers)data[0];

            if(data.Length > 1)
            {
                foreach(var value in data.Skip(1))
                {
                    RegistersCollection.Write((byte)registerAddress, value);
                }
            }
        }

        public byte[] Read(int count)
        {
            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Warning, "Trying to read without setting address");
                return new byte[] { 0 };
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.Read((byte)((int)registerAddress + i));
            }

            if(autoIncrement.Value)
            {
                registerAddress += 1;
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = null;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            registerAddress = null;
            currentTemperature = ZeroCelsiusApproximation;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(IsTemperatureOutOfRange(value))
                {
                    return;
                }
                temperature = value;

                var measurement = (ushort)(short)(temperature * TemperatureGainApproximation) << 3;
                currentTemperature = ZeroCelsiusApproximation + (ulong)measurement;
            }
        }

        private void DefineRegisters()
        {
            Registers.ChipID.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "ChipID",
                    valueProviderCallback: _ => (uint)ChipID)
            ;

            Registers.MeasurementHigh.Define(this)
                .WithValueField(0, 7, name: "DSPSIGM",
                    valueProviderCallback: _ => currentTemperature >> 8)
                .WithTaggedFlag("fresh", 7)
            ;

            Registers.MeasurementLow.Define(this)
                .WithValueField(0, 8, name: "DSPSIGL",
                    valueProviderCallback: _ => currentTemperature)
            ;

            Registers.EnableTemperatureReadout.Define(this)
                .WithValueField(0, 3, out enableTemperatureReadout, name: "DSPSIGSEL")
                .WithReservedBits(3, 5)
            ;

            Registers.PowerControl.Define(this)
                .WithTaggedFlag("sleep", 0)
                .WithTaggedFlag("stop", 1)
                .WithTaggedFlag("oneburst", 2)
                .WithTaggedFlag("usestore", 3)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("meas", 7)
            ;

            Registers.AutoIncrement.Define(this)
                .WithFlag(0, out autoIncrement, name: "ARAUTOINC")
                .WithReservedBits(1, 7)
            ;

            Registers.OTPAddress.Define(this)
                .WithValueField(0, 8, name: "OTP_Addr",
                    writeCallback: (_, value) => otpData = HandleOTP_ReadRequest((byte)value))
            ;

            Registers.OTPData.Define(this)
                .WithValueField(0, 8, name: "OTP_Data",
                    valueProviderCallback: _ => 
                    {
                        return (uint)otpData;
                    },
                    writeCallback: (_, value) => otpData = (byte)value)
            ;

            Registers.OTPControl.Define(this)
                .WithTag("otp_busy", 0, 1)
                .WithFlag(1, out otpReadEnable, name: "OTP_Control.otp_read_en")
                .WithTag("reserved", 2, 6)
            ;
        }

        private byte HandleOTP_ReadRequest(byte offset)
        {
            if(!otpReadEnable.Value)
            {
                return 0;
            }
            switch((OTPRegisters)offset)
            {
                case OTPRegisters.PartBase:
                    // Base part number dropping the “Si72”, for example 01 for Si7201
                    return PartNumber;
                case OTPRegisters.TempOffset:
                    // Temp sensor offset adjustment
                    return temperatureOffset;
                case OTPRegisters.TempGain:
                    // Temp sensor gain adjustment
                    return temperatureGain;
                default:
                    this.Log(LogLevel.Noisy, "Tried to read OTP_DATA offset: 0x{0:X}, returning 0", offset);
                    return 0;
            }
        }

        private bool IsTemperatureOutOfRange(decimal temperature)
        {
            if (temperature < MinTemperature || temperature > MaxTemperature)
            {
                this.Log(LogLevel.Warning, "Temperature {0} is out of range, use value from the range <{1:F2};{2:F2}>", temperature, MinTemperature, MaxTemperature);
                return true;
            }
            return false;
        }

        private Registers? registerAddress;

        private decimal temperature;

        private byte otpData;
        private IFlagRegisterField otpReadEnable;
        private IFlagRegisterField autoIncrement;
        private IValueRegisterField enableTemperatureReadout;
        private byte temperatureOffset;
        private byte temperatureGain;

        private ulong currentTemperature;

        private const decimal MinTemperature = -65.0m;
        private const decimal MaxTemperature = 150.0m;

        private const ushort ChipID = 0x14;
        private const byte PartNumber = 0x10;

        private const ulong ZeroCelsiusApproximation = 0x3920;
        private const decimal TemperatureGainApproximation = 6.667m;

        private enum Registers : byte
        {
            ChipID = 0xC0,
            MeasurementHigh = 0xC1,
            MeasurementLow = 0xC2,
            EnableTemperatureReadout = 0xC3,
            PowerControl = 0xC4,
            AutoIncrement = 0xC5,
            OTPAddress = 0xE1,
            OTPData = 0xE2,
            OTPControl = 0xE3,
            TestFieldGenerator = 0xE4,
        }

        private enum OTPRegisters : byte
        {
            PartBase = 0x14,
            TempOffset = 0x1D,
            TempGain = 0x1E,
        }
    }
}
