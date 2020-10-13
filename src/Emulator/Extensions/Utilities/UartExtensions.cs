//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Utilities
{
    public static class UartExtensions
    {
        public static void WriteLine(this IUART uart, string text, bool appendCarriageReturn = true)
        {
            const byte CarriageReturn = 0xD;

            foreach(var chr in text)
            {
                uart.WriteChar((byte)chr);
            }
            if(appendCarriageReturn)
            {
                uart.WriteChar(CarriageReturn);
            }
        }
    }
}
