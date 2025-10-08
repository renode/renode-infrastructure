//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

using Microsoft.Scripting.Hosting;

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

        public Action Hook { get; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", cpu);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly ICPUWithPSCI cpu;
    }
}