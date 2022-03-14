//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections;
using Microsoft.CSharp.RuntimeBinder;
using AntShell.Commands;
using Dynamitey;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface.Exceptions;
using Antmicro.Renode.Core.Structure;
using System.Runtime.InteropServices;
using System.Globalization;

namespace Antmicro.Renode.UserInterface
{
    public partial class Monitor
    {
        public event Action Quitted;

        private readonly List<string> usings = new List<string>();

        public enum NumberModes
        {
            Hexadecimal,
            Decimal,
            Both
        }

        public NumberModes CurrentNumberFormat{ get; set; }

        public static string SanitizePathSeparator(string baseString)
        {
            var sanitizedFile = baseString.Replace("\\", "/");
            if(sanitizedFile.Contains("/ "))
            {
                sanitizedFile = sanitizedFile.Replace("/ ", "\\ ");
            }
            return sanitizedFile;
        }

        private bool RunCommand(ICommandInteraction writer, Command command, IList<Token> parameters)
        {
            var commandType = command.GetType();
            var runnables = commandType.GetMethods().Where(x => x.GetCustomAttributes(typeof(RunnableAttribute), true).Any());
            ICommandInteraction candidateWriter = null;
            MethodInfo foundCandidate = null;
            bool lastIsAccurateMatch = false;
            IEnumerable<object> preparedParameters = null;

            foreach(var candidate in runnables)
            {
                bool isAccurateMatch = false;
                var candidateParameters = candidate.GetParameters();
                var writers = candidateParameters.Where(x => typeof(ICommandInteraction).IsAssignableFrom(x.ParameterType)).ToList();

                var lastIsArray = candidateParameters.Length > 0
                                  && typeof(IEnumerable<Token>).IsAssignableFrom(candidateParameters[candidateParameters.Length - 1].ParameterType);

                if(writers.Count > 1
                //all but last (and optional writer) should be tokens
                   || candidateParameters.Skip(writers.Count).Take(candidateParameters.Length - writers.Count - 1).Any(x => !typeof(Token).IsAssignableFrom(x.ParameterType))
                //last one should be Token or IEnumerable<Token>
                   || (candidateParameters.Length > writers.Count
                   && !typeof(Token).IsAssignableFrom(candidateParameters[candidateParameters.Length - 1].ParameterType)
                   && !lastIsArray))
                {
                    throw new RecoverableException(String.Format("Method {0} of command {1} has invalid signature, will not process further. You should file a bug report.",
                        candidate.Name, command.Name));
                }
                IList<Token> parametersWithoutLastArray = null;
                IList<ParameterInfo> candidateParametersWithoutArrayAndWriters = null;
                if(lastIsArray)
                {
                    candidateParametersWithoutArrayAndWriters = candidateParameters.Skip(writers.Count).Take(candidateParameters.Length - writers.Count - 1).ToList();
                    if(parameters.Count < candidateParameters.Length - writers.Count) //without writer
                    {
                        continue;
                    }
                    parametersWithoutLastArray = parameters.Take(candidateParametersWithoutArrayAndWriters.Count()).ToList();
                }
                else
                {
                    candidateParametersWithoutArrayAndWriters = candidateParameters.Skip(writers.Count).ToList();
                    if(parameters.Count != candidateParameters.Length - writers.Count) //without writer
                    {
                        continue;
                    }
                    parametersWithoutLastArray = parameters;
                }
                //Check for types
                if(parametersWithoutLastArray.Zip(
                       candidateParametersWithoutArrayAndWriters,
                       (x, y) => new {FromUser = x.GetType(), FromMethod = y.ParameterType})
                    .Any(x => !x.FromMethod.IsAssignableFrom(x.FromUser)))
                {
                    continue;
                }

                bool constraintsOk = true;
                //Check for constraints
                for(var i = 0; i < parametersWithoutLastArray.Count; ++i)
                {
                    var attribute = candidateParametersWithoutArrayAndWriters[i].GetCustomAttributes(typeof(ValuesAttribute), true);
                    if(attribute.Any())
                    {
                        if(!((ValuesAttribute)attribute[0]).Values.Contains(parametersWithoutLastArray[i].GetObjectValue()))
                        {
                            constraintsOk = false;
                            break;
                        }
                    }
                }
                if(lastIsArray)
                {
                    var arrayParameters = parameters.Skip(parametersWithoutLastArray.Count()).ToArray();
                    var elementType = candidateParameters.Last().ParameterType.GetElementType();
                    if(!arrayParameters.All(x => elementType.IsAssignableFrom(x.GetType())))
                    {
                        constraintsOk = false;
                    }
                    else
                    {
                        var array = Array.CreateInstance(elementType, arrayParameters.Length);
                        for(var i = 0; i < arrayParameters.Length; ++i)
                        {
                            array.SetValue(arrayParameters[i], i);
                        }
                        preparedParameters = parametersWithoutLastArray.Concat(new object[] { array });
                    }
                }
                else
                {
                    preparedParameters = parameters;
                }

                if(!constraintsOk)
                {
                    continue;
                }

                if(!parametersWithoutLastArray.Zip(
                       candidateParametersWithoutArrayAndWriters,
                       (x, y) => new {FromUser = x.GetType(), FromMethod = y.ParameterType})
                    .Any(x => x.FromMethod != x.FromUser))
                {
                    isAccurateMatch = true;
                }
                if(foundCandidate != null && (lastIsAccurateMatch == isAccurateMatch)) // if one is not better than the other
                {
                    throw new RecoverableException(String.Format("Ambiguous choice between methods {0} and {1} of command {2}. You should file a bug report.",
                        foundCandidate.Name, candidate.Name, command.Name));
                }
                if(lastIsAccurateMatch) // previous was better
                {
                    continue;
                }
                foundCandidate = candidate;
                lastIsAccurateMatch = isAccurateMatch;

                if(writers.Count == 1)
                {
                    candidateWriter = writer;
                }
            }

            if(foundCandidate != null)
            {
                var parametersWithWriter = candidateWriter == null ? preparedParameters : new object[] { candidateWriter }.Concat(preparedParameters);
                try
                {
                    if(foundCandidate.ReturnType == typeof(bool))
                    {
                        return (bool)foundCandidate.Invoke(command, parametersWithWriter.ToArray());
                    }
                    else
                    {
                        foundCandidate.Invoke(command, parametersWithWriter.ToArray());
                    }
                }
                catch(TargetInvocationException e)
                {
                    // rethrow only the inner exception but with a nice stack trace
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }

                return true;
            }
            if(parameters.Any(x => x is VariableToken))
            {
                return RunCommand(writer, command, ExpandVariables(parameters));
            }
            writer.WriteError(String.Format("Bad parameters for command {0} {1}", command.Name, string.Join(" ", parameters.Select(x => x.OriginalValue))));
            command.PrintHelp(writer);
            writer.WriteLine();

            return false;
        }

