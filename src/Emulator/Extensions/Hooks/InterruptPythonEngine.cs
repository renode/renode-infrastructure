//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
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
                source.Value.Execute(Scope);
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Machine.MachineKeyword, machine);
            Scope.SetVariable("self", cpu);
            source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(script));
        }

        public Action<ulong> HookWithExceptionIndex { get; private set; }

        [Transient]
        private Lazy<ScriptSource> source;
        private readonly string script;
        private readonly ICPUWithHooks cpu;
        private readonly Machine machine;
    }
}
