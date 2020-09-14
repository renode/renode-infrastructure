//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Hooks
{
    public sealed class RiscVInstructionPythonEngine : PythonEngine
    {
        public RiscVInstructionPythonEngine(BaseRiscV cpu, string pattern, string script = null, string path = null)
        {
            if((script == null && path == null) || (script != null && path != null))
            {
                throw new ConstructionException("Parameters 'script' and 'path' cannot be both set or both unset");
            }

            this.cpu = cpu;
            this.pattern = pattern;

            this.script = script;
            this.path = path;

            InnerInit();

            Hook = (instr) =>
            {
                Scope.SetVariable("instruction", instr);

                source.Value.Execute(Scope);
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("cpu", cpu);
            Scope.SetVariable("machine", cpu.GetMachine());
            Scope.SetVariable("state", cpu.UserState);

            if(script != null)
            {
                source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(script));
            }
            else
            {
                if(!File.Exists(path))
                {
                    throw new RecoverableException($"Couldn't find the script file: {path}");
                }
                source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromFile(path));
            }
        }

        public Action<ulong> Hook { get; }

        [Transient]
        private Lazy<ScriptSource> source;

        private readonly string script;
        private readonly string path;

        private readonly BaseRiscV cpu;
        private readonly string pattern;
    }
}