        //TODO: unused, but maybe should be used.
        private void PrintPython(IEnumerable<Token> p, ICommandInteraction writer)
        {
            if(!p.Any())
            {
                writer.WriteLine("\nPython commands:");
                writer.WriteLine("===========================");
                foreach(var command in pythonRunner.GetPythonCommands())
                {
                    writer.WriteLine(command);
                }
                writer.WriteLine();
            }
        }

        #region Parsing

        private const string DefaultNamespace = "Antmicro.Renode.Peripherals.";

        private IEnumerable<String> GetDeviceSuggestions(string name)
        {
            var staticBound = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            if(currentMachine != null || staticBound != null || iface != null)
            {
                var boundObject = staticBound ?? FromMapping(name) ?? iface;
                if(boundObject != null)
                {
                    return GetMonitorInfo(boundObject.GetType()).AllNames;
                }

                Type device;
                string longestMatch;
                string actualName;
                if(TryFindPeripheralTypeByName(name, out device, out longestMatch, out actualName))
                {
                    return GetMonitorInfo(device).AllNames;
                }
            }
            return new List<String>();
        }

        private string GetResultFormat(object result, int num, int? width = null)
        {
            string format;
            if(result is int || result is long || result is uint || result is ushort || result is byte)
            {
                format = "0x{" + num + ":X";
            }
            else
            {
                format = "{" + num;
            }
            if(width.HasValue)
            {
                format = format + ",-" + width.Value;
            }
            format = format + "}";
            return format;
        }

        private string GetNumberFormat(NumberModes mode, int width)
        {
            return NumberFormats[mode].Replace("X", "X" + width);
        }

        private Dictionary<NumberModes, string> NumberFormats = new Dictionary<NumberModes, string> {
            { NumberModes.Both, "0x{0:X} ({0})" },
            { NumberModes.Decimal, "{0}" },
            { NumberModes.Hexadecimal, "0x{0:X}" },
        };

