//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;
using Antmicro.Renode.Core;

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
                Source.Value.Execute(Scope);
            });
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", emulation);
            Source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(script));
        }

        public Action<long> Hook { get; private set; }

        private readonly string script;
        private readonly Emulation emulation;

        [Transient]
        private Lazy<ScriptSource> Source;
    }
}

