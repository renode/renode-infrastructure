//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;

namespace Antmicro.Renode.Hooks
{
    public sealed class GPIOPythonEngine : PythonEngine
    {
        public GPIOPythonEngine(IGPIOWithHooks gpio, string script)
        {
            this.script = script;
            this.gpio = gpio;

            InnerInit();

            Hook = state =>
            {
                Scope.SetVariable("state", state);
                Execute(code, error =>
                {
                    Logger.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", gpio);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        public Action<bool> Hook { get; }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly IGPIOWithHooks gpio;
    }
}

