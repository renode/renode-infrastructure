//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Wireless;
using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public class PacketInterceptionPythonEngine : PythonEngine
    {
        public PacketInterceptionPythonEngine(IRadio radio, string script = null, string filename = null)
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
                try
                {
                    source.Execute(Scope);
                }
                catch(Exception ex)
                {
                    throw new Exception("Hook execution failed: " + ex.Message);
                }
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
                source = Engine.CreateScriptSourceFromString(script);
            }
            else if(filename != null)
            {
                source = Engine.CreateScriptSourceFromFile(filename);
            }
        }

        [Transient]
        private ScriptSource source;
        private readonly string script;
        private readonly string filename;
        private readonly IRadio radio;
        private readonly Machine machine;
    }
}