//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public static class BCDHelper
    {
        public static byte EncodeToBCD(byte numericValue)
        {
            if(numericValue > 99 || numericValue < 0)
            {
                throw new ArgumentException($"Invalid value: {numericValue}");
            }

            var nibbleLow = numericValue % 10;
            var nibbleHigh = numericValue / 10;
            return (byte)((nibbleHigh << 4) | nibbleLow);
        }

        public static byte DecodeFromBCD(byte bcdValue)
        {
            var units = bcdValue & 0x0F;
            var tens = (bcdValue & 0xF0) >> 4;
            return (byte)((tens * 10) + units);
        }
    }
}
