//
// Copyright (c) 2010-2025 Antmicro
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
    public sealed class RiscVStackAccessPythonEngine : PythonEngine
    {
        public RiscVStackAccessPythonEngine(BaseRiscV cpu, string script = null, OptionalReadFilePath path = null)
        {
            if((script == null) == (path == null))
            {
                throw new ConstructionException("Parameters '{nameof(script)}' and '{nameof(path)}' cannot be both set or both unset");
            }

            this.cpu = cpu;

            this.script = script;
            this.path = path;

            InnerInit();

            Hook = (address, width, isWrite) =>
            {
                Scope.SetVariable("address", address);
                Scope.SetVariable("width", width);
                Scope.SetVariable("is_write", isWrite);
                Execute(code, error =>
                {
                    var message = $"Python runtime error: {error}";
                    this.cpu.Log(LogLevel.Error, message);
                    throw new CpuAbortException(message);
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
                source = Engine.CreateScriptSourceFromFile(new ReadFilePath(path));
            }

            code = Compile(source);
        }

        public Action<ulong, uint, bool> Hook { get; }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly string path;

        private readonly BaseRiscV cpu;
    }
}

