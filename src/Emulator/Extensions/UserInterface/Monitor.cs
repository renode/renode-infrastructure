//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface
{
    public partial class Monitor : ICommandHandler, IHasPreservableState
    {
        public Monitor()
        {
            deviceHandlingHelpers = new DeviceHandlingHelpers(this);
            Commands = new HashSet<Command>(new CommandComparer());
            TypeManager.Instance.AutoLoadedType += InitializeAutoCommand;

            pythonRunner = new MonitorPythonEngine(this);
            Quitted += pythonRunner.Dispose;

            var startingCurrentDirectory = Environment.CurrentDirectory;
            var handleEmulationChange = () =>
            {
                monitorContextOverride.Value = null;

                Token oldOrigin = null;
                monitorContext?.Variables.TryGetValue(MonitorContext.OriginVariable, out oldOrigin);

                EmulationManager.Instance.CurrentEmulation.MachineAdded += RegisterResetCommand;
                EmulationManager.Instance.CurrentEmulation.MachineRemoved += removedMachine =>
                {
                    // Note: A case where removedMachine is both the CurrentMachine and OverridingMachine
                    // is handled by EnterMachineContext.
                    if(removedMachine == monitorContext.CurrentMachine && !monitorContext.IsMachineOverriden)
                    {
                        MachineChanged?.Invoke(null);
                    }
                };

                monitorContext = new MonitorContext(startingCurrentDirectory, oldOrigin);
            };

            EmulationManager.Instance.EmulationChanged += handleEmulationChange;
            handleEmulationChange();

            EmulationManager.PreservableManager.RegisterPreservable(this, livesThroughEmulationChange: true);

            SetBasePath();
            InitCommands();

            JoinEmulation();
        }

        public void SetVariable(string name, Token value) => MonitorContext.SetVariable(name, value);

        public void SetMacro(string name, Token value) => MonitorContext.SetMacro(name, value);

        public void SetAlias(string name, Token value) => MonitorContext.SetAlias(name, value);

        public void Bind(string name, Func<object> objectServer) =>
            MonitorContext.Bind(name, objectServer);

        public bool TryLoadPlatform(string filename, ICommandInteraction writer = null)
        {
            writer = writer ?? Interaction;

            if(Machine == null)
            {
                var newMachine = new Machine();
                EmulationManager.Instance.CurrentEmulation.AddMachine(newMachine);
                Machine = newMachine;
            }
            var path = new PathToken(filename);
            var command = new LiteralToken("LoadPlatformDescription");
            ExecuteDeviceAction("machine", Machine, new Token[] { command, path });
            return true;
        }

        public bool SetPeripheralMacro(IPeripheral peripheral, string macroName, string contents, IMachine machine = null) =>
            MonitorContext.SetPeripheralMacro(peripheral, macroName, contents, machine);

        public bool Parse(string cmd, ICommandInteraction writer = null)
        {
            writer = writer ?? Interaction;

            if(stringEaterMode > 0)
            {
                //For multiline scripts in variables
                if(cmd.Contains(MultiLineTerminator))
                {
                    stringEaterMode += 1;
                    if(stringEaterMode > 2)
                    {
                        MonitorContext.SetVariable(stringEaterVariableName, new StringToken(stringEaterValue), recordingType.Value);
                        stringEaterValue = "";
                        stringEaterMode = 0;
                    }
                    return true;
                }
                if(stringEaterMode > 1)
                {
                    if(stringEaterValue != "")
                    {
                        stringEaterValue = stringEaterValue + "\n";
                    }
                    stringEaterValue = stringEaterValue + cmd;
                    return true;
                }

                MonitorContext.SetVariable(stringEaterVariableName, new StringToken(cmd), recordingType.Value);
                stringEaterValue = "";
                stringEaterMode = 0;
                return true;
            }

            if(string.IsNullOrWhiteSpace(cmd))
            {
                return true;
            }
            var tokens = Tokenize(cmd, writer);
            if(tokens == null)
            {
                return false;
            }
            int groupNumber = 0;
            foreach(var singleCommand in tokens.Tokens
                    .GroupBy(x => { if(x is CommandSplit) groupNumber++; return groupNumber; })
                    .Select(x => x.Where(y => !(y is CommandSplit)))
                    .Where(x => x.Any()))
            {
                if(!ParseTokens(singleCommand, writer))
                    return false;
            }
            return true;
        }

        public bool ParseTokens(IEnumerable<Token> tokensToParse, ICommandInteraction writer)
        {
            var reParse = false;
            var result = new List<Token>();
            var tokens = tokensToParse.ToList();
            foreach(var token in tokens)
            {
                Token resultToken = token;
                if(token is CommentToken)
                {
                    continue;
                }
                if(token is ExecutionToken)
                {
                    resultToken = ExecuteWithResult((string)token.GetObjectValue(), writer);
                    if(resultToken == null)
                    {
                        return false; //something went wrong with the inner command
                    }
                    reParse = true;
                }

                var pathToken = token as PathToken;
                if(pathToken != null)
                {
                    string fileName;
                    if(PathHelpers.TryGetFilenameFromAvailablePaths(pathToken.Value, MonitorContext.MonitorPath.PathElements, out fileName))
                    {
                        resultToken = new PathToken("@" + fileName);
                    }
                    else
                    {
                        Uri uri;
                        string filename;
                        string fname = pathToken.Value;
                        try
                        {
                            uri = new Uri(fname);
                            if(uri.IsFile)
                            {
                                throw new UriFormatException();
                            }
                            var success = Emulation.FileFetcher.TryFetchFromUri(uri, out filename);
                            if(!success)
                            {
                                writer.WriteError("Failed to download {0}, see log for details.".FormatWith(fname));
                                filename = null;
                                if(breakOnException)
                                {
                                    return false;
                                }
                            }
                            resultToken = new PathToken("@" + filename);
                        }
                        catch(UriFormatException)
                        {
                            //Not a proper uri, so probably a nonexisting local path
                        }
                    }
                }

                result.Add(resultToken);
            }
            if(!result.Any())
            {
                return true;
            }
            if(reParse)
            {
                return Parse(String.Join(" ", result.Select(x => x.OriginalValue)), writer);
            }
            try
            {
                if(!ExecuteCommand(result.ToArray(), writer) && breakOnException)
                {
                    return false;
                }
            }
            catch(Exception e) when(swallowExceptions && e.IsExceptionRecoverable(allowAggregate: true))
            {
                var contextMessage = String.Join(" ", result.Select(x => x.OriginalValue));

                foreach(var ex in (IEnumerable<Exception>)(e as AggregateException)?.InnerExceptions ?? new[] { e })
                {
                    PrintException(contextMessage, ex, writer);
                }

                return !breakOnException;
            }
            return true;
        }

        public bool TryExecuteScript(string filename, ICommandInteraction writer = null, IMachine machine = null)
        {
            writer = writer ?? Interaction;

            Token oldOrigin;
            var originalFilename = filename;
            if(!PathHelpers.TryGetFilenameFromAvailablePaths(filename, MonitorContext.MonitorPath.PathElements, out filename))
            {
                writer.WriteError($"Could not find file '{originalFilename}'");
                return false;
            }
            MonitorContext.Variables.TryGetValue(MonitorContext.OriginVariable, out oldOrigin);
            SetVariable(MonitorContext.OriginVariable, new PathToken("@" + Path.GetDirectoryName(filename).Replace(" ", @"\ ")));
            var lines = File.ReadAllLines(filename);
            Array.ForEach(lines, x => x.Replace("\r", "\n"));
            var processedLines = new List<string>(lines.Length);
            var builder = new StringBuilder();
            var currentlyEating = false;

            foreach(var line in lines)
            {
                var hasTerminator = line.Contains(MultiLineTerminator);
                if(!currentlyEating && !hasTerminator)
                {
                    processedLines.Add(line);
                }
                if(hasTerminator)
                {
                    //concatenate with the previous line
                    if(!currentlyEating && line.StartsWith(MultiLineTerminator, StringComparison.Ordinal))
                    {
                        builder.AppendLine(processedLines.Last());
                        processedLines.RemoveAt(processedLines.Count - 1);
                    }
                    builder.AppendLine(line);
                    if(currentlyEating)
                    {
                        processedLines.Add(builder.ToString());
                        builder.Clear();
                    }
                    currentlyEating = !currentlyEating;
                }
                else if(currentlyEating)
                {
                    builder.AppendLine(line);
                }
            }

            var success = true;

            var parseFile = () =>
            {
                foreach(var ln in processedLines)
                {
                    if(!Parse(ln))
                    {
                        success = false;
                        break;
                    }
                }
            };

            if(machine != null)
            {
                using(EnterMachineContext(machine))
                {
                    parseFile();
                }
            }
            else
            {
                parseFile();
            }

            if(oldOrigin != null)
            {
                SetVariable(MonitorContext.OriginVariable, oldOrigin);
            }
            return success;
        }

        public object ExecutePythonCommand(string command)
        {
            return pythonRunner.ExecutePythonCommand(command, Interaction);
        }

        public object GetVariable(string name) =>
            MonitorContext.GetVariable(name);

        public void UnregisterCommand(Command command)
        {
            if(!Commands.Contains(command))
            {
                Logger.LogAs(this, LogLevel.Warning, "Command {0} not registered.", command.Name);
                return;
            }
            Commands.Remove(command);
        }

        public bool TryCompilePlugin(string[] filenames, ICommandInteraction writer = null)
        {
            writer = writer ?? Interaction;

            // Sort filenames to have consistent order when calculating hash
            Array.Sort(filenames);

            string sha;
            using(var shaComputer = SHA256.Create())
            {
                var buffer = new List<byte[]>();
                foreach(string filename in filenames)
                {
                    buffer.Add(File.ReadAllBytes(filename));
                }

                var bytesSha = shaComputer.ComputeHash(buffer.SelectMany(x => x).ToArray());

                var strBldr = new StringBuilder(32 * 2);
                foreach(var b in bytesSha)
                {
                    strBldr.AppendFormat("{0:X2}", b);
                }
                sha = strBldr.ToString();

                if(scannedFilesCache.Contains(sha))
                {
                    var nameOrNames = filenames.Length == 1 ? filenames.Single() : $"s {Misc.PrettyPrintCollection(filenames)}";
                    writer.WriteLine($"Code from file{nameOrNames} has already been compiled. Ignoring...");
                    return true;
                }
            }

            try
            {
                if(!EmulationManager.Instance.CompiledFilesCache.TryGetEntryWithSha(sha, out var compiledCode))
                {
                    var compiler = new AdHocCompiler();
                    compiledCode = compiler.Compile(filenames);
                    // Load dynamically compiled assembly to memory. It presents an advantage that next
                    // ad-hoc compiled assembly can reference types from this one without any extra steps.
                    // Therefore "EnsureTypeIsLoaded" call is no necessary as dependencies are already loaded.
                    // XXX: Consider AssemblyLoadContext.LoadFromAssemblyPath.
                    Assembly.LoadFrom(compiledCode);
                    EmulationManager.Instance.CompiledFilesCache.StoreEntryWithSha(sha, compiledCode);
                }

                cache.ClearCache();
                var result = TypeManager.Instance.ScanFile(compiledCode);
                if(result)
                {
                    scannedFilesCache.Add(sha);
                }
                return result;
            }
            catch(Exception e)
                when(e is RecoverableException
                  || e is InvalidOperationException)
            {
                writer.WriteError("Errors during compilation or loading:\r\n" + e.Message.Replace(Environment.NewLine, "\r\n"));
                return false;
            }
        }

        public void BindStatic(string name, Func<object> objectServer) =>
            MonitorContext.BindStatic(name, objectServer);

        public void RegisterCommand(Command command)
        {
            if(Commands.Contains(command))
            {
                Logger.LogAs(this, LogLevel.Warning, "Command {0} already registered.", command.Name);
                return;
            }
            Commands.Add(command);
        }

        public ICommandInteraction HandleCommand(string cmd, ICommandInteraction ci)
        {
            Parse(cmd, ci);
            return ci;
        }

        public string[] SuggestionNeeded(string cmd)
        {
            return SuggestCommands(cmd).ToArray();
        }

        public object ExtractPreservedState()
        {
            return Machine?.ToString();
        }

        public void LoadPreservedState(object state)
        {
            if(state == null)
            {
                return;
            }

            if(!(state is string machineName))
            {
                throw new RecoverableException("Unexpected state received while loading preserved state");
            }

            if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(machineName, out var newMachine))
            {
                throw new RecoverableException("Machine was not found in the snapshot");
            }

            Machine = newMachine;
        }

        public IDisposable PushDirectory(string directory) =>
            MonitorContext.PushDirectory(directory);

        public IDisposable EnterContext(MonitorContext newContext)
        {
            var oldContext = monitorContextOverride.Value;
            monitorContextOverride.Value = newContext;
            return DisposableWrapper.New(() => monitorContextOverride.Value = oldContext);
        }

        public IEnumerable<Command> RegisteredCommands => Commands;

        public ICommandInteraction Interaction { get; set; }

        public IEnumerable<string> CurrentPathPrefixes => MonitorContext.MonitorPath.PathElements;

        public Func<IEnumerable<ICommandDescription>> GetInternalCommands { get; set; }

        public IMachine Machine
        {
            get => MonitorContext.CurrentMachine;

            set
            {
                var changingOriginalMachine = monitorContextOverride.Value == null && !MonitorContext.IsMachineOverriden;
                var previousMachine = monitorContext.CurrentMachine;

                MonitorContext.CurrentMachine = value;
                if(changingOriginalMachine && previousMachine != value)
                {
                    MachineChanged?.Invoke(MonitorContext.MachineName);
                }
            }
        }

        public MonitorContext MonitorContext
        {
            get => monitorContextOverride.Value ?? monitorContext;
            set
            {
                if(monitorContextOverride.Value != null)
                {
                    monitorContextOverride.Value = value;
                }
                else
                {
                    if(monitorContext.CurrentMachine != value.CurrentMachine)
                    {
                        MachineChanged?.Invoke(value.MachineName);
                    }

                    monitorContext = value;
                }
            }
        }

        public DeviceHandlingHelpers DeviceHelpers => deviceHandlingHelpers;

        public string PreservableName => "Monitor";

        public event Action<string> MachineChanged;

        public const string StartupCommandEnv = "STARTUP_COMMAND";
        public const string ConfigurationSection = "monitor";

        private static void SetBasePath()
        {
            if(!Misc.TryGetRootDirectory(out var baseDirectory))
            {
                Logger.Log(LogLevel.Warning, "Monitor: could not find root base path, using current instead.");
                return;
            }
            Directory.SetCurrentDirectory(baseDirectory);
        }

        private static string FindLastCommandInString(string origin)
        {
            bool inApostrophes = false;
            int position = 0;
            for(int i = 0; i < origin.Length; ++i)
            {
                switch(origin[i])
                {
                case '"':
                    inApostrophes = !inApostrophes;
                    break;
                case ';':
                    if(!inApostrophes)
                    {
                        position = i + 1;
                    }
                    break;
                }
            }
            return origin.Substring(position).TrimStart();
        }

        private static IEnumerable<String> SuggestFiles(String allButLast, String prefix, String directory, String lastElement)
        {
            //the sanitization of the first "./" is required to preserve the original input provided by the user
            var directoryPath = Path.Combine(prefix, directory);
            try
            {
                if(!Directory.Exists(directoryPath))
                {
                    return Enumerable.Empty<string>();
                }
                var files = Directory.GetFiles(directoryPath, lastElement + '*', SearchOption.TopDirectoryOnly)
                                     .Select(x => allButLast + "@" + PathHelpers.StripPathPrefix(x, prefix).Replace(" ", @"\ "));
                var dirs = Directory.GetDirectories(directoryPath, lastElement + '*', SearchOption.TopDirectoryOnly)
                                    .Select(x => allButLast + "@" + (PathHelpers.StripPathPrefix(x, prefix) + '/').Replace(" ", @"\ "));

                var result = new List<string>();
                //We change "\" characters to "/", unless they were followed by the space character, in which case they treated as escape char.
                foreach(var file in files.Concat(dirs))
                {
                    var sanitizedFile = PathHelpers.SanitizePathSeparator(file);
                    result.Add(sanitizedFile);
                }
                return result;
            }
            catch(UnauthorizedAccessException)
            {
                return new[] { "{0}@{1}/".FormatWith(allButLast, Path.Combine(PathHelpers.StripPathPrefix(directoryPath, prefix), lastElement)) };
            }
        }

        private static readonly bool swallowExceptions = ConfigurationManager.Instance.Get(ConfigurationSection, "consume-exceptions-from-command", true);
        private static readonly bool breakOnException = ConfigurationManager.Instance.Get(ConfigurationSection, "break-script-on-exception", true);

        private bool ExecuteCommand(Token[] com, ICommandInteraction writer)
        {
            if(MonitorContext.VerboseMode)
            {
                writer.WriteLine("Executing: " + com.Select(x => x.OriginalValue).Aggregate((x, y) => x + " " + y));
            }
            if(!com.Any())
            {
                return true;
            }

            //variable definition
            if(com.Length == 3 && com[0] is VariableToken && com[1] is EqualityToken)
            {
                Token dummy;
                var variableToExpand = com[0] as VariableToken;
                if(com[1] is ConditionalEqualityToken && MonitorContext.TryExpandVariable(variableToExpand, MonitorContext.Variables, out dummy))
                {
                    //variable exists, so we ignore this command
                    return true;
                }
                (Commands.OfType<SetCommand>().First()).Run(writer, variableToExpand, com[2]);
                return true;
            }
            var command = com[0] as LiteralToken;

            if(command == null)
            {
                writer.WriteError(string.Format("No such command or device: {0}", com[0].OriginalValue));
                return false;
            }

            var commandHandler = Commands.FirstOrDefault(x => x.Name == command.Value);
            if(commandHandler != null)
            {
                return RunCommand(writer, commandHandler, com.Skip(1).ToList());
            }
            else if(MonitorContext.IsNameAvailable(command.Value))
            {
                ProcessDeviceActionByName(command.Value, MonitorContext.ExpandVariables(com.Skip(1)), writer);
            }
            else if(IsNameAvailableInEmulationManager(command.Value))
            {
                ProcessDeviceAction(typeof(EmulationManager), typeof(EmulationManager).Name, com, writer);
            }
            else
            {
                foreach(var item in Commands)
                {
                    if(item.AlternativeNames != null && item.AlternativeNames.Contains(command.Value))
                    {
                        return RunCommand(writer, item, com.Skip(1).ToList());
                    }
                }

                if(MonitorContext.TryExpandVariable(new VariableToken(string.Format("${0}", com[0].OriginalValue)), MonitorContext.Aliases, out var cmd))
                {
                    var aliasedCommand = Tokenize(cmd.GetObjectValue().ToString(), writer).Tokens;
                    return ParseTokens(aliasedCommand.Concat(com.Skip(1)), writer);
                }

                if(!pythonRunner.ExecuteBuiltinCommand(MonitorContext.ExpandVariables(com).ToArray(), writer))
                {
                    writer.WriteError(string.Format("No such command or device: {0}", com[0].GetObjectValue()));
                    return false;
                }
            }
            return true;
        }

        private bool IsNameAvailableInEmulationManager(string name)
        {
            var info = GetMonitorInfo(typeof(EmulationManager));
            return info.AllNames.Contains(name);
        }

        private IEnumerable<String> SuggestCommands(String prefix)
        {
            var currentCommand = FindLastCommandInString(prefix);
            var suggestions = new List<String>();
            var prefixSplit = Regex.Matches(prefix, @"(((\\ )|\S))+").Cast<Match>().Select(x => x.Value).ToArray();
            var prefixToAdd = prefix.EndsWith(currentCommand, StringComparison.Ordinal) ? prefix.Substring(0, prefix.Length - currentCommand.Length) : String.Empty;
            var lastElement = String.Empty;

            if(prefixSplit.Length > 0)
            {
                lastElement = prefixSplit.Last();
            }

            var optionalInput = prefix.EndsWith(' ') ? prefixSplit : prefixSplit.SkipLast(1);
            var allButLastOptional = string.Join(' ', optionalInput);
            if(!string.IsNullOrEmpty(allButLastOptional))
            {
                allButLastOptional += ' ';
            }

            var allButLast = string.Join(' ', prefixSplit.SkipLast(1));
            if(!string.IsNullOrEmpty(allButLast))
            {
                allButLast += ' ';
            }

            //paths
            if(lastElement.StartsWith('@'))
            {
                lastElement = Regex.Replace(lastElement.Substring(1), @"\\([^\\])", "$1");
                var directory = String.Empty;
                var file = String.Empty;
                if(!String.IsNullOrWhiteSpace(lastElement))
                {
                    //these functions will fail on empty input
                    directory = Path.GetDirectoryName(lastElement) ?? lastElement;
                    file = Path.GetFileName(lastElement);
                }
                var rootIndicator = RuntimeInfo.IsWindows() ? "^[a-zA-Z]:/" : "^/";

                if(Regex.Match(lastElement, rootIndicator).Success)
                {
                    try
                    {
                        suggestions.AddRange(SuggestFiles(allButLast, String.Empty, directory, file)); //we need to filter out "/", because Path.GetDirectory returns null for "/"
                    }
                    catch(DirectoryNotFoundException) { }
                }
                else
                {
                    foreach(var pathEntry in MonitorContext.MonitorPath.PathElements.Select(x => Path.GetFullPath(x)))
                    {
                        if(!Directory.Exists(pathEntry))
                        {
                            continue;
                        }
                        suggestions.AddRange(SuggestFiles(allButLast, pathEntry, directory, file));
                    }
                }
            }
            //variables
            else if(lastElement.StartsWith('$'))
            {
                var options = MonitorContext.FindMatchingVariables(lastElement);
                if(options.Any())
                {
                    suggestions.AddRange(options.Select(x => allButLast + '$' + x));
                }
            }
            var currentCommandSplit = currentCommand.Split(' ');

            if(currentCommand.Contains(' '))
            {
                var cmd = Commands.SingleOrDefault(c => c.Name == currentCommandSplit[0] || c.AlternativeNames.Contains(currentCommandSplit[0])) as ISuggestionProvider;
                if(cmd != null)
                {
                    var sugs = cmd.ProvideSuggestions(currentCommandSplit.Length > 1 ? currentCommandSplit[1] : string.Empty);
                    suggestions.AddRange(sugs.Select(s => string.Format("{0}{1}", allButLastOptional, s)));
                }
                else if(currentCommandSplit.Length > 1 && MonitorContext.GetAllAvailableNames().Contains(currentCommandSplit[0]))
                {
                    var currentObject = MonitorContext.GetDevice(currentCommandSplit[0]);
                    //Take whole command split without first and last element
                    var commandsChain = currentCommandSplit.Skip(1).Take(currentCommandSplit.Length - 2).Select((word, index) => new { word, index }).ToList();
                    foreach(var command in commandsChain)
                    {
                        //If we're accessing a list, and there are no extra words yet, show suggestions for its elements (if any)
                        if(command.index == commandsChain.Count - 1 &&
                            (command.word == SelectCommand || command.word == ForEachCommand) && currentObject is IEnumerable enumerable)
                        {
                            currentObject = enumerable.Cast<object>().FirstOrDefault();
                            break;
                        }
                        //It is assumed that commands chain can contain only properties or fields
                        var newObject = FindFieldOrProperty(currentObject, command.word);
                        if(newObject == null)
                        {
                            currentObject = null;
                            break;
                        }
                        currentObject = newObject;
                    }

                    if(currentObject != null)
                    {
                        var devInfo = GetObjectSuggestions(currentObject).Distinct();
                        suggestions.AddRange(devInfo.Where(x => x.StartsWith(currentCommandSplit[currentCommandSplit.Length - 1], StringComparison.OrdinalIgnoreCase))
                            .Select(x => allButLastOptional + x));
                    }
                }
            }
            else
            {
                var sugg = Commands.Select(x => x.Name).ToList();

                sugg.AddRange(MonitorContext.GetAllAvailableNames());
                sugg.AddRange(pythonRunner.GetPythonCommands());
                sugg.AddRange(MonitorContext.Aliases.Keys);
                sugg.AddRange(MonitorContext.Aliases.Keys.Select(x => x.Substring(x.IndexOf('.') + 1))); // remove the "global." or "{machine-name}." prefix
                suggestions.AddRange(sugg.Where(x => x.StartsWith(currentCommandSplit[0])).Select(x => prefixToAdd + x));

                if(suggestions.Count == 0) //EmulationManager
                {
                    var dev = MonitorContext.GetDevice(typeof(EmulationManager).Name);
                    var devInfo = GetObjectSuggestions(dev).Distinct();
                    if(devInfo != null)
                    {
                        suggestions.AddRange(devInfo.Where(x => x.StartsWith(currentCommandSplit[0], StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            return suggestions.OrderBy(x => x).Distinct();
        }

        private void PrintException(string commandName, Exception e, ICommandInteraction writer)
        {
            writer.WriteError(string.Format("There was an error executing command '{0}': ", commandName));
            PrintExceptionDetails(e, writer);
        }

        private void RegisterResetCommand(IMachine machine)
        {
            machine.MachineReset += ResetMachine;
            machine.PeripheralReset += ResetPeripheral;
        }

        private void JoinEmulation()
        {
            Emulation.MachineExchanged += (oldMachine, newMachine) =>
            {
                if(Machine == oldMachine)
                {
                    Machine = newMachine;
                }
            };
        }

        private void InitCommands()
        {
            var includeCommand = new IncludeFileCommand(this, (x, y) => pythonRunner.TryExecutePythonScript(x, y), x => TryExecuteScript(x), (x, y) => TryCompilePlugin(x, y), (x, y) => TryLoadPlatform(x, y));
            Commands.Add(new HelpCommand(this, () =>
            {
                var gic = GetInternalCommands;
                var result = Commands.Cast<ICommandDescription>();
                if(gic != null)
                {
                    result = result.Concat(gic());
                }
                return result;
            }));
            Commands.Add(includeCommand);
            Commands.Add(new CreatePlatformCommand(this, x => Machine = x));
            Commands.Add(new UsingCommand(this, () => MonitorContext.Usings));
            Commands.Add(new QuitCommand(this, x => Machine = x, () => Quitted));
            Commands.Add(new PeripheralsCommand(this, () => Machine));
            Commands.Add(new TagsCommand(this, () => Machine));
            Commands.Add(new MonitorPathCommand(this, () => MonitorContext.MonitorPath));
            Commands.Add(new StartCommand(this, includeCommand));
            Commands.Add(new SetCommand(this, "set", "VARIABLE", (x, y) => SetVariable(x, y), (x, y) => EnableStringEater(x, y, VariableType.Variable),
                DisableStringEater, () => stringEaterMode, name => MonitorContext.GetVariableName(name)));
            Commands.Add(new SetCommand(this, "macro", "MACRO", (x, y) => SetMacro(x, y), (x, y) => EnableStringEater(x, y, VariableType.Macro),
                DisableStringEater, () => stringEaterMode, name => MonitorContext.GetVariableName(name)));
            Commands.Add(new SetCommand(this, "alias", "ALIAS", (x, y) => SetAlias(x, y), (x, y) => EnableStringEater(x, y, VariableType.Alias),
                DisableStringEater, () => stringEaterMode, name => MonitorContext.GetVariableName(name)));
            Commands.Add(new PythonExecuteCommand(this, x => MonitorContext.ExpandVariable(x, MonitorContext.Variables), (x, y) => pythonRunner.ExecutePythonCommand(x, y)));
            Commands.Add(new ExecuteCommand(this, "execute", "VARIABLE", x => MonitorContext.ExpandVariable(x, MonitorContext.Variables), () => MonitorContext.Variables.Keys));
            Commands.Add(new ExecuteCommand(this, "runMacro", "MACRO", x => MonitorContext.ExpandVariable(x, MonitorContext.Macros), () => MonitorContext.Macros.Keys));
            Commands.Add(new MachCommand(this, () => Machine, x => Machine = x));
            Commands.Add(new ResdCommand(this));
            Commands.Add(new VerboseCommand(this, x => MonitorContext.VerboseMode = x));
            Commands.Add(new SetAndRevertAfterCommand(this, new DeviceHandlingHelpers(this)));
        }

        private void DisableStringEater()
        {
            stringEaterMode = 0;
            stringEaterValue = null;
            stringEaterVariableName = null;
            recordingType = null;
        }

        private void EnableStringEater(string variable, int mode, VariableType type)
        {
            recordingType = type;
            stringEaterMode = mode;
            stringEaterVariableName = variable;
        }

        private void ResetMachine(IMachine machine)
        {
            string machineName;
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(machine, out machineName))
            {
                using(EnterMachineContext(machine))
                {
                    var macroName = MonitorContext.GetVariableName("reset");
                    Token resetMacro;
                    if(MonitorContext.Macros.TryGetValue(macroName, out resetMacro))
                    {
                        Logger.LogAs(this, LogLevel.Warning, "Found it!");
                        var macroLines = resetMacro.GetObjectValue().ToString().Split('\n');
                        foreach(var line in macroLines)
                        {
                            Parse(line, Interaction);
                        }
                    }
                    else
                    {
                        Logger.LogAs(this, LogLevel.Warning, "No action for reset - macro {0} is not registered.", macroName);
                    }
                }
            }
        }

        private void InitializeAutoCommand(Type type)
        {
            if(type.IsSubclassOf(typeof(AutoLoadCommand)))
            {
                var constructor = type.GetConstructor(new[] { typeof(Monitor) })
                                  ?? type.GetConstructors().FirstOrDefault(x =>
                {
                    var constructorParams = x.GetParameters();
                    if(constructorParams.Length == 0)
                    {
                        return false;
                    }
                    return constructorParams[0].ParameterType == typeof(Monitor) && constructorParams.Skip(1).All(y => y.IsOptional);
                });
                if(constructor == null)
                {
                    Logger.LogAs(this, LogLevel.Error, "Could not initialize command {0}.", type.Name);
                    return;
                }
                var parameters = new List<object> { this };
                parameters.AddRange(constructor.GetParameters().Skip(1).Select(x => x.DefaultValue));
                var commandInstance = (AutoLoadCommand)constructor.Invoke(parameters.ToArray());
                RegisterCommand(commandInstance);
            }
        }

        private TokenizationResult Tokenize(string cmd, ICommandInteraction writer)
        {
            var result = tokenizer.Tokenize(cmd);
            if(result.UnmatchedCharactersLeft != 0)
            {
                //Reevaluate the expression if the tokenization failed, but expanding the variables may help.
                //E.g. i $ORIGIN/dir/script. This happens only if the variable is the last successful token.
                if(result.Tokens.Any() && result.Tokens.Last() is VariableToken lastVariableToken)
                {
                    if(!MonitorContext.TryExpandVariable(lastVariableToken, MonitorContext.Variables, out var lastExpandedToken))
                    {
                        writer.WriteError($"No such variable: ${lastVariableToken.Value}");
                        return null;
                    }
                    // replace the last token with the expanded version
                    var newString = String.Concat(
                        result.Tokens.Take(result.Tokens.Count() - 1).Select(x => x.OriginalValue).Stringify(),
                        " ",
                        lastExpandedToken.OriginalValue,
                        cmd.Substring(cmd.Length - result.UnmatchedCharactersLeft)
                    );
                    return Tokenize(newString, writer);
                }
                var messages = new StringBuilder();

                var message = "Could not tokenize here:";
                writer.WriteError(message);
                messages.AppendFormat("Monitor: {0}\n", message);

                writer.WriteError(cmd);
                messages.AppendLine(cmd);

                var matchedLength = cmd.Length - result.UnmatchedCharactersLeft;
                var padded = "^".PadLeft(matchedLength + 1);
                writer.WriteError(padded);
                messages.AppendLine(padded);
                if(result.Exception != null)
                {
                    messages.AppendFormat("Encountered exception: {0}\n", result.Exception.Message);
                    writer.WriteError(result.Exception.Message);
                }
                Logger.Log(LogLevel.Warning, messages.ToString());
                return null;
            }
            return result;
        }

        private Token ExecuteWithResult(String value, ICommandInteraction writer)
        {
            var eater = new CommandInteractionEater();
            if(Parse(value, eater))
            {
                return new StringToken(eater.GetContents());
            }
            else
            {
                writer.WriteError(eater.GetError());
                return null;
            }
        }

        private IDisposable EnterMachineContext(IMachine machine)
        {
            var machineContext = MonitorContext.EnterMachineContext(machine);
            return DisposableWrapper.New(() =>
            {
                machineContext.Dispose();

                // When overriding Machine it is impossible to verify at the time whether
                // the monitor prompt should be changed as well.
                var revertingOriginalMachine = monitorContextOverride.Value == null && !MonitorContext.IsMachineOverriden;

                // It is possible thath `MachineRemove` event caused the Machine to be null, the prompt should be updated.
                var possibleMissedMachineRemoval = revertingOriginalMachine && Machine == null;
                if(possibleMissedMachineRemoval)
                {
                    MachineChanged?.Invoke(null);
                }
            });
        }

        private void PrintExceptionDetails(Exception e, ICommandInteraction writer, int tab = 0)
        {
            if(!(e is TargetInvocationException) && !String.IsNullOrWhiteSpace(e.Message))
            {
                writer.WriteError(e.Message.Replace("\n", "\r\n").Indent(tab, '\t'));
            }
            else
            {
                tab--; //if no message is printed out, we do not need an indentation.
            }
            var aggregateException = e as AggregateException;
            if(aggregateException != null)
            {
                foreach(var exception in aggregateException.InnerExceptions)
                {
                    PrintExceptionDetails(exception, writer, tab + 1);
                }
            }
            if(e.InnerException != null)
            {
                PrintExceptionDetails(e.InnerException, writer, tab + 1);
            }
        }

        private void ResetPeripheral(IMachine machine, IPeripheral peripheral)
        {
            string macroName;
            if(machine.TryGetLocalName(peripheral, out var localName))
            {
                macroName = $"{localName}.reset";
            }
            else
            {
                return;
            }

            using(EnterMachineContext(machine))
            {
                if(MonitorContext.TryExpandVariable(new VariableToken(macroName), MonitorContext.Macros, out var resetMacro))
                {
                    var macroLines = resetMacro.GetObjectValue().ToString().Split('\n');
                    foreach(var line in macroLines)
                    {
                        Parse(line, Interaction);
                    }
                }
            }
        }

        private HashSet<Command> Commands { get; set; }

        private Emulation Emulation
        {
            get
            {
                return EmulationManager.Instance.CurrentEmulation;
            }
        }

        private string stringEaterVariableName = "";
        private Monitor.VariableType? recordingType;
        private string stringEaterValue = "";
        private int stringEaterMode = 0;

        private MonitorContext monitorContext;
        private readonly ThreadLocal<MonitorContext> monitorContextOverride = new ThreadLocal<MonitorContext>();

        private readonly MonitorPythonEngine pythonRunner;

        private readonly Tokenizer.Tokenizer tokenizer = Tokenizer.Tokenizer.CreateTokenizer();

        private readonly List<string> scannedFilesCache = new List<string>();

        private readonly DeviceHandlingHelpers deviceHandlingHelpers;

        private const string EmulationToken = "emulation";

        private const string MultiLineTerminator = @"""""""";

        public class DeviceHandlingHelpers
        {
            public DeviceHandlingHelpers(Monitor monitor)
            {
                this.monitor = monitor;
            }

            public bool IsNameAvailable(string name)
                => monitor.MonitorContext.IsNameAvailable(name);

            public object IdentifyDevice(string name)
                => monitor.MonitorContext.IdentifyDevice(name);

            public object HandleDeviceChain(string name, out string chainedName, object device, IEnumerable<Token> tokens, out IEnumerable<Token> tail)
                => monitor.HandleDeviceChain(name, out chainedName, device, tokens, out tail);

            public bool ParseArgument(IList<Token> tokens, ref int i, out TokenList arg)
                => Monitor.ParseArgument(tokens, ref i, out arg);

            public bool FitArgumentType(TokenList tokens, Type paramType, out object result)
                => monitor.FitArgumentType(tokens, paramType, out result);

            public MemberInfo GetAccessor(object device, string member, bool? assertSetter = null, bool? assertGetter = null)
                => monitor.GetAccessor(device, member, assertSetter, assertGetter);

            private readonly Monitor monitor;
        }

        public enum VariableType
        {
            Variable,
            Macro,
            Alias
        }
    }
}
