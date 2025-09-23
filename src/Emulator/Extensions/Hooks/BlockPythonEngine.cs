//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Peripherals.CPU;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public sealed class BlockPythonEngine : PythonEngine
    {
        public BlockPythonEngine(IMachine mach, ICPUWithHooks cpu, string script)
        {
            Script = script;
            CPU = cpu;
            Machine = mach;

            InnerInit();

            Hook = (_, pc) =>
            {
                Scope.SetVariable("pc", pc);
                Execute(code, error =>
                {
                    CPU.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };

            HookWithSize = (pc, size) =>
            {
                Scope.SetVariable("pc", pc);
                Scope.SetVariable("size", size);
                Execute(code, error =>
                {
                    CPU.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public CpuAddressHook Hook { get; private set; }

        public Action<ulong, uint> HookWithSize { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Core.Machine.MachineKeyword, Machine);
            Scope.SetVariable("cpu", CPU);
            Scope.SetVariable("self", CPU);
            var source = Engine.CreateScriptSourceFromString(Script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;

        private readonly string Script;
        private readonly ICPUWithHooks CPU;
        private readonly IMachine Machine;
    }
}