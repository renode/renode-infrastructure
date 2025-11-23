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
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class RegisterCollectionHookPythonEngine<T, R> : PythonEngine
        where T : struct
        where R : IRegisterCollection
    {
        public RegisterCollectionHookPythonEngine(IProvidesRegisterCollection<R> registerCollectionProvider, string script)
        {
            this.registerCollectionProvider = registerCollectionProvider;
            this.script = script;

            InnerInit();
            Hook = (address, value) =>
            {
                Scope.SetVariable("address", address);
                Scope.SetVariable("value", value);
                Execute(code, error =>
                {
                    Logger.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public Action<long, T?> Hook { get; }

        public T? Value => (T?)Scope.GetVariable("value");

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", registerCollectionProvider);

            var source = Engine.CreateScriptSourceFromString(script);
            code = Compile(source);
        }

        [Transient]
        private CompiledCode code;

        private readonly string script;
        private readonly IProvidesRegisterCollection<R> registerCollectionProvider;
    }
}