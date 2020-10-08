//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB
{
    public class CommandsManager
    {
        public CommandsManager(Machine machine, IEnumerable<ICpuSupportingGdb> cpus)
        {
            availableCommands = new HashSet<CommandDescriptor>();
            activeCommands = new HashSet<Command>();
            Machine = machine;
            CanAttachCPU = true;

            ManagedCpus = new Dictionary<uint, ICpuSupportingGdb>();
            foreach(var cpu in cpus)
            {
                if(!TryAddManagedCPU(cpu))
                {
                    throw new RecoverableException(string.Format("Could not create GDB server with CPU: {0}", cpu.Name));
                }
            }
            selectedCpuNumber = ManagedCpus.OrderBy(x => x.Key).First().Key;
        }

        public void AttachCPU(ICpuSupportingGdb cpu)
        {
            if(!CanAttachCPU)
            {
                throw new RecoverableException("Cannot attach CPU because GDB is already connected.");
            }
            if(!TryAddManagedCPU(cpu))
            {
                throw new RecoverableException("CPU already attached to this GDB server.");
            }
        }

        public bool IsCPUAttached(ICpuSupportingGdb cpu)
        {
            return ManagedCpus.ContainsValue(cpu);
        }

        public void SelectCpuForDebugging(uint cpuNumber)
        {
            if(cpuNumber == 0)
            {
                // the documentation states that `0` indicates an arbitrary process or thread, so we will take the first one available
                cpuNumber = ManagedCpus.OrderBy(x => x.Key).First().Key;
            }
            else if(!ManagedCpus.ContainsKey(cpuNumber))
            {
                Logger.Log(LogLevel.Error, "Tried to set invalid CPU number: {0}", cpuNumber);
                return;
            }
            selectedCpuNumber = cpuNumber;
        }

        public void Register(Type t)
        {
            if((!Machine.SystemBus.IsMultiCore && typeof(IMultithreadCommand).IsAssignableFrom(t)) || t == typeof(Command) || !typeof(Command).IsAssignableFrom(t))
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
            var commandDescriptors = availableCommands.Where(x => packet.Data.DataAsString.StartsWith(x.Mnemonic, StringComparison.Ordinal));
            if(!commandDescriptors.Any())
            {
                command = null;
                return false;
            }

            var descriptor = commandDescriptors.OrderByDescending(x => x.Mnemonic.Length).FirstOrDefault();
            command = GetOrCreateCommand(descriptor.Method.DeclaringType);
            return true;
        }

        public Machine Machine { get; private set; }
        public Dictionary<uint, ICpuSupportingGdb> ManagedCpus { get; set; }
        public bool ShouldAutoStart { get; set; }
        public bool CanAttachCPU { get; set; }
        public ICpuSupportingGdb Cpu
        {
            get
            {
                return ManagedCpus[selectedCpuNumber];
            }
        }

        private bool TryAddManagedCPU(ICpuSupportingGdb cpu)
        {
            if(IsCPUAttached(cpu))
            {
                return false;
            }
            ManagedCpus.Add(cpu.Id + 1, cpu);
            return true;
        }

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

        private uint selectedCpuNumber;

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

