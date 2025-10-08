//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface.Exceptions;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

using AntShell.Commands;

using Dynamitey;

using Microsoft.CSharp.RuntimeBinder;

namespace Antmicro.Renode.UserInterface
{
    public partial class Monitor
    {
        public static string SanitizePathSeparator(string baseString)
        {
            var sanitizedFile = baseString.Replace("\\", "/");
            if(sanitizedFile.Contains("/ "))
            {
                sanitizedFile = sanitizedFile.Replace("/ ", "\\ ");
            }
            return sanitizedFile;
        }

        public void ClearCache()
        {
            cache.ClearCache();
        }

        public object ConvertValueOrThrowRecoverable(object value, Type type)
        {
            try
            {
                var convertedValue = ConvertValue(value, type);
                return convertedValue;
            }
            catch(Exception e)
            {
                if(e is FormatException || e is RuntimeBinderException || e is OverflowException || e is InvalidCastException || e is ArgumentException)
                {
                    throw new RecoverableException(e);
                }
                throw;
            }
        }

        public object ExecuteDeviceAction(string name, object device, IEnumerable<Token> p)
        {
            string commandValue;
            var type = device.GetType();
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

            if((commandValue == SelectCommand || commandValue == ForEachCommand) && parameterArray.Any() && device is IEnumerable enumerable)
            {
                // Match on non-generic IEnumerable and .Cast<object> to box IEnumerable<int> for example.
                var result = enumerable.Cast<object>().Select(o => RecursiveExecuteDeviceAction("", o, p, 1)).ToList();
                return commandValue == SelectCommand ? result : null;
            }
            if(foundMethods.Any())
            {
                foreach(var foundMethod in foundMethods.OrderBy(x => x.GetParameters().Count())
                        .ThenBy(y => y.GetParameters().Count(z => z.ParameterType == typeof(String))))
                {
                    var methodParameters = foundMethod.GetParameters();

                    List<object> parameters;
                    if(TryPrepareParameters(parameterArray, methodParameters, out parameters))
                    {
                        return InvokeMethod(device, foundMethod, parameters);
                    }
                }
                if(!foundExts.Any())
                {
                    throw new ParametersMismatchException(type, commandValue, name);
                }
            }
            if(foundExts.Any())
            { //intentionaly no 'else' - extensions may override methods as well
                foreach(var foundExt in foundExts.OrderBy(x => x.GetParameters().Count())
                        .ThenBy(y => y.GetParameters().Count(z => z.ParameterType == typeof(String))))
                {
                    var extensionParameters = foundExt.GetParameters().Skip(1).ToList();
                    List<object> parameters;
                    if(TryPrepareParameters(parameterArray, extensionParameters, out parameters))
                    {
                        return InvokeExtensionMethod(device, foundExt, parameters);
                    }
                }
                throw new ParametersMismatchException(type, commandValue, name);
            }
            else if(foundField != null)
            {
                TokenList setValue = null;
                if(!ParseOptionalArgument(parameterArray, out setValue))
                {
                    throw new RecoverableException($"Failed to parse argument: {Misc.PrettyPrintCollection(parameterArray)}");
                }
                //if setValue is a LiteralToken then it must contain the next command to process in recursive call
                if(CanTypeBeChained(foundField.FieldType) && setValue?.FirstOrDefault() is LiteralToken)
                {
                    var currentObject = InvokeGet(device, foundField);
                    var objectFullName = $"{name} {commandValue}";
                    return RecursiveExecuteDeviceAction(objectFullName, currentObject, p, 1);
                }
                else if(setValue != null && !foundField.IsLiteral && !foundField.IsInitOnly)
                {
                    if(!FitArgumentType(setValue, foundField.FieldType, out var value))
                    {
                        throw new RecoverableException($"Could not convert {setValue} to {foundField.FieldType}");
                    }
                    InvokeSet(device, foundField, value);
                    return null;
                }
                else
                {
                    return InvokeGet(device, foundField);
                }
            }
            else if(foundProp != null)
            {
                TokenList setValue = null;
                if(!ParseOptionalArgument(parameterArray, out setValue))
                {
                    throw new RecoverableException($"Failed to parse argument: {Misc.PrettyPrintCollection(parameterArray)}");
                }
                //if setValue is a LiteralToken then it must contain the next command to process in recursive call
                if(CanTypeBeChained(foundProp.PropertyType) && setValue?.FirstOrDefault() is LiteralToken)
                {
                    var currentObject = InvokeGet(device, foundProp);
                    var objectFullName = $"{name} {commandValue}";
                    return RecursiveExecuteDeviceAction(objectFullName, currentObject, p, 1);
                }
                else if(setValue != null && foundProp.IsCurrentlySettable(CurrentBindingFlags))
                {
                    if(!FitArgumentType(setValue, foundProp.PropertyType, out var value))
                    {
                        throw new RecoverableException($"Could not convert {setValue} to {foundProp.PropertyType}");
                    }
                    InvokeSet(device, foundProp, value);
                    return null;
                }
                else if(foundProp.IsCurrentlyGettable(CurrentBindingFlags))
                {
                    return InvokeGet(device, foundProp);
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
                var i = 0;
                if(!ParseArgument(parameterArray, ref i, out var index))
                {
                    throw new RecoverableException($"Failed to parse index from {Misc.PrettyPrintCollection(parameterArray)}");
                }
                if(!ParseOptionalArgument(parameterArray, out var value, ++i))
                {
                    throw new RecoverableException($"Failed to parse value from {Misc.PrettyPrintCollection(parameterArray.Skip(i))}");
                }
                foreach(var foundIndexer in foundIndexers.OrderBy(x => x.GetIndexParameters().Count())
                         .ThenByDescending(y => y.GetIndexParameters().Count(z => z.ParameterType == typeof(String))))
                {
                    List<object> parameters;
                    var indexerParameters = foundIndexer.GetIndexParameters();

                    if(TryPrepareParameters(index.Tokens, indexerParameters, out parameters))
                    {
                        if(value != null && foundIndexer.IsCurrentlySettable(CurrentBindingFlags))
                        {
                            if(!FitArgumentType(value, foundIndexer.PropertyType, out var convertedValue))
                            {
                                throw new RecoverableException($"Could not convert {value} to {foundIndexer.PropertyType}");
                            }

                            InvokeSetIndex(device, foundIndexer, parameters.Concat(new[] { convertedValue }).ToList());
                            return null;
                        }
                        else
                        {
                            return InvokeGetIndex(device, foundIndexer, parameters);
                        }
                    }
                }
                throw new ParametersMismatchException(type, commandValue, name);
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

        public object FindFieldOrProperty(object node, string name)
        {
            var type = node.GetType();
            var fields = cache.Get(type, GetAvailableFields);
            var properties = cache.Get(type, GetAvailableProperties);
            var foundField = fields.FirstOrDefault(x => x.Name == name);
            var foundProp = properties.FirstOrDefault(x => x.Name == name);

            if(foundProp?.GetMethod != null)
            {
                return InvokeGet(node, foundProp);
            }
            if(foundField != null)
            {
                return InvokeGet(node, foundField);
            }

            return null;
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

        public NumberModes CurrentNumberFormat { get; set; }

        public BindingFlags CurrentBindingFlags { get; set; }

        public event Action Quitted;

        private static string GetPossibleEnumValues(Type type)
        {
            if(!type.IsEnum)
            {
                throw new ArgumentException("Type is not Enum!", nameof(type));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Possible values are:");
            foreach(var name in Enum.GetNames(type))
            {
                builder.AppendLine($"\t{name}");
            }
            builder.AppendLine();

            if(type == typeof(ExecutionMode))
            {
                var emulation = EmulationManager.Instance.CurrentEmulation;
                var isBlocking = emulation.SingleStepBlocking;
                var blockingString = isBlocking ? "blocking" : "non-blocking";
                builder.AppendLine($"{nameof(ExecutionMode.SingleStep)} is {blockingString}. It can be changed with:");
                builder.AppendLine($"\t{EmulationToken} {nameof(emulation.SingleStepBlocking)} {!isBlocking}");
            }
            return builder.ToString();
        }

        /// <summary>
        /// Parses an optional argument, which can be a single token like "a", or a list like [1, 2, 3],
        /// starting at index <paramref name="i"/> within <paramref name="tokens"/>. The resulting
        /// <paramref name="arg"/> will be a <see cref="TokenList"/> representing the argument,
        /// or null if no argument was present.
        /// </summary>
        private static bool ParseOptionalArgument(IList<Token> tokens, out TokenList arg, int i = 0)
        {
            if(tokens.Count <= i)
            {
                arg = null;
                return true;
            }
            return ParseArgument(tokens, ref i, out arg);
        }

        /// <summary>
        /// Parses a single argument, which can be a single token like "a", or a list like [1, 2, 3],
        /// starting at index <paramref name="i"/> within <paramref name="tokens"/>. The resulting
        /// <paramref name="arg"/> will be a <see cref="TokenList"/> representing the argument,
        /// with <paramref name="i"/> updated to point at the last parsed token.
        /// </summary>
        private static bool ParseArgument(IList<Token> tokens, ref int i, out TokenList arg)
        {
            arg = null;
            if(tokens[i] is LeftBraceToken)
            {
                var result = new TokenList(isArray: true);
                while(++i < tokens.Count && !(tokens[i] is RightBraceToken))
                {
                    result.Tokens.Add(tokens[i]);
                    var next = tokens.ElementAtOrDefault(i + 1);
                    if(next is CommaToken)
                    {
                        i++;
                    }
                    else if(!(next is RightBraceToken))
                    {
                        return false;
                    }
                }
                if(i == tokens.Count)
                {
                    return false;
                }
                arg = result;
                return true;
            }
            else
            {
                arg = TokenList.Single(tokens[i]);
                return true;
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

        private object InvokeGetIndex(object device, PropertyInfo property, List<object> parameters)
        {
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

        private bool TryParseTokenForParamType(Token token, Type type, out object result)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            var tokenTypes = acceptableTokensTypes.Where(x => x.Item1 == (underlyingType ?? type));
            var isNull = (underlyingType != null || !type.IsValueType) && token is NullToken;
            //If this result type is limited to specific token types, and this is not one of them, fail
            if(tokenTypes.Any() && !(isNull || tokenTypes.Any(tt => tt.Item2.IsInstanceOfType(token))))
            {
                result = null;
                return false;
            }
            result = ConvertValueOrThrowRecoverable(token.GetObjectValue(), type);
            return true;
        }

        private bool FitArgumentType(TokenList tokens, Type paramType, out object result)
        {
            result = default;
            Type elemType;
            var isGenericList = typeof(IList).IsAssignableFrom(paramType) && paramType.IsGenericType;
            if(isGenericList)
            {
                elemType = paramType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))?.GenericTypeArguments?.Single();
                if(elemType == null)
                {
                    throw new ArgumentException("Got a non-generic list somehow.");
                }
            }
            else if(paramType.IsArray)
            {
                elemType = paramType.GetElementType();
            }
            else
            {
                elemType = paramType;
            }
            if(tokens.IsArray != (paramType.IsArray || isGenericList))
            {
                return false;
            }
            var genericListT = typeof(List<>).MakeGenericType(new [] { elemType });
            var list = (IList)Activator.CreateInstance(genericListT);

            foreach(var token in tokens)
            {
                if(!TryParseTokenForParamType(token, elemType, out var parsedValue))
                {
                    return false;
                }
                // For simple parameters (not arrays), the parameter type is equal to the element type which means
                // we only expect, and will accept, one element. If we got an array in this case we will have already
                // rejected it above, so now the correct thing to do is return this single element immediately instead
                // of accumulating it into a list.
                if(paramType == elemType)
                {
                    result = parsedValue;
                    return true;
                }
                list.Add(parsedValue);
            }

            if(isGenericList)
            {
                result = list;
                return true;
            }
            else if(paramType.IsArray)
            {
                var arr = Array.CreateInstance(elemType, list.Count);
                for(var i = 0; i < list.Count; ++i)
                {
                    arr.SetValue(list[i], i);
                }
                result = arr;
                return true;
            }

            throw new InvalidOperationException($"Unhandled parameter type {paramType}");
        }

        private void InvokeSetIndex(object device, PropertyInfo property, List<object> parameters)
        {
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

        private bool TryPrepareParameters(IList<Token> values, IList<ParameterInfo> parameters, out List<object> result)
        {
            result = new List<object>();
            //this might be expanded - try all parameters with the attribute, try to fill from factory based on it's type
            if(parameters.Count > 0 && typeof(IMachine).IsAssignableFrom(parameters[0].ParameterType)
                && Attribute.IsDefined(parameters[0], typeof(AutoParameterAttribute)))
            {
                result.Add(CurrentMachine);
                parameters = parameters.Skip(1).ToList();
            }

            //The last parameter can be a param array
            Type paramArrayType = null;
            Type paramArrayElementType = null;
            string paramArrayName = null;
            var lastParam = parameters.LastOrDefault();
            if(lastParam?.IsDefined(typeof(ParamArrayAttribute)) ?? false)
            {
                parameters = parameters.Take(parameters.Count - 1).ToList();
                paramArrayType = lastParam.ParameterType;
                paramArrayElementType = paramArrayType.GetElementType();
                paramArrayName = lastParam.Name;
            }

            var indexedValues = new Dictionary<int, TokenList>();
            var allowPositional = true;
            for(int i = 0, currentPos = 0; i < values.Count; ++i, ++currentPos)
            {
                //Parse named arguments
                if(i < values.Count - 2
                    && values[i] is LiteralToken lit
                    && values[i + 1] is EqualityToken)
                {
                    //Treat the params array as the position one after the last
                    var parameterIndex = lit.Value == paramArrayName ? parameters.Count : parameters.IndexOf(p => p.Name == lit.Value);
                    //Fail on nonexistent or duplicate names
                    if(parameterIndex == -1 || indexedValues.ContainsKey(parameterIndex))
                    {
                        return false;
                    }
                    //Disallow further positional arguments only if the name doesn't match the position
                    //For example, for f(a=0, b=0) `f a=4 9` is allowed, like in C#
                    allowPositional &= parameterIndex == currentPos;
                    i += 2; //Skip the name and = sign
                    if(!ParseArgument(values, ref i, out var arg))
                    {
                        return false;
                    }
                    indexedValues[parameterIndex] = arg;
                }
                else
                {
                    //If we have filled all positional slots then allow further positional arguments
                    //no matter what. This is used for a params T[] after named parameters
                    if(!allowPositional && currentPos < parameters.Count)
                    {
                        return false;
                    }
                    if(!ParseArgument(values, ref i, out var arg))
                    {
                        return false;
                    }
                    indexedValues[currentPos] = arg;
                }
            }

            //Too many arguments and no trailing params T[]
            var valueCount = indexedValues.Count;
            if(valueCount > parameters.Count && paramArrayElementType == null)
            {
                return false;
            }

            //Grab all arguments that we can treat as positional off the front
            List<TokenList> positionalValues = new List<TokenList>(valueCount);
            for(int i = 0; i < valueCount; ++i)
            {
                if(!indexedValues.TryGetValue(i, out var value))
                {
                    break;
                }
                indexedValues.Remove(i);
                positionalValues.Add(value);
            }

            try
            {
                int i;
                //Convert all given positional parameters
                for(i = 0; i < positionalValues.Count; ++i)
                {
                    var paramType = parameters.ElementAtOrDefault(i)?.ParameterType ?? (positionalValues[i].IsArray ? paramArrayType : paramArrayElementType);
                    if(!FitArgumentType(positionalValues[i], paramType, out var current))
                    {
                        return false;
                    }
                    result.Add(current);
                }
                //If not enough parameters, check for default values and named parameters
                if(i < parameters.Count)
                {
                    for(; i < parameters.Count; ++i)
                    {
                        //See if it was passed as a named parameter
                        if(indexedValues.TryGetValue(i, out var value))
                        {
                            var paramType = parameters[i].ParameterType;
                            if(!FitArgumentType(value, paramType, out var current))
                            {
                                return false;
                            }
                            result.Add(current);
                        }
                        else if(parameters[i].IsOptional)
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
            catch(RecoverableException)
            {
                //The 'expected' exceptions from conversion will have been wrapped; propagate all other exceptions
                return false;
            }
            return true;
        }

        private bool CanTypeBeChained(Type type)
        {
            return !type.IsEnum && !type.IsValueType && type != typeof(string);
        }

        private object RecursiveExecuteDeviceAction(string name, object currentObject, IEnumerable<Token> p, int tokensToSkip)
        {
            if(currentObject == null)
            {
                return null;
            }
            return ExecuteDeviceAction(name, currentObject, p.Skip(tokensToSkip));
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
            return fields.DistinctBy(x => x.ToString()); //Look @ GetAvailableMethods for explanation.
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
            var enumerableType = objectType.GetEnumerableElementType();
            if(enumerableType != null)
            {
                methods.Add(selectInfo.MakeGenericMethod(new[] { enumerableType, typeof(object) }));
                methods.Add(typeof(List<>).MakeGenericType(new[] { enumerableType }).GetMethod(nameof(List<object>.ForEach)));
            }
            return methods.DistinctBy(x => x.ToString()); //This acutally gives us a full, easily comparable signature. Brilliant solution to avoid duplicates from overloaded methods.
        }

        private IEnumerable<MethodInfo> GetAvailableExtensions(Type type) => TypeManager.Instance.GetExtensionMethods(type).Where(y => y.IsExtensionCallable()).OrderBy(y => y.Name);

        private IEmulationElement GetExternalInterfaceOrNull(string name)
        {
            IEmulationElement external;
            Emulation.ExternalsManager.TryGetByName(name, out external);
            return external;
        }

        private object InvokeMethod(object device, MethodInfo method, List<object> parameters)
        {
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

        private object ConvertValue(object value, Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            if((!type.IsValueType || underlyingType != null) && value == null)
            {
                return null;
            }
            if(value is bool && !type.IsInstanceOfType(value))
            {
                throw new FormatException(String.Format(
                    "Bool value {0} is not convertible to {1}!",
                    value, type.Name
                ));
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

                if(CurrentMachine != null)
                {
                    IPeripheralsGroup group;
                    if(CurrentMachine.PeripheralsGroups.TryGetByName((string)value, out group))
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
                if(value is string valueString)
                {
                    if(Enum.IsDefined(type, value))
                    {
                        return Enum.Parse(type, valueString);
                    }
                }
                else
                {
                    var enumValue = Enum.ToObject(type, value);
                    if(Enum.IsDefined(type, enumValue) || type.IsDefined(typeof(FlagsAttribute), false))
                    {
                        return enumValue;
                    }
                }
                throw new FormatException(String.Format(
                    "Enum value {0} is not defined for {1}!\n\n{2}",
                    value, type.Name, GetPossibleEnumValues(type)
                ));
            }
            if(underlyingType != null)
            {
                return ConvertValue(value, underlyingType);
            }
            return Dynamic.InvokeConvert(value, type, true);
        }

        private void InvokeSet(object device, MemberInfo info, object parameter)
        {
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
                       (x, y) => new { FromUser = x.GetType(), FromMethod = y.ParameterType })
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
                       (x, y) => new { FromUser = x.GetType(), FromMethod = y.ParameterType })
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

        private IEnumerable<String> GetObjectSuggestions(object node)
        {
            if(node != null)
            {
                return GetMonitorInfo(node.GetType()).AllNames;
            }
            return new List<String>();
        }

        private object GetDevice(string name)
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
                if(TryFindPeripheralByName(name, out device, out longestMatch))
                {
                    return device;
                }
            }
            return null;
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

        private object InvokeExtensionMethod(object device, MethodInfo method, List<object> parameters)
        {
            var context = InvokeContext.CreateStatic(method.ReflectedType);
            if(context != null)
            {
                return InvokeWithContext(context, method, (new[] { device }.Concat(parameters)).ToArray());
            }
            else
            {
                throw new NotImplementedException(String.Format("Unsupported field {0} in InvokeExtensionMethod", method.Name));
            }
        }

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
            else if(enumerable != null && !(result is string) && !enumerable.Cast<object>().Any(x => x == enumerable))
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

        private void ProcessDeviceActionByName(string name, IEnumerable<Token> p, ICommandInteraction writer)
        {
            var staticBound = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            if(CurrentMachine != null || staticBound != null || iface != null)
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
            var ret = CurrentMachine.TryGetByName(name, out peripheral, out longestMatching);
            longestMatch = longestMatching;

            if(!ret)
            {
                foreach(var prefix in usings)
                {
                    ret = CurrentMachine.TryGetByName(prefix + name, out peripheral, out currentMatch);
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

            if(CurrentMachine == null)
            {
                peripheral = null;
                return false;
            }

            string longestMatching;
            string currentMatch;
            string longestPrefix = string.Empty;
            var ret = CurrentMachine.TryGetByName(name, out peripheral, out longestMatching);
            longestMatch = longestMatching;

            if(!ret)
            {
                foreach(var prefix in usings)
                {
                    ret = CurrentMachine.TryGetByName(prefix + name, out peripheral, out currentMatch);
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

        private void PrintMonitorInfo(string name, MonitorInfo info, ICommandInteraction writer, string lookup = null)
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

                foreach(var property in info.Properties.Where(x => lookup == null || x.Name == lookup))
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
                foreach(var indexer in info.Indexers.Where(x => lookup == null || x.Name == lookup))
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

                foreach(var field in info.Fields.Where(x => lookup == null || x.Name == lookup))
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

        private object IdentifyDevice(string name)
        {
            var device = FromStaticMapping(name);
            var iface = GetExternalInterfaceOrNull(name);
            device = device ?? FromMapping(name) ?? iface ?? (object)CurrentMachine[name];
            return device;
        }

        private object InvokeGet(object device, MemberInfo info)
        {
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

        private void ProcessDeviceAction(Type deviceType, string name, IEnumerable<Token> p, ICommandInteraction writer)
        {
            var devInfo = GetMonitorInfo(deviceType);
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
                    var device = IdentifyDevice(name);
                    result = ExecuteDeviceAction(name, device, p);
                }
                catch(ParametersMismatchException e)
                {
                    var nodeInfo = GetMonitorInfo(e.Type);
                    if(nodeInfo != null)
                    {
                        PrintMonitorInfo(e.Name, nodeInfo, writer, e.Command);
                    }
                    throw;
                }
                if(result != null)
                {
                    PrintActionResult(result, writer);

                    if(result.GetType().IsEnum)
                    {
                        writer.WriteLine();
                        writer.WriteLine(GetPossibleEnumValues(result.GetType()));
                    }
                }
            }
        }

        private readonly Dictionary<NumberModes, string> NumberFormats = new Dictionary<NumberModes, string> {
            { NumberModes.Both, "0x{0:X} ({0})" },
            { NumberModes.Decimal, "{0}" },
            { NumberModes.Hexadecimal, "0x{0:X}" },
        };

        private readonly HashSet<Tuple<Type, Type>> acceptableTokensTypes = new HashSet<Tuple<Type, Type>>() {
            { new Tuple<Type, Type>(typeof(string), typeof(StringToken)) },
            { new Tuple<Type, Type>(typeof(string), typeof(PathToken)) },
            { new Tuple<Type, Type>(typeof(int), typeof(DecimalIntegerToken)) },
            { new Tuple<Type, Type>(typeof(bool), typeof(BooleanToken)) },
            { new Tuple<Type, Type>(typeof(long), typeof(DecimalIntegerToken)) },
            { new Tuple<Type, Type>(typeof(short), typeof(DecimalIntegerToken)) },
        };

        private readonly SimpleCache cache = new SimpleCache();
        private static readonly MethodInfo selectInfo = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2)
            .Where(m =>
            {
                var selectorType = m.GetParameters()[1].ParameterType;
                return selectorType.IsGenericType && selectorType.GetGenericTypeDefinition() == typeof(Func<,>);
            })
            .Single();

        private readonly List<string> usings = new List<string>() { "sysbus." };

        private const string DefaultNamespace = "Antmicro.Renode.Peripherals.";
        private const string SelectCommand = "Select";
        private const string ForEachCommand = "ForEach";

        public enum NumberModes
        {
            Hexadecimal,
            Decimal,
            Both
        }

        private class MachineWithWasPaused
        {
            public Machine Machine { get; set; }

            public bool WasPaused { get; set; }
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
            return properties.DistinctBy(x => x.ToString()); //Look @ GetAvailableMethods for explanation.
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
            return properties.DistinctBy(x => x.ToString()); //Look @ GetAvailableMethods for explanation.
        }

        private class TokenList : IEnumerable<Token>
        {
            public static TokenList Single(Token token)
            {
                var list = new TokenList(false);
                list.Tokens.Add(token);
                return list;
            }

            public TokenList(bool isArray)
            {
                this.IsArray = isArray;
            }

            public override string ToString()
            {
                return IsArray ? Misc.PrettyPrintCollection(Tokens) : Tokens.FirstOrDefault()?.ToString() ?? "null";
            }

            public IEnumerator<Token> GetEnumerator()
            {
                return Tokens.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)Tokens).GetEnumerator();
            }

            public readonly List<Token> Tokens = new List<Token>();
            public readonly bool IsArray;
        }
    }
}