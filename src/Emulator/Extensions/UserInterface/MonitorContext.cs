//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Sockets;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface
{
    public record MonitorContext
    {
        public static IEnumerable<MethodInfo> GetAvailableExtensions(Type type) =>
            TypeManager.Instance.GetExtensionMethods(type).Where(y => y.IsExtensionCallable()).OrderBy(y => y.Name);

        public MonitorContext(string currentDirectory, Token oldOrigin = null)
        {
            InitializeBindings();

            currentMachineOverride = new ThreadLocal<IMachine>();
            isMachineOverriden = new ThreadLocal<bool>();

            VariableCollections = new Dictionary<Monitor.VariableType, Dictionary<string, Token>>
            {
                { Monitor.VariableType.Variable, Variables },
                { Monitor.VariableType.Macro, Macros },
                { Monitor.VariableType.Alias, Aliases },
            };

            Variables[CurrentDirectoryVariable] = new PathToken("@" + currentDirectory);
            if(oldOrigin != null)
            {
                Variables[OriginVariable] = oldOrigin;
            }
        }

        public void SetVariable(string name, Token value, Monitor.VariableType collection)
        {
            VariableCollections[collection][name] = value;
        }

        public void SetVariable(string name, Token value)
        {
            Variables[name] = value;
        }

        public void SetMacro(string name, Token value)
        {
            Macros[name] = value;
        }

        public void SetAlias(string name, Token value)
        {
            Aliases[name] = value;
        }

        public void Bind(string name, Func<object> objectServer)
        {
            ObjectDelegateMappings[name] = objectServer;
        }

        public void BindStatic(string name, Func<object> objectServer)
        {
            StaticObjectDelegateMappings[name] = objectServer;
        }

        public bool SetPeripheralMacro(IPeripheral peripheral, string macroName, string contents, IMachine machine = null)
        {
            string variablePrefix;
            if(peripheral == null)
            {
                variablePrefix = "";
            }
            else if(machine.TryGetLocalName(peripheral, out variablePrefix))
            {
                variablePrefix += ".";
            }
            else
            {
                return false;
            }

            var variableName = GetVariableName($"{variablePrefix}{macroName}");
            SetMacro(variableName, new StringToken(contents));
            return true;
        }

        public IDisposable PushDirectory(string directory)
        {
            MonitorPath.PushDirectory(directory);
            return DisposableWrapper.New(() => MonitorPath.PopDirectory());
        }

        public string GetVariableName(string variableName)
        {
            var elements = variableName.Split('.', 2);

            if(elements.Length == 1 || (!elements[0].Equals("global") && !EmulationManager.Instance.CurrentEmulation.Names.Any(x => NormalizeMachineName(x) == elements[0])))
            {
                if(CurrentMachine != null)
                {
                    variableName = $"{MachineNameNormalized}.{variableName}";
                }
                else
                {
                    variableName = $"{GlobalVariablePrefix}.{variableName}";
                }
            }
            return variableName;
        }

        public object GetVariable(string name)
        {
            return TryExpandVariable(new VariableToken(name), Variables, out var value) ? value.GetObjectValue() : null;
        }

        public bool TryExpandVariable(VariableToken token, Dictionary<string, Token> collection, out Token expandedVariable)
        {
            expandedVariable = null;
            var varName = token.Value;
            string newName;
            if(collection.TryGetValue(varName, out expandedVariable))
            {
                return true;
            }
            if(CurrentMachine != null)
            {
                newName = $"{MachineNameNormalized}.{varName}";
                if(collection.TryGetValue(newName, out expandedVariable))
                {
                    return true;
                }
            }
            newName = $"{MonitorContext.GlobalVariablePrefix}.{varName}";
            if(collection.TryGetValue(newName, out expandedVariable))
            {
                return true;
            }
            return false;
        }

        public Token ExpandVariable(VariableToken token, Dictionary<string, Token> collection)
        {
            Token result;
            if(!TryExpandVariable(token, collection, out result))
            {
                throw new RecoverableException($"No such variable: ${token.Value}");
            }
            return result;
        }

        public IList<Token> ExpandVariables(IEnumerable<Token> tokens)
        {
            return tokens.Select(x => x is VariableToken ? ExpandVariable(x as VariableToken, Variables) ?? x : x).ToList(); // ?? to prevent null tokens
        }

        public bool IsNameAvailable(string name)
        {
            var names = GetAvailableNames();
            return names.Contains(name) || Usings.Any(use => names.Contains(use + name));
        }

        public IEnumerable<string> GetAvailableNames()
        {
            if(CurrentMachine != null)
            {
                return CurrentMachine.GetAllNames().Union(EmulationManager.Instance.CurrentEmulation.ExternalsManager.GetNames().Union(StaticObjectDelegateMappings.Keys.Union(ObjectDelegateMappings.Keys)));
            }
            return EmulationManager.Instance.CurrentEmulation.ExternalsManager.GetNames().Union(StaticObjectDelegateMappings.Keys);
        }

        public IEnumerable<string> GetAllAvailableNames()
        {
            var baseNames = GetAvailableNames().ToList();
            var result = new List<string>(baseNames);
            foreach(var use in Usings)
            {
                var localUse = use;
                result.AddRange(baseNames.Where(x => x.StartsWith(localUse, StringComparison.Ordinal) && x.Length > localUse.Length).Select(x => x.Substring(localUse.Length)));
            }
            return result;
        }

        public IEnumerable<string> FindMatchingVariables(string lastElement)
        {
            var varName = lastElement.Substring(1);
            var options = Variables.Keys.Concat(Macros.Keys).Where(x => x.StartsWith(varName, StringComparison.Ordinal)).ToList();
            var machinePrefix = CurrentMachine == null ? GlobalVariablePrefix : MachineName;
            options.AddRange(Variables.Keys.Concat(Macros.Keys).Where(x => x.StartsWith($"{machinePrefix}.{varName}", StringComparison.Ordinal)).Select(x => x.Substring(machinePrefix.Length)));
            return options;
        }

        public IEmulationElement GetExternalInterfaceOrNull(string name)
        {
            IEmulationElement external;
            EmulationManager.Instance.CurrentEmulation.ExternalsManager.TryGetByName(name, out external);
            return external;
        }

        public IEnumerable<FieldInfo> GetAvailableFields(Type objectType)
        {
            var fields = new List<FieldInfo>();
            var type = objectType;
            while(type != null && type != typeof(object))
            {
                fields.AddRange(type.GetFields(CurrentBindingFlags)
                                .Where(x => x.IsCallable())
                );
                type = type.BaseType;
            }
            return fields.DistinctBy(x => x.ToString()); //Look @ GetAvailableMethods for explanation.
        }

        public IEnumerable<MethodInfo> GetAvailableMethods(Type objectType)
        {
            var methods = new List<MethodInfo>();
            var type = objectType;
            while(type != null && type != typeof(object))
            {
                methods.AddRange(
                    type.GetMethods(CurrentBindingFlags)
                    .Where(x =>
                        !(x.IsSpecialName
                            && (x.Name.StartsWith("get_", StringComparison.Ordinal) || x.Name.StartsWith("set_", StringComparison.Ordinal)
                                || x.Name.StartsWith("add_", StringComparison.Ordinal) || x.Name.StartsWith("remove_", StringComparison.Ordinal)))
                        && !x.IsAbstract
                        && !x.IsConstructor
                        && !x.IsGenericMethod
                        && x.IsRIDSupported()
                        && x.IsCallable()
                    )
                );
                type = type.BaseType;
            }
            var enumerableType = objectType.GetEnumerableElementType();
            if(enumerableType != null)
            {
                methods.Add(selectInfo.MakeGenericMethod(new[] { enumerableType, typeof(object) }));
                methods.Add(typeof(List<>).MakeGenericType(new[] { enumerableType }).GetMethod(nameof(List<object>.ForEach)));
            }
            return methods.DistinctBy(x => x.ToString()); //This acutally gives us a full, easily comparable signature. Brilliant solution to avoid duplicates from overloaded methods.
        }

        public object GetDevice(string name)
        {
            var staticBound = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            if(CurrentMachine != null || staticBound != null || iface != null)
            {
                var boundObject = staticBound ?? FromMapping(name) ?? iface;
                if(boundObject != null)
                {
                    return boundObject;
                }

                IPeripheral device;
                string longestMatch;
                if(TryFindPeripheralByName(name, out device, out longestMatch, out _))
                {
                    return device;
                }
            }
            return null;
        }

        public object IdentifyDevice(string name)
        {
            var device = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            device = device ?? FromMapping(name) ?? iface ?? CurrentMachine[name];
            return device;
        }

        public bool TryFindPeripheralTypeByName(string name, out Type type, out string longestMatch, out string actualName)
        {
            type = null;
            if(TryFindPeripheralByName(name, out var peripheral, out longestMatch, out actualName))
            {
                type = peripheral.GetType();
                return true;
            }
            return false;
        }

        public bool TryFindPeripheralByName(string name, out IPeripheral peripheral, out string longestMatch, out string actualName)
        {
            actualName = name;
            if(CurrentMachine == null)
            {
                longestMatch = string.Empty;
                peripheral = null;
                return false;
            }

            var longestPrefix = string.Empty;
            var ret = CurrentMachine.TryGetByName(name, out peripheral, out var longestMatching);
            if(!ret)
            {
                foreach(var prefix in Usings)
                {
                    ret = CurrentMachine.TryGetByName(prefix + name, out peripheral, out var currentMatch);
                    if(longestMatching.Split('.').Length < currentMatch.Split('.').Length - prefix.Split('.').Length)
                    {
                        longestMatching = currentMatch;
                        longestPrefix = prefix;
                    }
                    if(ret)
                    {
                        actualName = prefix + name;
                        break;
                    }
                }
            }
            longestMatch = longestPrefix + longestMatching;
            return ret;
        }

        public object FromStaticMapping(string name) =>
            StaticObjectDelegateMappings.GetOrDefault(name)?.Invoke();

        public object FromMapping(string name) =>
            ObjectDelegateMappings.GetOrDefault(name)?.Invoke();

        public void PrintMonitorInfo(string name, MonitorInfo info, ICommandInteraction writer, string lookup = null)
        {
            if(info == null)
            {
                return;
            }
            if(info.Methods != null && info.Methods.Any(x => lookup == null || x.Name == lookup))
            {
                writer.WriteLine("\nThe following methods are available:");

                foreach(var method in info.Methods.Where(x => lookup == null || x.Name == lookup))
                {
                    writer.Write(" - ");
                    writer.Write(Misc.TypePrettyName(method.ReturnType), ConsoleColor.Green);
                    writer.Write($" {method.Name} (");

                    IEnumerable<ParameterInfo> parameters;

                    if(method.IsExtension())
                    {
                        parameters = method.GetParameters().Skip(1);
                    }
                    else
                    {
                        parameters = method.GetParameters();
                    }
                    parameters = parameters.Where(x => !Attribute.IsDefined(x, typeof(AutoParameterAttribute)));

                    var lastParameter = parameters.LastOrDefault();
                    foreach(var param in parameters.Where(x => !x.IsRetval))
                    {
                        if(param.IsOut)
                        {
                            writer.Write("out ", ConsoleColor.Yellow);
                        }
                        if(param.IsDefined(typeof(ParamArrayAttribute)))
                        {
                            writer.Write("params ", ConsoleColor.Yellow);
                        }
                        writer.Write(Misc.TypePrettyName(param.ParameterType), ConsoleColor.Green);
                        writer.Write($" {param.Name}");

                        if(param.IsOptional)
                        {
                            writer.Write(" = ");
                            if(param.DefaultValue == null)
                            {
                                writer.Write("null", ConsoleColor.DarkRed);
                            }
                            else
                            {
                                if(param.ParameterType.Name == "String")
                                {
                                    writer.Write("\"", ConsoleColor.DarkRed);
                                }
                                writer.Write(param.DefaultValue.ToString(), ConsoleColor.DarkRed);
                                if(param.ParameterType.Name == "String")
                                {
                                    writer.Write("\"", ConsoleColor.DarkRed);
                                }
                            }
                        }
                        if(lastParameter != param)
                        {
                            writer.Write(", ");
                        }
                    }
                    writer.WriteLine(")");
                }
                writer.WriteLine(string.Format("\n\rUsage:\n\r {0} MethodName param1 param2 ...\n\r", name));
            }

            if(info.Properties != null && info.Properties.Any(x => lookup == null || x.Name == lookup))
            {
                writer.WriteLine("\nThe following properties are available:");

                foreach(var property in info.Properties.Where(x => lookup == null || x.Name == lookup))
                {
                    writer.Write(" - ");
                    writer.Write(Misc.TypePrettyName(property.PropertyType), ConsoleColor.Green);
                    writer.WriteLine($" {property.Name}");
                    writer.Write("     available for ");
                    if(property.IsCurrentlyGettable(CurrentBindingFlags))
                    {
                        writer.Write("'get'", ConsoleColor.Yellow);
                    }
                    if(property.IsCurrentlyGettable(CurrentBindingFlags) && property.IsCurrentlySettable(CurrentBindingFlags))
                    {
                        writer.Write(" and ");
                    }
                    if(property.IsCurrentlySettable(CurrentBindingFlags))
                    {
                        writer.Write("'set'", ConsoleColor.Yellow);
                    }
                    writer.WriteLine();
                }
                writer.Write("\n\rUsage:\n\r - ");
                writer.Write("get", ConsoleColor.Yellow);
                writer.Write($": {name} PropertyName\n\r - ");
                writer.Write("set", ConsoleColor.Yellow);
                writer.WriteLine($": {name} PropertyName Value\n\r");
            }

            if(info.Indexers != null && info.Indexers.Any(x => lookup == null || x.Name == lookup))
            {
                writer.WriteLine("\nThe following indexers are available:");
                foreach(var indexer in info.Indexers.Where(x => lookup == null || x.Name == lookup))
                {
                    writer.Write(" - ");
                    writer.Write(Misc.TypePrettyName(indexer.PropertyType), ConsoleColor.Green);
                    writer.Write($" {indexer.Name}[");
                    var parameters = indexer.GetIndexParameters();
                    var lastParameter = parameters.LastOrDefault();
                    foreach(var param in parameters)
                    {
                        writer.Write(Misc.TypePrettyName(param.ParameterType), ConsoleColor.Green);
                        writer.Write($" {param.Name}");
                        if(param.IsOptional)
                        {
                            writer.Write(" = ");
                            if(param.DefaultValue == null)
                            {
                                writer.Write("null", ConsoleColor.DarkRed);
                            }
                            else
                            {
                                if(param.ParameterType.Name == "String")
                                {
                                    writer.Write("\"", ConsoleColor.DarkRed);
                                }
                                writer.Write(param.DefaultValue.ToString(), ConsoleColor.DarkRed);
                                if(param.ParameterType.Name == "String")
                                {
                                    writer.Write("\"", ConsoleColor.DarkRed);
                                }
                            }
                        }
                        if(lastParameter != param)
                        {
                            writer.Write(", ");
                        }
                    }
                    writer.Write("]     available for ");
                    if(indexer.IsCurrentlyGettable(CurrentBindingFlags))
                    {
                        writer.Write("'get'", ConsoleColor.Yellow);
                    }
                    if(indexer.IsCurrentlyGettable(CurrentBindingFlags) && indexer.IsCurrentlySettable(CurrentBindingFlags))
                    {
                        writer.Write(" and ");
                    }
                    if(indexer.IsCurrentlySettable(CurrentBindingFlags))
                    {
                        writer.Write("'set'", ConsoleColor.Yellow);
                    }
                    writer.WriteLine();
                }
                writer.Write("\n\rUsage:\n\r - ");
                writer.Write("get", ConsoleColor.Yellow);
                writer.Write($": {name} IndexerName [param1 param2 ...]\n\r - ");
                writer.Write("set", ConsoleColor.Yellow);
                writer.WriteLine($": {name} IndexerName [param1 param2 ...] Value\n\r   IndexerName is optional if every indexer has the same name.");
            }

            if(info.Fields != null && info.Fields.Any(x => lookup == null || x.Name == lookup))
            {
                writer.WriteLine("\nThe following fields are available:");

                foreach(var field in info.Fields.Where(x => lookup == null || x.Name == lookup))
                {
                    writer.Write(" - ");
                    writer.Write(Misc.TypePrettyName(field.FieldType), ConsoleColor.Green);
                    writer.Write($" {field.Name}");
                    if(field.IsLiteral || field.IsInitOnly)
                    {
                        writer.Write(" (read only)");
                    }
                    writer.WriteLine("");
                }
                writer.Write("\n\rUsage:\n\r - ");
                writer.Write("get", ConsoleColor.Yellow);
                writer.Write($": {name} fieldName\n\r - ");
                writer.Write("set", ConsoleColor.Yellow);
                writer.WriteLine($": {name} fieldName Value\n\r");
            }
        }

        public IDisposable EnterMachineContext(IMachine newMachine)
        {
            var isOverridenOnEntry = IsMachineOverriden;
            var oldMachine = currentMachineOverride.Value;
            Action<IMachine> clearOldMachine = removedMachine =>
            {
                if(removedMachine == oldMachine)
                {
                    oldMachine = null;
                }
            };

            // The 'oldMahcine' must be deleted even if we are in a different context at the moment.
            RegisterRemoveHandler(oldMachine, clearOldMachine);

            currentMachineOverride.Value = newMachine;
            isMachineOverriden.Value = true;
            RegisterRemoveHandler(newMachine, OnOverrideMachineRemoved);

            return DisposableWrapper.New(() =>
            {
                isMachineOverriden.Value = isOverridenOnEntry;
                currentMachineOverride.Value = oldMachine;
                EmulationManager.Instance.CurrentEmulation.MachineRemoved -= clearOldMachine;
                RegisterRemoveHandler(oldMachine, OnOverrideMachineRemoved);
            });
        }

        public IMachine CurrentMachine
        {
            get
            {
                if(IsMachineOverriden)
                {
                    return currentMachineOverride.Value;
                }

                if(currentMachine == null && machineName != null)
                {
                    IMachine foundMachine;
                    EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(machineName, out foundMachine);

                    if(foundMachine == null)
                    {
                        // Could not find machine -> it must have been removed; do not search for it again.
                        machineName = null;
                    }
                    else
                    {
                        currentMachine = foundMachine;
                        RegisterRemoveHandler(foundMachine, OnMachineRemoved);
                    }
                }

                return currentMachine;
            }

            set
            {
                if(IsMachineOverriden)
                {
                    currentMachineOverride.Value = value;
                    RegisterRemoveHandler(value, OnOverrideMachineRemoved);
                }
                else
                {
                    currentMachine = value;
                    RegisterRemoveHandler(value, OnMachineRemoved);
                }
            }
        }

        public string MachineName => EmulationManager.Instance.CurrentEmulation.TryGetMachineName(CurrentMachine, out var name) ? name : null;

        public string MachineNameNormalized => NormalizeMachineName(MachineName);

        public bool IsMachineOverriden => isMachineOverriden.Value;

        public Monitor.NumberModes CurrentNumberFormat { get; set; }
            = ConfigurationManager.Instance.Get<Monitor.NumberModes>(Monitor.ConfigurationSection, "number-format", Monitor.NumberModes.Hexadecimal);

        public BindingFlags CurrentBindingFlags { get; set; }
            = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        public bool VerboseMode = false;

        public readonly MonitorPath MonitorPath = new MonitorPath(Environment.CurrentDirectory);

        public readonly Dictionary<string, Token> Variables = new Dictionary<string, Token>();
        public readonly Dictionary<string, Token> Macros = new Dictionary<string, Token>();
        public readonly Dictionary<string, Token> Aliases = new Dictionary<string, Token>();
        public readonly Dictionary<Monitor.VariableType, Dictionary<string, Token>> VariableCollections;

        public readonly List<string> Usings = new List<string>() { "sysbus." };

        public const string OriginVariable = $"{GlobalVariablePrefix}.ORIGIN";

        private static string NormalizeMachineName(string machineName) => machineName?.Replace("-", "_") ?? null;

        private static readonly MethodInfo selectInfo = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2)
            .Where(m =>
            {
                var selectorType = m.GetParameters()[1].ParameterType;
                return selectorType.IsGenericType && selectorType.GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .Single();

        private void OnOverrideMachineRemoved(IMachine removedMachine)
        {
            if(currentMachineOverride.Value == removedMachine)
            {
                currentMachineOverride.Value = null;
                EmulationManager.Instance.CurrentEmulation.MachineRemoved -= OnOverrideMachineRemoved;
            }
        }

        private void OnMachineRemoved(IMachine removedMachine)
        {
            if(removedMachine == currentMachine)
            {
                currentMachine = null;
                machineName = null;
                EmulationManager.Instance.CurrentEmulation.MachineRemoved -= OnMachineRemoved;
            }
        }

        private void RegisterRemoveHandler(IMachine machine, Action<IMachine> onRemoveAction)
        {
            EmulationManager.Instance.CurrentEmulation.MachineRemoved -= onRemoveAction;
            if(machine != null)
            {
                EmulationManager.Instance.CurrentEmulation.MachineRemoved += onRemoveAction;
            }
        }

        private void InitializeBindings()
        {
            StaticObjectDelegateMappings = new Dictionary<string, Func<object>>();
            ObjectDelegateMappings = new Dictionary<string, Func<object>>();

            Bind(Core.Machine.MachineKeyword, () => CurrentMachine);
            BindStatic("connector", () => EmulationManager.Instance.CurrentEmulation.Connector);
            BindStatic(EmulationToken, () => EmulationManager.Instance.CurrentEmulation);
            BindStatic("plugins", () => TypeManager.Instance.PluginManager);
            BindStatic("EmulationManager", () => EmulationManager.Instance);
            BindStatic("sockets", () => SocketsManager.Instance);
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            EmulationManager.Instance.CurrentEmulation.TryGetMachineName(CurrentMachine, out machineName);
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            InitializeBindings();
            currentMachineOverride = new ThreadLocal<IMachine>();
            isMachineOverriden = new ThreadLocal<bool>();

            // Right after deserialization the EmulationManager is not yet fully set up thus
            // currentMachine can't be retrieved here.
        }

        [Transient]
        private Dictionary<string, Func<object>> StaticObjectDelegateMappings;
        [Transient]
        private Dictionary<string, Func<object>> ObjectDelegateMappings;

        // Used for preserving machine name on serialization.
        private string machineName = null;

        [Transient]
        private IMachine currentMachine;

        [Transient]
        private ThreadLocal<IMachine> currentMachineOverride;
        [Transient]
        private ThreadLocal<bool> isMachineOverriden;

        private const string GlobalVariablePrefix = "global";

        private const string EmulationToken = "emulation";

        private const string CurrentDirectoryVariable = $"{GlobalVariablePrefix}.CWD";
    }
}
