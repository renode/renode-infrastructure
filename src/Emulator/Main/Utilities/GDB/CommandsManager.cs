//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities.GDB
{
    public class CommandsManager
    {
        public CommandsManager(IMachine machine, IEnumerable<ICpuSupportingGdb> cpus)
        {
            availableCommands = new HashSet<CommandDescriptor>();
            typesWithCommands = new HashSet<string>();
            activeCommands = new HashSet<Command>();
            mnemonicList = new List<string>();
            Machine = machine;
            CanAttachCPU = true;

            commandsCache = new Dictionary<string, Command>();
            ManagedCpus = new ManagedCpusDictionary();
            foreach(var cpu in cpus)
            {
                if(!TryAddManagedCPU(cpu))
                {
                    throw new RecoverableException($"Could not create GDB server for CPU: {cpu.GetName()}");
                }
            }
            selectedCpu = ManagedCpus[PacketThreadId.Any];
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
            return ManagedCpus.Contains(cpu);
        }

        public void SelectCpuForDebugging(ICpuSupportingGdb cpu)
        {
            if(!ManagedCpus.Contains(cpu))
            {
                throw new RecoverableException($"Tried to set invalid CPU: {cpu.GetName()} for debugging, which isn't connected to the current {nameof(GdbStub)}");
            }
            selectedCpu = cpu;
        }

        public void Register(Type t)
        {
            if(t == typeof(Command) || !typeof(Command).IsAssignableFrom(t))
            {
                return;
            }

            typesWithCommands.Add(t.AssemblyQualifiedName);
            AddCommandsFromType(t);
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

            // if unifiedFeatures contains any feature, it means that features were compiled and cached
            if(unifiedFeatures.Any())
            {
                return unifiedFeatures;
            }

            if(ManagedCpus.Count() == 1)
            {
                unifiedFeatures.AddRange(Cpu.GDBFeatures);
                return unifiedFeatures;
            }

            var features = new Dictionary<string, List<GDBFeatureDescriptor>>();
            foreach(var cpu in ManagedCpus)
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

        public GDBRegisterDescriptor[] GetCompiledRegisters(int registerNumber)
        {
            // The GDB backend relies a lot on getting a filtered list of
            // registers. Extracting them from all features every time is
            // costly, so this function caches the already filtered lists for
            // faster retrieval.

            if(unifiedRegisters.TryGetValue(registerNumber, out var registers))
            {
                return registers;
            }

            registers = GetCompiledFeatures().SelectMany(f => f.Registers)
                .Where(r => r.Number == registerNumber).ToArray();
            unifiedRegisters.Add(registerNumber, registers);

            return registers;
        }

        public IMachine Machine { get; }
        public ManagedCpusDictionary ManagedCpus { get; }
        public bool CanAttachCPU { get; set; }
        public ICpuSupportingGdb Cpu => selectedCpu;

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
            ManagedCpus.Add(cpu);
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

        [PostDeserialization]
        private void ExtractCommandsFromTypeNames()
        {
            foreach(var typeName in typesWithCommands)
            {
                var t = Type.GetType(typeName);
                AddCommandsFromType(t);
            }
        }

        private void AddCommandsFromType(Type t)
        {
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

        [Constructor]
        private readonly HashSet<CommandDescriptor> availableCommands;
        private readonly HashSet<string> typesWithCommands;
        private readonly HashSet<Command> activeCommands;
        private readonly List<GDBFeatureDescriptor> unifiedFeatures = new List<GDBFeatureDescriptor>();
        private readonly Dictionary<int, GDBRegisterDescriptor[]> unifiedRegisters = new Dictionary<int, GDBRegisterDescriptor[]>();

        private readonly Dictionary<string,Command> commandsCache;
        private ICpuSupportingGdb selectedCpu;
        [Constructor]
        private readonly List<string> mnemonicList;

        public class ManagedCpusDictionary : IEnumerable<ICpuSupportingGdb>
        {
            /// <remarks> There is no check whatsoever to prevent inserting the same CPU twice here, with different unique ID </remarks>
            public uint Add(ICpuSupportingGdb cpu)
            {
                // Thread id "0" might be interpreted as "any" thread by GDB, so start from 1
                uint ctr = (uint)cpusToIds.Count + 1;
                cpusToIds.Add(cpu, ctr);
                idsToCpus.Add(ctr, cpu);
                return ctr;
            }

            public IEnumerator<ICpuSupportingGdb> GetEnumerator()
            {
                return cpusToIds.Keys.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public ICpuSupportingGdb this[int idx]
            {
                get
                {
                    // There are two special cases here:
                    // -1 means "all" - not supported right now
                    // 0 means an arbitrary process or thread - so take the first one available
                    switch(idx)
                    {
                        case PacketThreadId.All:
                            throw new NotSupportedException("Selecting \"all\" CPUs is not supported");
                        case PacketThreadId.Any:
                            return idsToCpus.OrderBy(kv => kv.Key).First().Value;
                        default:
                            return idsToCpus[(uint)idx];
                    }
                }
            }

            public ICpuSupportingGdb this[uint idx] => this[(int)idx];
            public uint this[ICpuSupportingGdb cpu] => cpusToIds[cpu];

            public IEnumerable<uint> GdbCpuIds => idsToCpus.Keys;

            private readonly Dictionary<uint, ICpuSupportingGdb> idsToCpus = new Dictionary<uint, ICpuSupportingGdb>();
            private readonly Dictionary<ICpuSupportingGdb, uint> cpusToIds = new Dictionary<ICpuSupportingGdb, uint>();
        }

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

