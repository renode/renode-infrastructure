//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Exceptions;
using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class InterruptPythonEngine : PythonEngine
    {
        public InterruptPythonEngine(Machine machine, ICPUWithHooks cpu, string script)
        {
            this.script = script;
            this.machine = machine;
            this.cpu = cpu;

            InnerInit();

            HookWithExceptionIndex = exceptionIndex =>
            {
                Scope.SetVariable("exceptionIndex", exceptionIndex);
                Execute(code, error =>
                {
                    this.cpu.Log(LogLevel.Error, "Python runtime error: {0}", error);
                    throw new CpuAbortException($"Python runtime error: {error}");
                });
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Machine.MachineKeyword, machine);
            Scope.SetVariable("self", cpu);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        public Action<ulong> HookWithExceptionIndex { get; private set; }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly ICPUWithHooks cpu;
        private readonly Machine machine;
    }
}
