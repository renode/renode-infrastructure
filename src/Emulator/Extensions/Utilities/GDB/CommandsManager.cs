//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB
{
    internal class CommandsManager
    {
        public CommandsManager(ICpuSupportingGdb cpu)
        {
            availableCommands = new HashSet<CommandDescriptor>();
            activeCommands = new HashSet<Command>();

            Machine machine;
            EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(cpu, out machine);
            Cpu = cpu;
            Machine = machine;
        }

        public void Register(Type t)
        {
            if(t == typeof(Command) || !typeof(Command).IsAssignableFrom(t))
            {
                return;
            }

            var interestingMethods = Command.GetExecutingMethods(t);
            if(interestingMethods.Length == 0)
            {
                Logger.Log(LogLevel.Error, string.Format("No proper constructor or executing methods found in type {0}", t.Name));
                return;
            }

            foreach(var interestingMethod in interestingMethods)
            {
                availableCommands.Add(new CommandDescriptor(interestingMethod));
            }
        }

        public bool TryGetCommand(Packet packet, out Command command)
        {
            var commandDescriptor = availableCommands.SingleOrDefault(x => packet.Data.DataAsString.StartsWith(x.Mnemonic, StringComparison.Ordinal));
            if(commandDescriptor == null)
            {
                command = null;
                return false;
            }

            command = GetOrCreateCommand(commandDescriptor.Method.DeclaringType);
            return true;
        }

        public Machine Machine { get; private set; }
        public ICpuSupportingGdb Cpu { get; private set; }
        public bool ShouldAutoStart { get; set; }

        private Command GetOrCreateCommand(Type t)
        {
            var result = activeCommands.SingleOrDefault(x => x.GetType() == t);
            if(result == null)
            {
                result = (Command)Activator.CreateInstance(t, new[] { this });
                activeCommands.Add(result);
            }

            return result;
        }

        private readonly HashSet<CommandDescriptor> availableCommands;
        private readonly HashSet<Command> activeCommands;

        private class CommandDescriptor
        {
            public CommandDescriptor(MethodInfo method)
            {
                Method = method;
                Mnemonic = method.GetCustomAttribute<ExecuteAttribute>().Mnemonic;
            }

            public string Mnemonic { get; private set; }
            public MethodInfo Method { get; private set; }
        }
    }
}

