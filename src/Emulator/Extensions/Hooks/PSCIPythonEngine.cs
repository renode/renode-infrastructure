//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;

namespace Antmicro.Renode.Hooks
{
    public sealed class PSCIPythonEngine : PythonEngine
    {
        public PSCIPythonEngine(ICPUWithPSCI cpu, string script, ulong functionIdentifier)
        {
            this.script = script;
            this.cpu = cpu;

            InnerInit();

            Hook = () =>
            {
                Scope.SetVariable("self", cpu);
                Scope.SetVariable("function_id", functionIdentifier);
                Execute(code, error =>
                {
                    Logger.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", cpu);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        public Action Hook { get; }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly ICPUWithPSCI cpu;
    }
}

