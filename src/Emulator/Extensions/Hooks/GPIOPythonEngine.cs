//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

using Microsoft.Scripting.Hosting;

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

        public Action<bool> Hook { get; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", gpio);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly IGPIOWithHooks gpio;
    }
}