//
// Copyright (c) 2010-2022 Antmicro
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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Hooks
{
    public sealed class RiscVInstructionPythonEngine : PythonEngine
    {
        public RiscVInstructionPythonEngine(BaseRiscV cpu, string pattern, string script = null, OptionalReadFilePath path = null)
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
            Scope.SetVariable("cpu", cpu);
            Scope.SetVariable("machine", cpu.GetMachine());
            Scope.SetVariable("state", cpu.UserState);

            ScriptSource source;
            if(script != null)
            {
                source = Engine.CreateScriptSourceFromString(script);
            }
            else
            {
                if(!File.Exists(path))
                {
                    throw new RecoverableException($"Couldn't find the script file: {path}");
                }
                source = Engine.CreateScriptSourceFromFile(path);
            }

            code = Compile(source);
        }

        public Action<ulong> Hook { get; }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly string path;

        private readonly BaseRiscV cpu;
        private readonly string pattern;
    }
}

