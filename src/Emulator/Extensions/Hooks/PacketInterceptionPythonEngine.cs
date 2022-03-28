//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class PacketInterceptionPythonEngine : PythonEngine
    {
        public PacketInterceptionPythonEngine(IRadio radio, string script = null, OptionalReadFilePath filename = null)
        {
            if((script == null && filename == null) || (script != null && filename != null))
            {
                throw new ConstructionException("Parameters `script` and `filename` cannot be both set or both unset.");
            }
            
            this.radio = radio;
            this.script = script;
            this.filename = filename;
            machine = radio.GetMachine();

            InnerInit();

            Hook = packet =>
            {
                Scope.SetVariable("packet", packet);
                Execute(code, error =>
                {
                    this.radio.Log(LogLevel.Error, "Python runtime error: {0}", error);
                });
            };
        }

        public Action<byte[]> Hook { get; private set; }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", radio);
            Scope.SetVariable("machine", machine);
            if(script != null)
            {
                var source = Engine.CreateScriptSourceFromString(script);
                code = Compile(source);
            }
            else if(filename != null)
            {
                var source = Engine.CreateScriptSourceFromFile(filename);
                code = Compile(source);
            }
        }

        [Transient]
        private CompiledCode code;
        private readonly string script;
        private readonly string filename;
        private readonly IRadio radio;
        private readonly Machine machine;
    }
}
