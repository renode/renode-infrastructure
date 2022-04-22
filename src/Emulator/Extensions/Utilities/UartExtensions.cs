//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Utilities
{
    public static class UartExtensions
    {
        public static string DumpHistoryBuffer(this IUART uart, int limit = 0)
        {
            var emu = EmulationManager.Instance.CurrentEmulation;
            if(!emu.BackendManager.TryGetBackendFor(uart, out var backend))
            {
                throw new RecoverableException($"No backend found for {uart}");
            }

            if(backend is UARTBackend uartBackend)
            {
                return uartBackend.DumpHistoryBuffer(limit);
            }
            
            throw new RecoverableException($"Unsupported type of backend for {uart}: {backend.GetType()}");
        }
        
        public static void WriteLine(this IUART uart, string text, bool appendCarriageReturn = true)
        {
            uart.WriteLine(text, appendCarriageReturn ? LineEnding.CR : LineEnding.None);
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
            const byte CarriageReturn = (byte)'\r';
            const byte LineFeed = (byte)'\n';

            switch(lineEnding)
            {
               case LineEnding.CR:
                   uart.WriteChar(CarriageReturn);
                   break;
                   
               case LineEnding.CRLF:
                   uart.WriteChar(CarriageReturn);
                   uart.WriteChar(LineFeed);
                   break;
                   
               case LineEnding.LF:
                   uart.WriteChar(LineFeed);
                   break;
            };
        }

        public enum LineEnding
        {
           None,
           CR,
           CRLF,
           LF
        }
    }
}
