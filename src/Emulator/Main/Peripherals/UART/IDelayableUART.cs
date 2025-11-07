//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class IDelayableUARTExtensions
    {
        public static TimeInterval GetActualTransmissionDuration(this IUART @this, uint dataBits = 7)
        {
            return TimeInterval.FromNanoseconds((ulong)Math.Ceiling(
                (
                    1 // start bit
                    + dataBits
                    + (@this.ParityBit == Parity.None ? 0 : 1)
                    + @this.GetNumberOfStopBits()
                ) / @this.BaudRate * 1e9
            ));
        }

        public static double GetNumberOfStopBits(this IUART @this)
        {
            switch(@this.StopBits)
            {
            case Bits.None:
            default:
                return 0;
            case Bits.One:
                return 1;
            case Bits.Half:
                return 0.5;
            case Bits.OneAndAHalf:
                return 1.5;
            case Bits.Two:
                return 2;
            }
        }
    }

    public interface IDelayableUART : IUART
    {
        TimeInterval CharacterTransmissionDelay { get; }
    }
}
