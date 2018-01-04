//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Hooks
{
    public class BusPeripheralsHooksPythonEngine : PythonEngine
    {
        public BusPeripheralsHooksPythonEngine(SystemBus sysbus, IBusPeripheral peripheral, string readScript = null, string writeScript = null)
        {       
            Peripheral = peripheral;
            Sysbus = sysbus;
            ReadScript = readScript;
            WriteScript = writeScript;

            InnerInit();

            if (WriteScript != null)
            {
                WriteHook = new Func<uint, long, uint>((valueToWrite, offset) =>
                    {
                        Scope.SetVariable("value", valueToWrite);
                        Scope.SetVariable("offset", offset);
                        WriteSource.Value.Execute(Scope);
                        return (uint)Scope.GetVariable("value");
                    });
            }

            if (ReadScript != null)
            {
                ReadHook = new Func<uint, long, uint>((readValue, offset) =>
                    {
                        Scope.SetVariable("value", readValue);
                        Scope.SetVariable("offset", offset);
                        ReadSource.Value.Execute(Scope);
                        return (uint)Scope.GetVariable("value");
                    });
            }
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", Peripheral);
            Scope.SetVariable("sysbus", Sysbus);
            Scope.SetVariable(Machine.MachineKeyword, Sysbus.Machine);

            ReadSource = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(ReadScript));
            WriteSource = new Lazy<ScriptSource>(() => Engine.CreateScriptSourceFromString(WriteScript));
        }

        public Func<uint, long, uint> WriteHook { get; private set; }
        public Func<uint, long, uint> ReadHook { get; private set; }

        private readonly string ReadScript;
        private readonly string WriteScript;
        private readonly IBusPeripheral Peripheral;
        private readonly SystemBus Sysbus;

        [Transient]
        private Lazy<ScriptSource> ReadSource;
        [Transient]
        private Lazy<ScriptSource> WriteSource;
    }
}

