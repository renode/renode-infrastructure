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
        public enum LineEnding
        {
           None,
           CR,
           CRLF,
           LF
        }

        public static void WriteLine(this IUART uart, string text, bool appendCarriageReturn = true)
        {
           uart.WriteLine(text, appendCarriageReturn);
        }

        public static void WriteLine(this IUART uart, string text, LineEnding lineEnding = LineEnding.CR)
        {
            foreach(var chr in text)
            {
                uart.WriteChar((byte)chr);
            }

            WriteLineEnding(uart, lineEnding);
        }

        private static void WriteLineEnding(IUART uart, LineEnding lineEnding)
        {
            const byte CarriageReturn = 0xD;
            const byte LineFeed = 0xA;

            byte[] eol = new byte[] {};
            switch (lineEnding)
            {
               case LineEnding.CR:    eol = new byte[] {CarriageReturn};            break;
               case LineEnding.CRLF:  eol = new byte[] {CarriageReturn, LineFeed};  break;
               case LineEnding.LF:    eol = new byte[] {LineFeed};                  break;
            };

            foreach(var chr in eol)
            {
                uart.WriteChar(chr);
            }
        }
    }
}
