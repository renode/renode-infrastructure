//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Sensors
{
    // This class implements a common ST sensor
    // register handling logic.
    // It can be used as a base for all I2C sensors that
    // support 7-bit-length registers addresses.

    // it should be enum instead of IConvertible, but earlier versions of C# do not support this
    public abstract class ST_I2CSensorBase<T> : I2CPeripheralBase<T> where T : IConvertible
    {
        public ST_I2CSensorBase() : base(7)
        {
        }

        protected byte GetScaledValue(decimal value, short sensitivity, bool upperByte)
        {
            var scaled = (short)(value * sensitivity);
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        protected short CalculateScale(int minVal, int maxVal, int width)
        {
            var range = maxVal - minVal;
            return (short)(((1 << width) / range) - 1);
        }
    }
}
