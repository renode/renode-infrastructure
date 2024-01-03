//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Microsoft.Scripting.Hosting;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface
{
    public static class MonitorExecutorExtensions
    {
        public static void ExecutePython(this IMachine machine, string script)
        {
            var engine = new ExecutorPythonEngine(machine, script);
            engine.Action();
        }

        public static void ExecutePythonFromFile(this IMachine machine, ReadFilePath filePath)
        {
            machine.ExecutePython(ReadScriptFromFile(filePath));
        }

        public static void ExecutePythonEvery(this IMachine machine, string name, int milliseconds, string script)
        {
            var engine = new ExecutorPythonEngine(machine, script);
            var clockEntry = new ClockEntry((ulong)milliseconds, 1000, engine.Action, machine, name);
            machine.ClockSource.AddClockEntry(clockEntry);

            events.Add(machine, name, engine.Action);
            machine.StateChanged += (m, s) => UnregisterEvent(m, name, s);
        }

        public static void ExecutePythonEveryFromFile(this IMachine machine, string name, int milliseconds, ReadFilePath filePath)
        {
            machine.ExecutePythonEvery(name, milliseconds, ReadScriptFromFile(filePath));
        }

        public static void StopPythonExecution(this IMachine machine, string name)
        {
            machine.ClockSource.TryRemoveClockEntry(events.WithdrawAction(machine, name));
            events.Remove(machine, name);
        }

        private static void UnregisterEvent(IMachine machine, String name, MachineStateChangedEventArgs state)
        {
            if(state.CurrentState == MachineStateChangedEventArgs.State.Disposed)
            {
                events.Remove(machine, name);
            }
        }

        private static string ReadScriptFromFile(ReadFilePath filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Error while opening Python script file '{filePath}': {e.Message}");
            }
        }

        private static readonly PeriodicEventsRegister events = new PeriodicEventsRegister();

        private sealed class ExecutorPythonEngine : PythonEngine
        {
            public ExecutorPythonEngine(IMachine machine, string script)
            {
                Scope.SetVariable(Machine.MachineKeyword, machine);
                Scope.SetVariable("self", machine);

                var source = Engine.CreateScriptSourceFromString(script);
                code = Compile(source);
                Action = () => Execute(code);
            }

            public Action Action { get; private set; }

            private CompiledCode code;
        }

        private sealed class PeriodicEventsRegister
        {
            public void Add(IMachine machine, string name, Action action)
            {
                lock(periodicEvents)
                {
                    if(HasEvent(machine, name))
                    {
                        throw new RecoverableException("Periodic event '{0}' already registered in this machine.".FormatWith(name));
                    }
                    periodicEvents.Add(Tuple.Create(machine, name), action);
                }
            }

            public Action WithdrawAction(IMachine machine, string name)
            {
                lock(periodicEvents)
                {
                    var action = periodicEvents[Tuple.Create(machine, name)];
                    Remove(machine, name);
                    return action;
                }
            }

            public void Remove(IMachine machine, String name)
            {
                lock(periodicEvents)
                {
                    periodicEvents.Remove(Tuple.Create(machine, name));
                }
            }

            public bool HasEvent(IMachine machine, String name)
            {
                lock(periodicEvents)
                {
                    return periodicEvents.ContainsKey(Tuple.Create(machine, name));
                }
            }

            private static readonly Dictionary<Tuple<IMachine, string>, Action> periodicEvents = new Dictionary<Tuple<IMachine, string>, Action>();
        }
    }
}
