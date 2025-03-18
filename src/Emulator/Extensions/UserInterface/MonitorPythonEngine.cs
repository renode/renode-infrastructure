//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using AntShell.Commands;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Hosting;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface
{
    public class MonitorPythonEngine : PythonEngine, IDisposable
    {
        private readonly string[] Imports =
        {
            "clr.AddReference('Infrastructure')",
        };

        public MonitorPythonEngine(Monitor monitor)
        {
            this.monitor = monitor;
            var rootPath = Misc.GetRootDirectory();

            var imports = Engine.CreateScriptSourceFromString(Aggregate(Imports));
            imports.Execute(Scope);

            var monitorPath = Path.Combine(rootPath, MonitorPyPath);
            if(File.Exists(monitorPath))
            {
                var script = Engine.CreateScriptSourceFromFile(monitorPath); // standard lib
                script.Compile().Execute(Scope);
                Logging.Logger.Log(Logging.LogLevel.Info, "Loaded monitor commands from: {0}", monitorPath);
            }

            Scope.SetVariable("self", monitor);
            Scope.SetVariable("monitor", monitor);
        }

        public void Dispose()
        {
            if(streamToEventConverter != null && streamToEventConverterForError != null)
            {
                streamToEventConverter.IgnoreWrites = true;
                streamToEventConverterForError.IgnoreWrites = true;
            }
        }

        [PreSerialization]
        protected void BeforeSerialization()
        {
            throw new NotSupportedException("MonitorPythonEngine should not be serialized!");
        }

        public bool ExecuteBuiltinCommand(Token[] command, ICommandInteraction writer)
        {
            var command_name = ((LiteralToken)command[0]).Value;
            if(!Scope.ContainsVariable("mc_" + command_name))
            {
                return false;
            }

            object comm = Scope.GetVariable("mc_" + command_name); // get a method
            var arguments = command.Skip(1);
            var argumentsLength = command.Length - 1;

            var firstEqualIndex = arguments.IndexOf(t => t is EqualityToken);
            var parametersLength = firstEqualIndex == -1 ? argumentsLength : firstEqualIndex - 1;

            if(arguments.Any() && arguments.First() is EqualityToken firstArgument)
            {
                throw new RecoverableException($"Invalid argument {firstArgument} at position 1");
            }

            var parameters = arguments.Take(parametersLength).Select(GetTokenValue).ToArray();
            var keywordArguments = arguments.Skip(parametersLength).Split(3).Select((kwarg, idx) =>
            {
                if(kwarg.Length != 3 || !(kwarg[0] is LiteralToken) || !(kwarg[1] is EqualityToken))
                {
                    var argument = string.Join<object>("", kwarg);
                    throw new RecoverableException($"Invalid keyword argument {argument} at position {parametersLength + 1 + idx}");
                }

                var key = kwarg[0].GetObjectValue();
                var value = GetTokenValue(kwarg[2]);
                return new KeyValuePair<object, object>(key, value);
            }).ToDictionary(kv => kv.Key, kv => kv.Value);

            ConfigureOutput(writer);

            try
            {
                var result = PythonCalls.CallWithKeywordArgs(DefaultContext.Default, comm, parameters, keywordArguments);
                if(result != null && (!(result is bool) || !(bool)result))
                {
                    writer.WriteError(String.Format("Command {0} failed, returning \"{1}\".", command_name, result));
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Command '{command_name} {String.Join(" ", parameters)}' failed", e);
            }
            return true;
        }

        public bool TryExecutePythonScript(ReadFilePath fileName, ICommandInteraction writer)
        {
            var script = Engine.CreateScriptSourceFromFile(fileName);
            ExecutePythonScriptInner(script, writer);
            return true;
        }

        public object ExecutePythonCommand(string command, ICommandInteraction writer)
        {
            try
            {
                var script = Engine.CreateScriptSourceFromString(command);
                return ExecutePythonScriptInner(script, writer);
            }
            catch(Microsoft.Scripting.SyntaxErrorException e)
            {
                throw new RecoverableException(String.Format("Line : {0}\n{1}", e.Line, e.Message));
            }
        }

        private object GetTokenValue(Token token)
        {
            var value = token.GetObjectValue();
            if(token is LiteralToken)
            {
                if(EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementByName(value as string, monitor.Machine, out var emulationElement))
                {
                    return emulationElement;
                }
                throw new RecoverableException($"No such emulation element: {value}");
            }
            return value;
        }

        private object ExecutePythonScriptInner(ScriptSource script, ICommandInteraction writer)
        {
            ConfigureOutput(writer);
            try
            {
                return script.Execute(Scope);
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
        }

        public string[] GetPythonCommands(string prefix = "mc_", bool trimPrefix = true)
        {
            return Scope.GetVariableNames().Where(x => x.StartsWith(prefix ?? string.Empty, StringComparison.Ordinal)).Select(x => x.Substring(trimPrefix ? prefix.Length : 0)).ToArray();
        }

        private void ConfigureOutput(ICommandInteraction writer)
        {
            streamToEventConverter = new StreamToEventConverter();
            streamToEventConverterForError = new StreamToEventConverter();
            var utf8WithoutBom = new UTF8Encoding(false);

            var inputStream = writer.GetRawInputStream();
            if(inputStream != null)
            {
                Engine.Runtime.IO.SetInput(inputStream, utf8WithoutBom);
            }
            Engine.Runtime.IO.SetOutput(streamToEventConverter, utf8WithoutBom);
            Engine.Runtime.IO.SetErrorOutput(streamToEventConverterForError, utf8WithoutBom);
            streamToEventConverter.BytesWritten += bytes => writer.Write(utf8WithoutBom.GetString(bytes).Replace("\n", "\r\n"));
            streamToEventConverterForError.BytesWritten += bytes => writer.WriteError(utf8WithoutBom.GetString(bytes).Replace("\n", "\r\n"));
        }

        private StreamToEventConverter streamToEventConverter;
        private StreamToEventConverter streamToEventConverterForError;
        private readonly Monitor monitor;
        private const string MonitorPyPath = "scripts/monitor.py";
    }
}
