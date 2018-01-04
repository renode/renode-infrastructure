//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MAX6682MUA : ISPIPeripheral
    {
        public MAX6682MUA()
        {
            Reset();
        }

        public void Reset()
        {
            isFirstByte = true;
            currentReadOut = 0;
        }

        public void FinishTransmission()
        {
            Reset();
        }

        public byte Transmit(byte data)
        {
            byte value = 0;
            if(isFirstByte)
            {
                currentReadOut = (uint)Temperature * 8;
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

        private const decimal MaxTemperature = 125;
        private const decimal MinTemperature = -55;
    }
}
