//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Hooks
{
    public class BusPeripheralsHooksPythonEngine : PythonEngine
    {
        public BusPeripheralsHooksPythonEngine(IBusController sysbus, IBusPeripheral peripheral, string readScript = null, string writeScript = null)
        {
            Peripheral = peripheral;
            Sysbus = sysbus;
            ReadScript = readScript;
            WriteScript = writeScript;

            InnerInit();

            if (WriteScript != null)
            {
                WriteHook = new Func<ulong, long, ulong>((valueToWrite, offset) =>
                    {
                        Scope.SetVariable("value", valueToWrite);
                        Scope.SetVariable("offset", offset);
                        Execute(writeCode, error =>
                        {
                            Sysbus.Log(LogLevel.Error, "Python runtime error: {0}", error);
                        });
                        return (ulong)Scope.GetVariable("value");
                    });
            }

            if (ReadScript != null)
            {
                ReadHook = new Func<ulong, long, ulong>((readValue, offset) =>
                    {
                        Scope.SetVariable("value", readValue);
                        Scope.SetVariable("offset", offset);
                        Execute(readCode, error =>
                        {
                            Sysbus.Log(LogLevel.Error, "Python runtime error: {0}", error);
                        });
                        return (ulong)Scope.GetVariable("value");
                    });
            }
        }

        [PostDeserialization]
        private void InnerInit()
        {
            Scope.SetVariable("self", Peripheral);
            Scope.SetVariable("sysbus", Sysbus);
            Scope.SetVariable(Machine.MachineKeyword, Sysbus.Machine);

            if(ReadScript != null)
            {
                var source = Engine.CreateScriptSourceFromString(ReadScript);
                readCode = Compile(source);
            }
            if(WriteScript != null)
            {
                var source = Engine.CreateScriptSourceFromString(WriteScript);
                writeCode = Compile(source);
            }
        }

        public Func<ulong, long, ulong> WriteHook { get; private set; }
        public Func<ulong, long, ulong> ReadHook { get; private set; }

        [Transient]
        private CompiledCode readCode;
        [Transient]
        private CompiledCode writeCode;

        private readonly string ReadScript;
        private readonly string WriteScript;
        private readonly IBusPeripheral Peripheral;
        private readonly IBusController Sysbus;
    }
}

