//
// Copyright (c) 2010-2022 Antmicro
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
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB
{
    public class CommandsManager
    {
        public CommandsManager(Machine machine, IEnumerable<ICpuSupportingGdb> cpus, bool blockOnStep)
        {
            availableCommands = new HashSet<CommandDescriptor>();
            activeCommands = new HashSet<Command>();
            mnemonicList = new List<string>();
            Machine = machine;
            CanAttachCPU = true;
            BlockOnStep = blockOnStep;

            commandsCache = new Dictionary<string, Command>();
            ManagedCpus = new Dictionary<uint, ICpuSupportingGdb>();
            foreach(var cpu in cpus)
            {
                if(!TryAddManagedCPU(cpu))
                {
                    throw new RecoverableException($"Could not create GDB server for CPU: {cpu.GetName()}");
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
            InvalidateCompiledFeatures();
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
                mnemonicList.Add(interestingMethod.GetCustomAttribute<ExecuteAttribute>().Mnemonic);
            }
        }

        public bool TryGetCommand(Packet packet, out Command command)
        {
            var mnemonic = packet.Data.MatchMnemonicFromList(mnemonicList);
            if(mnemonic == null)
            {
                command = null;
                return false;
            }

            if(!commandsCache.TryGetValue(mnemonic, out command))
            {
                var commandDescriptor = availableCommands.FirstOrDefault(x => mnemonic == x.Mnemonic);
                command = commandDescriptor == null ? null: GetOrCreateCommand(commandDescriptor.Method.DeclaringType);
                commandsCache[mnemonic] = command;
            }
            return command != null;
        }

        public List<GDBFeatureDescriptor> GetCompiledFeatures()
        {
            // GDB gets one feature description file and uses it for all threads.
            // This method gathers features from all cores and unifies them.
            if(ManagedCpus.Count == 1)
            {
                return Cpu.GDBFeatures;
            }

            // if unifiedFeatures contains any feature, it means that features were compiled and cached
            if(unifiedFeatures.Any())
            {
                return unifiedFeatures;
            }

            var features = new Dictionary<string, List<GDBFeatureDescriptor>>();
            foreach(var cpu in ManagedCpus.Values)
            {
                foreach(var feature in cpu.GDBFeatures)
                {
                    if(!features.ContainsKey(feature.Name))
                    {
                        features.Add(feature.Name, new List<GDBFeatureDescriptor>());
                    }
                    features[feature.Name].Add(feature);
                }
            }

            foreach(var featureVariations in features.Values)
            {
                unifiedFeatures.Add(UnifyFeature(featureVariations));
            }

            return unifiedFeatures;
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
        public bool BlockOnStep { get; }

        private static GDBFeatureDescriptor UnifyFeature(List<GDBFeatureDescriptor> featureVariations)
        {
            if(featureVariations.Count == 1)
            {
                return featureVariations[0];
            }
            // This function unifies variations of a feature by taking the widest registers of matching name then adds taken register's type.
            var unifiedFeature = new GDBFeatureDescriptor(featureVariations.First().Name);
            var registers = new Dictionary<string, Tuple<GDBRegisterDescriptor, List<GDBCustomType>>>();
            var types = new Dictionary<string, Tuple<GDBCustomType, uint>>();
            foreach(var feature in featureVariations)
            {
                foreach(var register in feature.Registers)
                {
                    if(!registers.ContainsKey(register.Name))
                    {
                        registers.Add(register.Name, Tuple.Create(register, feature.Types));
                    }
                    else if(register.Size > registers[register.Name].Item1.Size)
                    {
                        registers[register.Name] = Tuple.Create(register, feature.Types);
                    }
                }
            }
            foreach(var pair in registers.Values.Where(elem => !String.IsNullOrEmpty(elem.Item1.Type)).OrderByDescending(elem => elem.Item1.Size))
            {
                unifiedFeature.Registers.Add(pair.Item1);
                AddFeatureTypes(ref types, pair.Item1.Type, pair.Item1.Size, pair.Item2);
            }
            foreach(var type in types.Values)
            {
                unifiedFeature.Types.Add(type.Item1);
            }
            return unifiedFeature;
        }

        private static void AddFeatureTypes(ref Dictionary<string, Tuple<GDBCustomType, uint>> types, string id, uint width, List<GDBCustomType> source)
        {
            if(types.TryGetValue(id, out var hit))
            {
                if(hit.Item2 != width)
                {
                    Logger.Log(LogLevel.Warning, string.Format("Found type \"{0}\" defined for multiple register widths ({1} and {2}). Reported target description may be erroneous.", id, hit.Item2, width));
                }
                return;
            }

            var index = source.FindIndex(element => element.Attributes["id"] == id);
            if(index == -1)
            {
                // If type description is not found, assume it's a built-in type.
                return;
            }

            var type = source[index];
            foreach(var field in type.Fields)
            {
                if(field.TryGetValue("type", out var fieldType) && !types.ContainsKey(fieldType))
                {
                    AddFeatureTypes(ref types, fieldType, width, source);
                }
            }
            types.Add(id, Tuple.Create(type, width));
        }

        private void InvalidateCompiledFeatures()
        {
            if(unifiedFeatures.Any())
            {
                unifiedFeatures.RemoveAll(_ => true);
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
        private readonly List<GDBFeatureDescriptor> unifiedFeatures = new List<GDBFeatureDescriptor>();

        private readonly Dictionary<string,Command> commandsCache;
        private uint selectedCpuNumber;
        private readonly List<string> mnemonicList;

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

