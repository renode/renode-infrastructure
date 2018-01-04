//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class DummySensor : IDoubleWordPeripheral, ITemperatureSensor, IHumiditySensor
    {
        public uint ReadDoubleWord(long offset)
        {
            throw new NotImplementedException();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            Temperature = 0;
            Humidity = 0;
        }

        public decimal Temperature
        {
            get
            {
                return temperature;
            }
            set
            {
                temperature = value;
                TemperatureUpdateCounter++;
            }
        }

        public decimal Humidity
        {
            get
            {
                return humidity;
            }
            set
            {
                humidity = value;
                HumidityUpdateCounter++;
            }
        }

        public int TemperatureUpdateCounter { get; set; }
        public int HumidityUpdateCounter { get; set; }

        private decimal temperature;
        private decimal humidity;
    }
}
