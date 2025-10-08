//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class WatchpointHookPythonEngine : PythonEngine
    {
        public WatchpointHookPythonEngine(IBusController sysbus, string script)
        {
            this.sysbus = sysbus;
            this.script = script;

            InnerInit();
            Hook = (cpu, address, width, value) =>
            {
                Scope.SetVariable("cpu", cpu);
                Scope.SetVariable("address", address);
                Scope.SetVariable("width", width);
                Scope.SetVariable("value", value);
                Execute(code, error =>
                {
                    this.sysbus.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public BusHookDelegate Hook { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", sysbus);

            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly IBusController sysbus;
    }
}