        private void PrintActionResult(object result, ICommandInteraction writer, bool withNewLine = true)
        {
            var endl = "";
            if(withNewLine)
            {
                endl = "\r\n"; //Cannot be Environment.NewLine, we need \r explicitly.
            }
            var enumerable = result as IEnumerable;
            if(result is int || result is long || result is uint || result is ushort || result is byte || result is ulong || result is short)
            {
                writer.Write(string.Format(CultureInfo.InvariantCulture, GetNumberFormat(CurrentNumberFormat, 2 * Marshal.SizeOf(result.GetType())) + endl, result));
            }
            else if(result is string[,])
            {
                var table = result as string[,];
                PrettyPrint2DArray(table, writer);
            }
            else if(result is IDictionary)
            {
                dynamic dict = result;
                var length = 0;
                foreach(var entry in dict)
                {
                    var value = entry.Key.ToString();
                    length = length > value.Length ? length : value.Length;
                }
                foreach(var entry in dict)
                {
                    var format = GetResultFormat(entry.Key, 0, length) + " : " + GetResultFormat(entry.Value, 1);
                    string entryResult = string.Format(CultureInfo.InvariantCulture, format, entry.Key, entry.Value); //DO NOT INLINE WITH WriteLine. May result with CS1973, but may even fail in runtime.
                    writer.WriteLine(entryResult);
                }
                return;
            }
            else if(enumerable != null && !(result is string))
            {
                var i = 0;
                writer.Write("[\r\n");
                foreach(var item in enumerable)
                {
                    ++i;
                    PrintActionResult(item, writer, false);
                    writer.Write(", ");
                    if(i % 10 == 0)
                    {
                        writer.Write("\r\n");
                    }
                }
                writer.Write("\r\n]" + endl);
            }
            else if(result is RawImageData image)
            {
                writer.WriteRaw(InlineImage.Encode(image.ToPng()));
            }
            else
            {
                writer.Write(string.Format(CultureInfo.InvariantCulture, "{0}" + endl, result));
            }
        }

        private static void PrettyPrint2DArray(string[,] table, ICommandInteraction writer)
        {
            var columnLengths = new int[table.GetLength(1)];
            for(var i = 0; i < columnLengths.Length; i++)
            {
                for(var j = 0; j < table.GetLength(0); j++)
                {
                    columnLengths[i] = Math.Max(table[j, i].Length, columnLengths[i]);
                }
            }
            var lineLength = columnLengths.Sum() + columnLengths.Length + 1;
            writer.WriteLine("".PadRight(lineLength, '-'));
            for(var i = 0; i < table.GetLength(0); i++)
            {
                if(i == 1)
                {
                    writer.WriteLine("".PadRight(lineLength, '-'));
                }
                writer.Write('|');
                for(var j = 0; j < table.GetLength(1); j++)
                {
                    writer.Write(table[i, j].PadRight(columnLengths[j]));
                    writer.Write('|');
                }
                writer.WriteLine();
            }
            writer.WriteLine("".PadRight(lineLength, '-'));
        }

        private void ProcessDeviceAction(Type device, string name, IEnumerable<Token> p, ICommandInteraction writer)
        {
            var devInfo = GetMonitorInfo(device);
            if(!p.Any())
            {
                if(devInfo != null)
                {
                    PrintMonitorInfo(name, devInfo, writer);
                }
            }
            else
            {
                object result;
                try
                {
                    result = ExecuteDeviceAction(device, name, p);
                }
                catch(ParametersMismatchException)
                {
                    if(devInfo != null)
                    {
                        PrintMonitorInfo(name, devInfo, writer, p.First().OriginalValue);
                    }
                    throw;
                }
                if(result != null)
                {
                    PrintActionResult(result, writer);

                    if(result.GetType().IsEnum)
                    {
                        writer.WriteLine("\nPossible values are:");
                        foreach(var entry in Enum.GetValues(result.GetType()))
                        {
                            writer.WriteLine(string.Format("\t{0} ({1})", entry, /*(int)*/entry));
                        }
                        writer.WriteLine();
                    }
                }
            }
        }

        private void ProcessDeviceActionByName(string name, IEnumerable<Token> p, ICommandInteraction writer)
        {
            var staticBound = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            if(currentMachine != null || staticBound != null || iface != null)
            { //special cases
                var boundElement = staticBound ?? FromMapping(name);
                if(boundElement != null)
                {
                    ProcessDeviceAction(boundElement.GetType(), name, p, writer);
                    return;
                }
                if(iface != null)
                {
                    ProcessDeviceAction(iface.GetType(), name, p, writer);
                    return;
                }

                Type device;
                string longestMatch;
                string actualName;
                if(TryFindPeripheralTypeByName(name, out device, out longestMatch, out actualName))
                {
                    ProcessDeviceAction(device, actualName, p, writer);
                }
                else
                {
                    if(longestMatch.Length > 0)
                    {
                        throw new RecoverableException(String.Format("Could not find device {0}, the longest match is {1}.", name, longestMatch));
                    }
                    throw new RecoverableException(String.Format("Could not find device {0}.", name));
                }
            }
        }

