//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract partial class AtCommandModem
    {
        [ArgumentParser]
        protected string ParseString(string str)
        {
            if(str.Length < 2 || str.First() != '"' || str.Last() != '"')
            {
                return str; // unquoted string is allowed
            }
            else
            {
                return str.Substring(1, str.Length - 2);
            }
        }

        // This enum parser is looked up as special case and its signature is incomatible
        // with other argument parsing functions, so it has no [ArgumentParser] annotation
        // We don't use SmartParser here because it's case sensitive and doesn't handle spaces in arguments
        protected object ParseStringEnum(string str, Type enumType)
        {
            var strValue = ParseString(str).Replace(" ", "");
            try
            {
                // We use Parse and catch the exception because TryParse is generic and
                // so doesn't take the enum type as a normal argument.
                return Enum.Parse(enumType, strValue, true);
            }
            catch(Exception e)
            {
                throw new ArgumentOutOfRangeException($"Enum {enumType.FullName} does not contain '{strValue}'", e);
            }
        }

        private object ParseArgument(string argument, Type parameterType)
        {
            // If the type is nullable, get its underlying type
            var underlyingType = Nullable.GetUnderlyingType(parameterType);
            parameterType = underlyingType ?? parameterType;

            if(argument == "")
            {
                return Type.Missing;
            }
            else if(argumentParsers.TryGetValue(parameterType, out var parser))
            {
                return parser(argument);
            }
            else if(parameterType.IsEnum)
            {
                // Try parsing as a string enum, i.e. "TCP" -> Tcp
                try
                {
                    return ParseStringEnum(argument, parameterType);
                }
                catch(ArgumentException)
                {
                    // If this fails, do nothing - number -> enum parsing, i.e. 0 -> Tcp will be attempted by SmartParser
                }
            }

            // Use SmartParser for numeric types and number -> enum parsing
            // Note: this is not a part of the else-if chain above because it also handles string -> enum parsing
            if(SmartParser.Instance.TryParse(argument, parameterType, out var result))
            {
                return result;
            }
            // If we failed to parse this enum as a string above, and SmartParser also failed to parse it
            // as a number, this means the argument itself is invalid.
            if(parameterType.IsEnum)
            {
                throw new ArgumentException($"Enum argument '{argument}' is invalid");
            }

            // If none of the parsers covered this parameter, this is a model implementation error.
            throw new NotSupportedException($"No argument parser found for {parameterType.FullName}");
        }

        private object[] ParseArguments(string argumentsString, IEnumerable<ParameterInfo> parameters)
        {
            var parameterTypes = parameters.Select(p => p.ParameterType);
            Type arrayParameterType = null;
            var lastParameter = parameters.LastOrDefault();
            if(lastParameter?.IsDefined(typeof(ParamArrayAttribute)) ?? false)
            {
                arrayParameterType = lastParameter.ParameterType.GetElementType();
                parameterTypes = parameterTypes.Take(parameterTypes.Count() - 1);
            }

            var arguments = argumentsString.Split(',');
            var parsedArguments = arguments.Zip(parameterTypes, (arg, type) => ParseArgument(arg.Trim(), type));
            if(arrayParameterType != null)
            {
                var arrayElements = arguments
                    .Skip(parsedArguments.Count())
                    .Select(arg => ParseArgument(arg.Trim(), arrayParameterType)).ToArray();

                // We make a new array and copy into it so that we get (for example)
                // int[] or string[] as appropriate. arrayElements is always an object[].
                var arrayArgument = Array.CreateInstance(arrayParameterType, arrayElements.Length);
                Array.Copy(arrayElements, arrayArgument, arrayElements.Length);
                parsedArguments = parsedArguments.Append(new [] { arrayArgument } );
            }
            return parsedArguments.ToArray();
        }

        private Dictionary<Type, Func<string, object>> GetArgumentParsers()
        {
            // We don't inherit the [ArgumentParser] attribute in order to allow "hiding" parsers
            // in subclasses by overriding them and not marking them with [ArgumentParser]
            return this.GetType().GetMethodsWithAttribute<ArgumentParserAttribute>(inheritAttribute: false)
                .Select(ma => ma.Method)
                .ToDictionary<MethodInfo, Type, Func<string, object>>(m => m.ReturnType, m =>
                {
                    // We can't just do m.CreateDelegate(typeof(Func<string, object>)) because
                    // that wouldn't work for parse functions that return value types
                    var delType = typeof(Func<,>).MakeGenericType(typeof(string), m.ReturnType);
                    dynamic del = m.CreateDelegate(delType, this);
                    // This lambda is only to box value types
                    return s => del(s);
                });
        }

        [AttributeUsage(AttributeTargets.Method)]
        protected class ArgumentParserAttribute : Attribute
        {
        }

        private class ParsedCommand
        {
            public ParsedCommand(string command)
            {
                if(!command.StartsWith("AT", true, CultureInfo.InvariantCulture))
                {
                    throw new ArgumentException($"Command '{command}' does not start with AT");
                }

                if(command.EndsWith("=?")) // Test command
                {
                    Command = command.Substring(0, command.Length - 2);
                    Type = CommandType.Test;
                }
                else if(command.EndsWith("?")) // Read command
                {
                    Command = command.Substring(0, command.Length - 1);
                    Type = CommandType.Read;
                }
                else if(command.Contains("=")) // Write command
                {
                    var parts = command.Split(new [] { '=' }, 2);
                    Command = parts[0];
                    Arguments = parts[1];
                    Type = CommandType.Write;
                }
                else // Execution command or basic command
                {
                    // We assume that basic commands can have at most one single-digit argument
                    // (like ATE or ATE0). Basic commands are treated as execution commands.
                    string arguments = "";
                    if(char.IsDigit(command.Last()))
                    {
                        arguments = command.Last().ToString();
                        command = command.Substring(0, command.Length - 1);
                    }
                    Command = command;
                    Arguments = arguments;
                    Type = CommandType.Execution;
                }
            }

            public static bool TryParse(string command, out ParsedCommand parsed)
            {
                try
                {
                    parsed = new ParsedCommand(command);
                    return true;
                }
                catch(Exception)
                {
                    parsed = default(ParsedCommand);
                    return false;
                }
            }

            public string Command
            {
                get => command;
                private set => command = value.ToUpper();
            }
            public CommandType Type { get; }
            public string Arguments { get; } = "";

            private string command;
        }
    }
}
