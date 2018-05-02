//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Hooks
{
    public class WatchpointHookPythonEngine : PythonEngine
    {
        public WatchpointHookPythonEngine(SystemBus sysbus, string script)
        {
            this.sysbus = sysbus;
            this.script = script;

            InnerInit();
            Hook = (address, width) =>
            {
                Scope.SetVariable("address", address);
                Scope.SetVariable("width", width);
                source.Value.Execute(Scope);
            };
        }

        public Action<ulong, SysbusAccessWidth> Hook { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", sysbus);

            source = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(script));
        }

        [Transient]
        private Lazy<ScriptSource> source;

        private readonly string script;
        private readonly object sysbus;
    }
}

