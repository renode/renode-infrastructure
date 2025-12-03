//
// Copyright (c) 2010-2022 Antmicro
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

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class SyncPointHookPythonEngine : PythonEngine
    {
        public SyncPointHookPythonEngine(string script, Emulation emulation)
        {
            this.script = script;
            this.emulation = emulation;
            InnerInit();

            Hook = new Action<long>(syncCount =>
            {
                Scope.SetVariable("syncCount", syncCount);
                Execute(code, error =>
                {
                    Logger.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            });
        }

        public Action<long> Hook { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", emulation);
            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly Emulation emulation;
    }
}