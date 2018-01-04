//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;

namespace Antmicro.Renode.Hooks
{
    public sealed class UserStatePythonEngine : PythonEngine
    {
        public UserStatePythonEngine(Machine machine, string script)
        {
            Script = script;
            Machine = machine;

            InnerInit();

            Hook = state =>
            {
                Scope.SetVariable("state", state);
                Source.Value.Execute(Scope);
            };
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable(Machine.MachineKeyword, Machine);
            Scope.SetVariable("self", Machine);
            Source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(Script));
        }

        public Action<string> Hook { get; private set; }

        [Transient]
        private Lazy<ScriptSource> Source;
        private readonly string Script;
        private readonly Machine Machine;
    }
}

