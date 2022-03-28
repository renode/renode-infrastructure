//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
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
        public static void ExecutePythonEvery(this Machine machine, string name, int milliseconds, string script)
        {
            var engine = new ExecutorPythonEngine(machine, script);
            var clockEntry = new ClockEntry((ulong)milliseconds, 1000, engine.Action, machine, name);
            machine.ClockSource.AddClockEntry(clockEntry);

            events.Add(machine, name, engine.Action);
            machine.StateChanged += (m, s) => UnregisterEvent(m, name, s);
        }

        public static void StopPythonExecution(this Machine machine, string name)
        {
            machine.ClockSource.RemoveClockEntry(events.WithdrawAction(machine, name));
            events.Remove(machine, name);
        }

        private static void UnregisterEvent(Machine machine, String name, MachineStateChangedEventArgs state)
        {
            if(state.CurrentState == MachineStateChangedEventArgs.State.Disposed)
            {
                events.Remove(machine, name);
            }
        }

        private static readonly PeriodicEventsRegister events = new PeriodicEventsRegister();

        private sealed class ExecutorPythonEngine : PythonEngine
        {
            public ExecutorPythonEngine(Machine machine, string script)
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
            public void Add(Machine machine, string name, Action action)
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

            public Action WithdrawAction(Machine machine, string name)
            {
                lock(periodicEvents)
                {
                    var action = periodicEvents[Tuple.Create(machine, name)];
                    Remove(machine, name);
                    return action;
                }
            }

            public void Remove(Machine machine, String name)
            {
                lock(periodicEvents)
                {
                    periodicEvents.Remove(Tuple.Create(machine, name));
                }
            }

            public bool HasEvent(Machine machine, String name)
            {
                lock(periodicEvents)
                {
                    return periodicEvents.ContainsKey(Tuple.Create(machine, name));
                }
            }

            private static readonly Dictionary<Tuple<Machine, string>, Action> periodicEvents = new Dictionary<Tuple<Machine, string>, Action>();
        }
    }
}
