//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;

namespace Antmicro.Renode.Hooks
{
    public sealed class UartPythonEngine : PythonEngine
    {
        public UartPythonEngine(Machine machine, IUART uart, string script)
        {
            Script = script;
            Uart = uart;
            Machine = machine;

            InnerInit();

            Hook = line =>
            {
                Scope.SetVariable("line", line);
                Source.Value.Execute(Scope);
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Machine.MachineKeyword, Machine);
            Scope.SetVariable("uart", Uart);
            Scope.SetVariable("self", Uart);
            Source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(Script));
        }

        public Action<string> Hook { get; private set; }

        [Transient]
        private Lazy<ScriptSource> Source;
        private readonly string Script;
        private readonly IUART Uart;
        private readonly Machine Machine;
    }
}

