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
    public class HS3001 : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, ITemperatureSensor, IHumiditySensor
    {
        public HS3001()
        {
            RegistersCollection = new WordRegisterCollection(this);
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

        public WordRegisterCollection RegistersCollection { get; }

        public decimal Temperature
        {
            get; set;
        }

        public decimal Humidity
        {
            get; set;
        }

        private void DefineRegisters()
        {
            Registers.HumiditySensorResolutionRead.Define(this)
                .WithTag("Humidity Sensor Resolution Read", 0, 16);

            Registers.TemperatureSensorResolutionRead.Define(this)
                .WithTag("Temperature Sensor Resolution Read", 0, 16);

            Registers.ReadSensorIDHigh.Define(this)
                .WithTag("Read Sensor ID Upper Bytes", 0, 16);

            Registers.ReadSensorIDLow.Define(this)
                .WithTag("Read Sensor ID Lower Bytes", 0, 16);

            Registers.HumiditySensorResolutionWrite.Define(this)
                .WithTag("Humidity Sensor Resolution Write", 0, 16);

            Registers.TemperatureSensorResolutionWrite.Define(this)
                .WithTag("Temperature Sensor Resolution Write", 0, 16);
        }

        private enum Registers : byte
        {
            HumiditySensorResolutionRead = 0x6,
            TemperatureSensorResolutionRead = 0x11,
            ReadSensorIDHigh = 0x1E,
            ReadSensorIDLow = 0x1F,
            HumiditySensorResolutionWrite = 0x46,
            TemperatureSensorResolutionWrite = 0x51
        }
    }
}
