//
// Copyright (c) 2010-2022 Antmicro
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
    public class TMP103 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public TMP103(IMachine machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();
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
                this.Log(LogLevel.Error, "Trying to read without setting address");
                return new byte[] {};
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.Read((byte)((int)registerAddress + i));
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

            currentTemperature = 0;
            temperatureLowThreshold = defaultLowThreshold;
            temperatureHighThreshold = defaultHighThreshold;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get => (decimal)currentTemperature;
            set
            {
                if(value > sbyte.MaxValue)
                {
                    currentTemperature = sbyte.MaxValue;
                    this.Log(LogLevel.Warning, "{0} is higher than maximum of {1} and has been clamped", value, sbyte.MaxValue);
                }
                else if(value < sbyte.MinValue)
                {
                    currentTemperature = sbyte.MinValue;
                    this.Log(LogLevel.Warning, "{0} is lower than minimum of {1} and has been clamped", value, sbyte.MinValue);
                }
                else
                {
                    currentTemperature = (sbyte)value;
                }

                UpdateThresholdFlags();
            }
        }

        private void UpdateThresholdFlags()
        {
            if(latchFlags.Value)
            {
                temperatureLowFlag.Value |= currentTemperature < temperatureLowThreshold;
                temperatureHighFlag.Value |= currentTemperature > temperatureHighThreshold;
            }
            else
            {
                temperatureLowFlag.Value = currentTemperature < temperatureLowThreshold;
                temperatureHighFlag.Value = currentTemperature > temperatureHighThreshold;
            }
        }

        private void DefineRegisters()
        {
            Registers.Temperature.Define(this)
                .WithValueField(0, 8, name: "TEMP.temp",
                    valueProviderCallback: _ => (uint)currentTemperature,
                    writeCallback: (_, value) => currentTemperature = (sbyte)value)
            ;

            Registers.Configuration.Define(this, 0x2)
                .WithTag("CONF.mode", 0, 2)
                .WithFlag(2, out latchFlags, name: "CONF.latch")
                .WithFlag(3, out temperatureLowFlag, name: "CONF.fl")
                .WithFlag(4, out temperatureHighFlag, name: "CONF.fh")
                .WithTag("CONF.conv_rate", 5, 2)
                .WithTaggedFlag("CONF.id", 7)
            ;

            Registers.TemperatureLow.Define(this)
                .WithValueField(0, 8, name: "TLOW.tlow",
                    valueProviderCallback: _ => (uint)temperatureLowThreshold,
                    writeCallback: (_, value) =>
                    {
                        temperatureLowThreshold = (sbyte)value;
                        UpdateThresholdFlags();
                    })
            ;

            Registers.TemperatureHigh.Define(this)
                .WithValueField(0, 8, name: "THIGH.thigh",
                    valueProviderCallback: _ => (uint)temperatureHighThreshold,
                    writeCallback: (_, value) =>
                    {
                        temperatureHighThreshold = (sbyte)value;
                        UpdateThresholdFlags();
                    })
            ;
        }

        private Registers? registerAddress;

        private sbyte currentTemperature;
        private sbyte temperatureLowThreshold;
        private sbyte temperatureHighThreshold;

        private IFlagRegisterField latchFlags;
        private IFlagRegisterField temperatureLowFlag;
        private IFlagRegisterField temperatureHighFlag;

        private const sbyte defaultLowThreshold = -10;
        private const sbyte defaultHighThreshold = 60;

        private enum Registers : byte
        {
            Temperature = 0x00,
            Configuration = 0x01,
            TemperatureLow = 0x02,
            TemperatureHigh = 0x03,
        }
    }
}
