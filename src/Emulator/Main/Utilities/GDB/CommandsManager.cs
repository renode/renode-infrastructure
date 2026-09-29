//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.GDB.Commands;

namespace Antmicro.Renode.Utilities.GDB
{
    public class CommandsManager
    {
        public CommandsManager(IMachine machine)
        {
            availableCommands = new HashSet<CommandDescriptor>();
            typesWithCommands = new HashSet<string>();
            activeCommands = new HashSet<Command>();
            mnemonicList = new List<string>();
            Machine = machine;
            CanAttachCPU = true;

            commandsCache = new Dictionary<string, Command>();
            ManagedCpus = new ManagedCpusDictionary(this);
        }

        public void AttachCPU(ICpuSupportingGdb cpu, int? pid)
        {
            if(!CanAttachCPU)
            {
                throw new RecoverableException("Cannot attach CPU because GDB is already connected.");
            }
            if(!ManagedCpus.DeclareProcess(cpu, pid))
            {
                throw new RecoverableException("CPU is already a part of a different process.");
            }
            if(!TryAddManagedCPU(cpu))
            {
                ManagedCpus.DeclareProcess(cpu, null);
                throw new RecoverableException("CPU already attached to this GDB server.");
            }
            InvalidateCompiledFeatures();
            if(selectedCpu is null)
            {
                selectedCpu = cpu;
                if(pid is int p)
                {
                    ManagedCpus.AttachedProcesses.Add(p);
                }
            }
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
            if(!typeof(Command).IsAssignableFrom(t) || t.IsAbstract)
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
                command = commandDescriptor == null ? null : GetOrCreateCommand(commandDescriptor.Method.DeclaringType);
                commandsCache[mnemonic] = command;
            }
            return command != null;
        }

        public List<GDBFeatureDescriptor> GetCompiledFeatures()
        {
            // GDB gets one feature description file and uses it for all threads.
            // This method gathers features from all cores and unifies them.

            // if unifiedFeatures contains any feature, it means that features were compiled and cached
            var pidKey = Process ?? 0; // 0 is safe to use in non-multiprocess cases as PID 0 is not valid in GDB
            if(unifiedFeatures.TryGetValue(pidKey, out var features))
            {
                return features;
            }

            features = new List<GDBFeatureDescriptor>();
            unifiedFeatures.Add(pidKey, features);

            if(ManagedCpus.Count() == 1)
            {
                features.AddRange(Cpu.GDBFeatures);
                return features;
            }

            var featuresDict = new Dictionary<string, List<GDBFeatureDescriptor>>();
            foreach(var cpu in ManagedCpus.Where(cpu => (ManagedCpus[cpu].ProcessId ?? pidKey) == pidKey))
            {
                foreach(var feature in cpu.GDBFeatures)
                {
                    if(!featuresDict.ContainsKey(feature.Name))
                    {
                        featuresDict.Add(feature.Name, new List<GDBFeatureDescriptor>());
                    }
                    featuresDict[feature.Name].Add(feature);
                }
            }

            foreach(var featureVariations in featuresDict.Values)
            {
                features.Add(UnifyFeature(featureVariations));
            }

            return features;
        }

        public GDBRegisterDescriptor[] GetCompiledRegisters(int registerNumber)
        {
            // The GDB backend relies a lot on getting a filtered list of
            // registers. Extracting them from all features every time is
            // costly, so this function caches the already filtered lists for
            // faster retrieval.

            var pidKey = Process ?? 0; // 0 is safe to use in non-multiprocess cases as PID 0 is not valid in GDB
            if(unifiedRegisters.TryGetValue((pidKey, registerNumber), out var registers))
            {
                return registers;
            }

            registers = GetCompiledFeatures().SelectMany(f => f.Registers)
                .Where(r => r.Number == registerNumber).ToArray();
            unifiedRegisters.Add((pidKey, registerNumber), registers);

            return registers;
        }

        public void AddBreakpoint(ulong address, BreakpointType type)
        {
            GetOrCreateCommand<BreakpointCommand>().InsertBreakpoint(type, address);
        }

        public void AddWatchpoint(WatchpointDescriptor descriptor, int counter = 1)
        {
            GetOrCreateCommand<BreakpointCommand>().InsertBreakpoint(BreakpointType.AccessWatchpoint, descriptor.Address, descriptor, counter);
        }

        public void RemoveAllBreakpoints()
        {
            var breakpointCommand = GetOrCreateCommand<BreakpointCommand>();
            breakpointCommand.RemoveAllBreakpoints();
            breakpointCommand.RemoveAllWatchpoints();
        }

        public void LoadLatestSnapshotBefore(Action<CommandsManager> onLoadAction = null)
        {
            var currentTimeStamp = EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime;
            LoadLatestSnapshotBefore(currentTimeStamp, onLoadAction);
        }

