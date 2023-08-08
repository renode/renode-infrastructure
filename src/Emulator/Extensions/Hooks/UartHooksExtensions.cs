//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Hooks
{
    public static class UartHooksExtensions
    {
        public static void AddCharHook(this IUART uart, Func<byte, bool> predicate, Action<byte> hook)
        {
            uart.CharReceived += x =>
            {
                if(predicate(x))
                {
                    hook(x);
                }
            };
        }

        public static void AddLineHook(this IUART uart, Func<string, bool> predicate, Action<string> hook)
        {
            var currentLine = string.Empty;
            uart.CharReceived += x =>
            {
                if((x == 10 || x == 13))
                {
                    if(predicate(currentLine))
                    {
                        hook(currentLine);
                    }
                    currentLine = string.Empty;
                    return;
                }
                currentLine += (char)x;
            };
        }

        public static void AddLineHook(this IUART uart, [AutoParameter] IMachine machine, string contains, string pythonScript)
        {
            var engine = new UartPythonEngine(machine, uart, pythonScript);
            uart.AddLineHook(x => x.Contains(contains), engine.Hook);
        }
    }
}

