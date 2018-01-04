//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class TI_LM74 : ISPIPeripheral, ITemperatureSensor
    {
        public TI_LM74()
        {
            Reset();
        }

        public void FinishTransmission()
        {
            Reset();
        }

        public void Reset()
        {
            isFirstByte = true;
            currentReadOut = 0;
        }

        public byte Transmit(byte data)
        {
            byte value = 0;
            if(isFirstByte)
            {
                //The 3 LSB are set to 1. 0x1000 = 0.0625C. Decimal->Int->UInt conversion to handle negative values.
                currentReadOut = (((uint)(int)(Temperature * 10000 / 625) << 3) | 0x7);
                value = (byte)(currentReadOut >> 8);
            }
            else
            {
                value = (byte)(currentReadOut & 0xFF);
            }
            isFirstByte = !isFirstByte;
            return value;
        }

        public decimal Temperature
        {
            get
            {
                return temperature;
            }
            set
            {
                if(MinTemperature > value || value > MaxTemperature)
                {
                    throw new RecoverableException("The temperature value must be between {0} and {1}.".FormatWith(MinTemperature, MaxTemperature));
                }
                temperature = value;
            }
        }

        private decimal temperature;
        private uint currentReadOut;
        private bool isFirstByte;

        private const decimal MaxTemperature = 150;
        private const decimal MinTemperature = -55;
    }
}