        public void LoadLatestSnapshotBefore(TimeInterval beforeTimeStamp, Action<CommandsManager> onLoadAction = null)
        {
            if(Machine.GdbStubs.Count > 1)
            {
                throw new RecoverableException("More than one GDB connection opened. This is currently not supported.");
            }
            var port = Machine.GdbStubs.Values.First().Terminal.Port.Value;
            var machineName = Machine.ToString();

            var snapshotPath = EmulationManager.Instance.CurrentEmulation.SnapshotTracker.GetSnapshotForGdbBeforeTimeStamp(beforeTimeStamp);
            EmulationManager.Instance.Load(snapshotPath, preserveState: true);

            if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(machineName, out var newMachine))
            {
                throw new RecoverableException("Machine was not found in the snapshot.");
            }
            var newCommandsManager = newMachine.GdbStubs[port].CommandsManager;
            onLoadAction?.Invoke(newCommandsManager);
        }

        public IMachine Machine { get; }

        public ManagedCpusDictionary ManagedCpus { get; }

        public bool CanAttachCPU { get; set; }

        public bool MultiprocessEnabled { get; set; }

        public ICpuSupportingGdb Cpu => selectedCpu;

        public int? Process => ManagedCpus[Cpu].ProcessId;

        public ISet<Tuple<ulong, BreakpointType>> Breakpoints => GetOrCreateCommand<BreakpointCommand>().Breakpoints;

        public IDictionary<WatchpointDescriptor, int> Watchpoints => GetOrCreateCommand<BreakpointCommand>().Watchpoints;

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
            unifiedFeatures.Clear();
            unifiedRegisters.Clear();
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

        private T GetOrCreateCommand<T>() where T : Command
        {
            return GetOrCreateCommand(typeof(T)) as T;
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

        private ICpuSupportingGdb selectedCpu;

        [Constructor]
        private readonly HashSet<CommandDescriptor> availableCommands;
        private readonly HashSet<string> typesWithCommands;
        private readonly HashSet<Command> activeCommands;
        // Key: Process ID
        private readonly Dictionary<int, List<GDBFeatureDescriptor>> unifiedFeatures = new();
        // Key: (Process ID, Register number)
        private readonly Dictionary<(int, int), GDBRegisterDescriptor[]> unifiedRegisters = new();

        private readonly Dictionary<string,Command> commandsCache;
        [Constructor]
        private readonly List<string> mnemonicList;

        public class ManagedCpusDictionary : IEnumerable<ICpuSupportingGdb>
        {
            public ManagedCpusDictionary(CommandsManager manager)
            {
                this.manager = manager;
            }

            /// <remarks> There is no check whatsoever to prevent inserting the same CPU twice here, with different unique ID </remarks>
            public int Add(ICpuSupportingGdb cpu)
            {
                // Thread id "0" might be interpreted as "any" thread by GDB, so start from 1
                var ctr = cpusToIds.Count + 1;
                cpusToIds.Add(cpu, ctr);
                idsToCpus.Add(ctr, cpu);
                return ctr;
            }

            public bool DeclareProcess(ICpuSupportingGdb cpu, int? pid)
            {
                if(cpusToPids.ContainsKey(cpu) && pid is not null)
                {
                    return false;
                }

                if(pid is int p)
                {
                    cpusToPids[cpu] = p;
                }
                else
                {
                    cpusToPids.Remove(cpu);
                }
                return true;
            }

            public IEnumerator<ICpuSupportingGdb> GetEnumerator()
            {
                return cpusToIds.Keys.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public PacketThreadId this[ICpuSupportingGdb cpu]
            {
                get
                {
                    int? processId = manager.MultiprocessEnabled ? cpusToPids[cpu] : null;
                    return new PacketThreadId(processId, cpusToIds[cpu]);
                }
            }

            public ICpuSupportingGdb this[PacketThreadId id]
            {
                // There are two special cases here:
                // -1 means "all" - not supported right now
                // 0 means an arbitrary process or thread - so take the first one available
                get => id.ThreadId switch
                {
                    PacketThreadId.All => throw new NotSupportedException("Selecting \"all\" CPUs is not supported"),
                    PacketThreadId.Any => this.idsToCpus.OrderBy(kv => kv.Key).First().Value,
                    _ => idsToCpus[id.ThreadId],
                };
            }

            public IEnumerable<PacketThreadId> All => cpusToIds.Keys.Select(cpu => this[cpu]);

            public bool MultiprocessExtensionRequested => cpusToPids.Count > 0;

            public HashSet<int> AttachedProcesses => attachedProcesses;

            private readonly CommandsManager manager;
            private readonly Dictionary<int, ICpuSupportingGdb> idsToCpus = new Dictionary<int, ICpuSupportingGdb>();
            private readonly Dictionary<ICpuSupportingGdb, int> cpusToIds = new Dictionary<ICpuSupportingGdb, int>();
            private readonly Dictionary<ICpuSupportingGdb, int> cpusToPids = new Dictionary<ICpuSupportingGdb, int>();
            private readonly HashSet<int> attachedProcesses = new HashSet<int>();
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
