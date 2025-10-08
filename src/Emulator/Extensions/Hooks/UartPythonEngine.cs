//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public sealed class UartPythonEngine : PythonEngine
    {
        public UartPythonEngine(IMachine machine, IUART uart, string script)
        {
            Script = script;
            Uart = uart;
            Machine = machine;

            InnerInit();

            Hook = line =>
            {
                Scope.SetVariable("line", line);
                Execute(code, error =>
                {
                    Uart.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public Action<string> Hook { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Core.Machine.MachineKeyword, Machine);
            Scope.SetVariable("uart", Uart);
            Scope.SetVariable("self", Uart);
            var source = Engine.CreateScriptSourceFromString(Script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;
        private readonly string Script;
        private readonly IUART Uart;
        private readonly IMachine Machine;
    }
}