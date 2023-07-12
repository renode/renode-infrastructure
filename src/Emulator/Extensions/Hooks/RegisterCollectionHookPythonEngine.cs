//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Migrant.Hooks;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Hooks
{
    public class RegisterCollectionHookPythonEngine<T> : PythonEngine where T: struct
    {
        public RegisterCollectionHookPythonEngine(IPeripheral peripheral, string script)
        {
            this.peripheral = peripheral;
            this.script = script;

            InnerInit();
            Hook = (address, value) =>
            {
                Scope.SetVariable("address", address);
                Scope.SetVariable("value", value);
                Execute(code, error =>
                {
                    this.peripheral.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public Action<long, T?> Hook { get; }

        public T? Value => (T?)Scope.GetVariable("value");

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", peripheral);

            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly IPeripheral peripheral;
    }
}