        private bool TryFindPeripheralTypeByName(string name, out Type type, out string longestMatch, out string actualName)
        {
            IPeripheral peripheral;
            type = null;
            longestMatch = string.Empty;
            actualName = name;
            string longestMatching;
            string currentMatch;
            string longestPrefix = string.Empty;
            var ret = currentMachine.TryGetByName(name, out peripheral, out longestMatching);
            longestMatch = longestMatching;

            if(!ret)
            {
                foreach(var prefix in usings)
                {
                    ret = currentMachine.TryGetByName(prefix + name, out peripheral, out currentMatch);
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
            if(ret)
            {
                type = peripheral.GetType();
            }
            return ret;
        }

        private bool TryFindPeripheralByName(string name, out IPeripheral peripheral, out string longestMatch)
        {
            longestMatch = string.Empty;

            if(currentMachine == null)
            {
                peripheral = null;
                return false;
            }

            string longestMatching;
            string currentMatch;
            string longestPrefix = string.Empty;
            var ret = currentMachine.TryGetByName(name, out peripheral, out longestMatching);
            longestMatch = longestMatching;

            if(!ret)
            {
                foreach(var prefix in usings)
                {
                    ret = currentMachine.TryGetByName(prefix + name, out peripheral, out currentMatch);
                    if(longestMatching.Split('.').Length < currentMatch.Split('.').Length - prefix.Split('.').Length)
                    {
                        longestMatching = currentMatch;
                        longestPrefix = prefix;
                    }
                    if(ret)
                    {
                        break;
                    }
                }
            }
            longestMatch = longestPrefix + longestMatching;
            return ret;
        }

        private static string TypePrettyName(Type type)
        {
            var genericArguments = type.GetGenericArguments();
            if(genericArguments.Length == 0)
            {
                return type.Name;
            }
            if(type.GetGenericTypeDefinition() == typeof(Nullable<>) && genericArguments.Length == 1)
            {
                return genericArguments.Select(x => TypePrettyName(x) + "?").First();
            }
            var typeDefeninition = type.Name;
            var unmangledName = typeDefeninition.Substring(0, typeDefeninition.IndexOf("`", StringComparison.Ordinal));
            return unmangledName + "<" + String.Join(",", genericArguments.Select(TypePrettyName)) + ">";
        }

        private void PrintMonitorInfo(string name, MonitorInfo info, ICommandInteraction writer, string lookup = null)
        {
            if(info == null)
            {
                return;
            }
            if(info.Methods != null && info.Methods.Any(x => lookup == null || x.Name == lookup))
            {
                writer.WriteLine("\nThe following methods are available:");

                foreach(var method in info.Methods.Where(x=> lookup == null || x.Name==lookup))
                {
                    writer.Write(" - ");
                    writer.Write(TypePrettyName(method.ReturnType), ConsoleColor.Green);
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
                    foreach(var param in parameters.Where(x=> !x.IsRetval))
                    {
                        if(param.IsOut)
                        {
                            writer.Write("out ", ConsoleColor.Yellow);
                        }
                        writer.Write(TypePrettyName(param.ParameterType), ConsoleColor.Green);
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

                foreach(var property in info.Properties.Where(x=> lookup==null || x.Name==lookup))
                {
                    writer.Write(" - ");
                    writer.Write(TypePrettyName(property.PropertyType), ConsoleColor.Green);
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
                foreach(var indexer in info.Indexers.Where(x=> lookup==null || x.Name==lookup))
                {
                    writer.Write(" - ");
                    writer.Write(TypePrettyName(indexer.PropertyType), ConsoleColor.Green);
                    writer.Write($" {indexer.Name}[");
                    var parameters = indexer.GetIndexParameters();
                    var lastParameter = parameters.LastOrDefault();
                    foreach(var param in parameters)
                    {
                        writer.Write(TypePrettyName(param.ParameterType), ConsoleColor.Green);
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

                foreach(var field in info.Fields.Where(x=> lookup==null || x.Name==lookup))
                {
                    writer.Write(" - ");
                    writer.Write(TypePrettyName(field.FieldType), ConsoleColor.Green);
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

        private class MachineWithWasPaused
        {
            public Machine Machine { get; set; }

            public bool WasPaused { get; set; }
        }

        public MonitorInfo GetMonitorInfo(Type device)
        {
            var info = new MonitorInfo();
            var methodsAndExtensions = new List<MethodInfo>();

            var methods = cache.Get(device, GetAvailableMethods);
            if(methods.Any())
            {
                methodsAndExtensions.AddRange(methods);
            }

            var properties = cache.Get(device, GetAvailableProperties);
            if(properties.Any())
            {
                info.Properties = properties.OrderBy(x => x.Name);
            }

            var indexers = cache.Get(device, GetAvailableIndexers);
            if(indexers.Any())
            {
                info.Indexers = indexers.OrderBy(x => x.Name);
            }

            var fields = cache.Get(device, GetAvailableFields);
            if(fields.Any())
            {
                info.Fields = fields.OrderBy(x => x.Name);
            }

            var extensions = cache.Get(device, GetAvailableExtensions);

            if(extensions.Any())
            {
                methodsAndExtensions.AddRange(extensions);
            }
            if(methodsAndExtensions.Any())
            {
                info.Methods = methodsAndExtensions.OrderBy(x => x.Name);
            }
            return info;
        }

        #endregion

        #region Device invocations

        public object ConvertValueOrThrowRecoverable(object value, Type type)
        {
            try
            {
                var convertedValue = ConvertValue(value, type);
                return convertedValue;
            }
            catch(Exception e)
            {
                if(e is FormatException || e is RuntimeBinderException || e is OverflowException || e is InvalidCastException)
                {
                    throw new RecoverableException(e);
                }
                throw;
            }
        }

        private object ConvertValue(object value, Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            if((!type.IsValueType || underlyingType != null) && value == null)
            {
                return null;
            }
            if(type.IsInstanceOfType(value))
            {
                return Dynamic.InvokeConvert(value, type, true);
            }
            if(value is string)
            {
                IPeripheral peripheral;
                string longestMatch;
                if(TryFindPeripheralByName((string)value, out peripheral, out longestMatch))
                {
                    if(type.IsInstanceOfType(peripheral))
                    {
                        return peripheral;
                    }
                }

                if(currentMachine != null)
                {
                    IPeripheralsGroup group;
                    if(currentMachine.PeripheralsGroups.TryGetByName((string)value, out group))
                    {
                        if(type.IsInstanceOfType(group))
                        {
                            return group;
                        }
                    }
                }

                IHostMachineElement @interface;
                if(Emulation.ExternalsManager.TryGetByName((string)value, out @interface))
                {
                    if(type.IsInstanceOfType(@interface))
                    {
                        return @interface;
                    }
                }

                IExternal external;
                if(Emulation.ExternalsManager.TryGetByName((string)value, out external))
                {
                    if(type.IsInstanceOfType(external))
                    {
                        return external;
                    }
                }
            }//intentionally no else (may be iconvertible or enum)
            if(type.IsEnum)
            {
                var valueString = value as string;
                if(valueString != null)
                {
                    if(!Enum.IsDefined(type, value))
                    {
                        throw new FormatException(String.Format("Enum value {0} is not defined for {1}!", value, type.Name));
                    }
                    return Enum.Parse(type, valueString);
                }
                var val = Enum.ToObject(type, value);

                if(!Enum.IsDefined(type, val) && !type.IsDefined(typeof(FlagsAttribute), false))
                {
                    throw new FormatException(String.Format("Enum value {0} is not defined for {1}!", value, type.Name));
                }
                return val;
            }
            if(underlyingType != null)
            {
                return ConvertValue(value, underlyingType);
            }
            return Dynamic.InvokeConvert(value, type, true);
        }

        private object IdentifyDevice(string name)
        {
            var device = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            device = device ?? FromMapping(name) ?? iface ?? (object)currentMachine[name];
            return device;
        }

        private object InvokeGet(string name, MemberInfo info)
        {
            var device = IdentifyDevice(name);
            var context = CreateInvocationContext(device, info);
            if(context != null)
            {
                return Dynamic.InvokeGet(context, info.Name);
            }
            else
            {
                var propInfo = info as PropertyInfo;
                var fieldInfo = info as FieldInfo;
                if(fieldInfo != null)
                {
                    return fieldInfo.GetValue(null);
                }
                if(propInfo != null)
                {
                    return propInfo.GetValue(!propInfo.IsStatic() ? device : null, null);
                }
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeGet", info.Name));
            }
        }

        private void InvokeSet(string name, MemberInfo info, object parameter)
        {
            var device = IdentifyDevice(name);
            var context = CreateInvocationContext(device, info);
            if(context != null)
            {
                Dynamic.InvokeSet(context, info.Name, parameter);
            }
            else
            {
                var propInfo = info as PropertyInfo;
                var fieldInfo = info as FieldInfo;
                if(fieldInfo != null)
                {
                    fieldInfo.SetValue(null, parameter);
                    return;
                }
                if(propInfo != null)
                {
                    propInfo.SetValue(!propInfo.IsStatic() ? device : null, parameter, null);
                    return;
                }
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeSet", info.Name));
            }
        }

        private object InvokeExtensionMethod(string name, MethodInfo method, List<object> parameters)
        {
            var device = IdentifyDevice(name);
            var context = InvokeContext.CreateStatic(method.ReflectedType);
            if(context != null)
            {
                return InvokeWithContext(context, method, (new [] { device }.Concat(parameters)).ToArray());
            }
            else
            {
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeExtensionMethod", method.Name));
            }
        }

        private object InvokeMethod(string name, MethodInfo method, List<object> parameters)
        {
            var device = IdentifyDevice(name);

            var context = CreateInvocationContext(device, method);
            if(context != null)
            {
                return InvokeWithContext(context, method, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeMethod", method.Name));
            }
        }

        private void InvokeSetIndex(string name, PropertyInfo property, List<object> parameters)
        {
            var device = IdentifyDevice(name);

            var context = CreateInvocationContext(device, property);
            if(context != null)
            {
                Dynamic.InvokeSetIndex(context, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeSetIndex", property.Name));
            }
        }

        private object InvokeGetIndex(string name, PropertyInfo property, List<object> parameters)
        {
            var device = IdentifyDevice(name);
            var context = CreateInvocationContext(device, property);
            if(context != null)
            {
                return Dynamic.InvokeGetIndex(context, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeGetIndex", property.Name));
            }
        }

        private object InvokeWithContext(InvokeContext context, MethodInfo method, object[] parameters)
        {
            if(method.ReturnType == typeof(void))
            {
                Dynamic.InvokeMemberAction(context, method.Name, parameters);
                return null;
            }
            else
            {
                return Dynamic.InvokeMember(context, method.Name, parameters);
            }
        }

        /// <summary>
        /// Creates the invocation context.
        /// </summary>
        /// <returns>The invokation context or null, if can't be handled by Dynamitey.</returns>
        /// <param name="device">Target device.</param>
        /// <param name="info">Field, property or method info.</param>
        private static InvokeContext CreateInvocationContext(object device, MemberInfo info)
        {
            if(info.IsStatic())
            {
                if(info is FieldInfo || info is PropertyInfo)
                {
                    //FieldInfo not supported in Dynamitey
                    return null;
                }
                return InvokeContext.CreateStatic(device.GetType());
            }
            var propertyInfo = info as PropertyInfo;
            if(propertyInfo != null)
            {
                //private properties not supported in Dynamitey
                if((propertyInfo.CanRead && propertyInfo.GetGetMethod(true).IsPrivate)
                   || (propertyInfo.CanWrite && propertyInfo.GetSetMethod(true).IsPrivate))
                {
                    return null;
                }
            }
            return InvokeContext.CreateContext(device, info.ReflectedType);
        }

        private IEmulationElement GetExternalInterfaceOrNull(string name)
        {
            IEmulationElement external;
            Emulation.ExternalsManager.TryGetByName(name, out external);
            return external;
        }

        private readonly HashSet<Tuple<Type, Type>> acceptableTokensTypes = new HashSet<Tuple<Type, Type>>() {
            { new Tuple<Type, Type>(typeof(string), typeof(StringToken)) },
            { new Tuple<Type, Type>(typeof(string), typeof(PathToken)) },
            { new Tuple<Type, Type>(typeof(int), typeof(DecimalIntegerToken)) },
            { new Tuple<Type, Type>(typeof(bool), typeof(BooleanToken)) },
            { new Tuple<Type, Type>(typeof(long), typeof(DecimalIntegerToken)) },
            { new Tuple<Type, Type>(typeof(short), typeof(DecimalIntegerToken)) },
        };

        private bool TryPrepareParameters(IList<Token> values, IList<ParameterInfo> parameters, out List<object> result)
        {

            result = new List<object>();
            int autoFilledCount = 0;
            //this might be expanded - try all parameters with the attribute, try to fill from factory based on it's type
            if(parameters.Count > 0 && parameters[0].ParameterType == typeof(Machine)
                && Attribute.IsDefined(parameters[0], typeof(AutoParameterAttribute)))
            {
                result.Add(currentMachine);
                autoFilledCount++;
            }

            //Too many arguments
            if(values.Count > parameters.Count)
            {
                return false;
            }

            try
            {
                int i;
                //Convert all given parameters
                for(i = 0; i < values.Count; ++i)
                {
                    var tokenTypes = acceptableTokensTypes.Where(x => x.Item1 == parameters[i + autoFilledCount].ParameterType).ToList();
                    if(tokenTypes.Any())
                    {
                        var isOk = false;
                        foreach(var tokenType in tokenTypes)
                        {
                            if(tokenType.Item2.IsInstanceOfType(values[i]))
                            {
                                isOk = true;
                                break;
                            }
                        }
                        if(!isOk)
                        {
                            return false;
                        }
                    }
                    result.Add(ConvertValue(values[i].GetObjectValue(), parameters[i + autoFilledCount].ParameterType));
                }
                i += autoFilledCount;
                //If not enough parameters, check for their default values
                if(i < parameters.Count)
                {
                    for(; i < parameters.Count; ++i)
                    {
                        if(parameters[i].IsOptional)
                        {
                            result.Add(parameters[i].DefaultValue);
                        }
                        else
                        {
                            return false; //non-optional parameter encountered
                        }
                    }
                }
            }
            catch(Exception e)
            {
                if(e is FormatException || e is RuntimeBinderException || e is OverflowException || e is InvalidCastException)
                {
                    return false;
                }
                throw;
            }
            return true;
        }

        public object ExecuteDeviceAction(Type type, string name, IEnumerable<Token> p)
        {
            string commandValue;
            var command = p.FirstOrDefault();
            if(command is LiteralToken || command is LeftBraceToken)
            {
                commandValue = command.GetObjectValue() as string;
            }
            else
            {
                throw new RecoverableException("Bad syntax");
            }

            var methods = cache.Get(type, GetAvailableMethods);
            var fields = cache.Get(type, GetAvailableFields);
            var properties = cache.Get(type, GetAvailableProperties);
            var indexers = cache.Get(type, GetAvailableIndexers).ToList();
            var extensions = cache.Get(type, GetAvailableExtensions);

            var foundMethods = methods.Where(x => x.Name == commandValue).ToList();
            var foundField = fields.FirstOrDefault(x => x.Name == commandValue);
            var foundProp = properties.FirstOrDefault(x => x.Name == commandValue);
            var foundExts = extensions.Where(x => x.Name == commandValue).ToList();
            var foundIndexers = command is LeftBraceToken && indexers.Any() && indexers.All(x => x.Name == indexers[0].Name) //can use default indexer
                                ? indexers.ToList() : indexers.Where(x => x.Name == commandValue).ToList();

            var parameterArray = p.Skip(command is LeftBraceToken ? 0 : 1).ToArray(); //Don't skip left brace, proper code to do that is below.

            var setValue = parameterArray.FirstOrDefault();

            if(foundMethods.Any())
            {
                foreach(var foundMethod in foundMethods.OrderBy(x=>x.GetParameters().Count())
                        .ThenBy(y=>y.GetParameters().Count(z=>z.ParameterType==typeof(String))))
                {
                    var methodParameters = foundMethod.GetParameters();

                    List<object> parameters;
                    if(TryPrepareParameters(parameterArray, methodParameters, out parameters))
                    {
                        return InvokeMethod(name, foundMethod, parameters);
                    }

                }
                if(!foundExts.Any())
                {
                    throw new ParametersMismatchException();
                }
            }
            if(foundExts.Any())
            { //intentionaly no 'else' - extensions may override methods as well
                foreach(var foundExt in foundExts.OrderBy(x=>x.GetParameters().Count())
                        .ThenBy(y=>y.GetParameters().Count(z=>z.ParameterType==typeof(String))))
                {
                    var extensionParameters = foundExt.GetParameters().Skip(1).ToList();
                    List<object> parameters;
                    if(TryPrepareParameters(parameterArray, extensionParameters, out parameters))
                    {
                        return InvokeExtensionMethod(name, foundExt, parameters);
                    }
                }
                throw new ParametersMismatchException();

            }
            else if(foundField != null)
            {
                if(setValue != null && !foundField.IsLiteral && !foundField.IsInitOnly)
                {
                    object value;
                    try
                    {
                        value = ConvertValue(setValue.GetObjectValue(), foundField.FieldType);
                    }
                    catch(Exception e)
                    {
                        if(e is FormatException || e is RuntimeBinderException)
                        {
                            throw new RecoverableException(e);
                        }
                        throw;
                    }
                    InvokeSet(name, foundField, value);
                    return null;
                }
                else
                {
                    return InvokeGet(name, foundField);
                }
            }
            else if(foundProp != null)
            {
                if(setValue != null && foundProp.IsCurrentlySettable(CurrentBindingFlags))
                {
                    object value;
                    try
                    {
                        value = ConvertValue(setValue.GetObjectValue(), foundProp.PropertyType);
                    }
                    catch(Exception e)
                    {
                        if(e is FormatException || e is RuntimeBinderException)
                        {
                            throw new RecoverableException(e);
                        }
                        throw;
                    }
                    InvokeSet(name, foundProp, value);
                    return null;
                }
                else if(foundProp.IsCurrentlyGettable(CurrentBindingFlags))
                {
                    return InvokeGet(name, foundProp);
                }
                else
                {
                    throw new RecoverableException(String.Format(
                        "Could not execute this action on property {0}",
                        foundProp.Name
                    )
                    );
                }
            }
            else if(foundIndexers.Any())
            {
                setValue = null;
                if(parameterArray.Length < 3 || !(parameterArray[0] is LeftBraceToken))
                {
                    throw new ParametersMismatchException();
                }
                var index = parameterArray.IndexOf(x => x is RightBraceToken);
                if(index == -1)
                {
                    throw new ParametersMismatchException();
                }
                if(index == parameterArray.Length - 2)
                {
                    setValue = parameterArray[parameterArray.Length - 1];
                }
                else if(index != parameterArray.Length - 1)
                {
                    throw new ParametersMismatchException();
                }
                var getParameters = parameterArray.Skip(1).Take(index - 1).ToArray();
                foreach(var foundIndexer in foundIndexers.OrderBy(x=>x.GetIndexParameters ().Count())
                         .ThenByDescending(y=>y.GetIndexParameters().Count(z=>z.ParameterType==typeof(String))))
                {
                    List<object> parameters;
                    var indexerParameters = foundIndexer.GetIndexParameters();

                    object value;
                    if(TryPrepareParameters(getParameters, indexerParameters, out parameters))
                    {
                        try
                        {
                            if(setValue != null && foundIndexer.IsCurrentlySettable(CurrentBindingFlags))
                            {
                                value = ConvertValue(setValue.GetObjectValue(), foundIndexer.PropertyType);


                                InvokeSetIndex(name, foundIndexer, parameters.Concat(new[] { value }).ToList());
                                return null;
                            }
                            else
                            {
                                return InvokeGetIndex(name, foundIndexer, parameters);
                            }
                        }
                        catch(Exception e)
                        {
                            if(e is FormatException || e is RuntimeBinderException || e is KeyNotFoundException)
                            {
                                throw new RecoverableException(e);
                            }
                            throw;
                        }
                    }
                }
                throw new ParametersMismatchException();
            }
            if(command is LiteralToken)
            {
                throw new RecoverableException(String.Format("{1} does not provide a field, method or property {0}.", command.GetObjectValue(), name));
            }
            else
            {
                throw new RecoverableException(String.Format("{0} does not provide a default-named indexer.", name));
            }
        }

        IEnumerable<PropertyInfo> GetAvailableIndexers(Type objectType)
        {
            var properties = new List<PropertyInfo>();
            var type = objectType;
            while(type != null && type != typeof(object))
            {
                properties.AddRange(type.GetProperties(CurrentBindingFlags)
                                    .Where(x => x.IsCallableIndexer())
                );
                type = type.BaseType;
            }
            return properties.DistinctBy(x=> x.ToString()); //Look @ GetAvailableMethods for explanation.
        }

        IEnumerable<PropertyInfo> GetAvailableProperties(Type objectType)
        {
            var properties = new List<PropertyInfo>();
            var type = objectType;
            while(type != null && type != typeof(object))
            {
                properties.AddRange(type.GetProperties(CurrentBindingFlags)
                                    .Where(x => x.IsCallable())
                );
                type = type.BaseType;
            }
            return properties.DistinctBy(x=> x.ToString()); //Look @ GetAvailableMethods for explanation.
        }

        private IEnumerable<FieldInfo> GetAvailableFields(Type objectType)
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
            return fields.DistinctBy(x=> x.ToString()); //Look @ GetAvailableMethods for explanation.
        }

        private IEnumerable<MethodInfo> GetAvailableMethods(Type objectType)
        {
            var methods = new List<MethodInfo>();
            var type = objectType;
            while(type != null && type != typeof(object))
            {
                methods.AddRange(type.GetMethods(CurrentBindingFlags)
                                 .Where(x => !(x.IsSpecialName
                && (x.Name.StartsWith("get_", StringComparison.Ordinal) || x.Name.StartsWith("set_", StringComparison.Ordinal)
                || x.Name.StartsWith("add_", StringComparison.Ordinal) || x.Name.StartsWith("remove_", StringComparison.Ordinal)))
                && !x.IsAbstract
                && !x.IsConstructor
                && !x.IsGenericMethod
                && x.IsCallable()
                )
                );
                type = type.BaseType;
            }
            return methods.DistinctBy(x=> x.ToString()); //This acutally gives us a full, easily comparable signature. Brilliant solution to avoid duplicates from overloaded methods.
        }

        private IEnumerable<MethodInfo> GetAvailableExtensions(Type type) => TypeManager.Instance.GetExtensionMethods(type).Where(y => y.IsExtensionCallable()).OrderBy(y => y.Name);

        public void ClearCache()
        {
            cache.ClearCache();
        }

        public BindingFlags CurrentBindingFlags { get; set; }
        private readonly SimpleCache cache = new SimpleCache();

        #endregion
    }
}

