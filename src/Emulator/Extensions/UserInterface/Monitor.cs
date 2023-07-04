//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Security.Cryptography;
using AntShell;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.UserInterface.Commands;

namespace Antmicro.Renode.UserInterface
{
    public partial class Monitor : ICommandHandler
    {
        public ICommandInteraction HandleCommand(string cmd, ICommandInteraction ci)
        {
            Parse(cmd, ci);
            return ci;
        }

        public string[] SuggestionNeeded(string cmd)
        {
            return SuggestCommands(cmd).ToArray();
        }

        public Func<IEnumerable<ICommandDescription>> GetInternalCommands { get; set; }

        public Machine Machine
        {
            get
            {
                return currentMachine;
            }
            set
            {
                currentMachine = value;
            }
        }

        private Emulation Emulation
        {
            get
            {
                return emulationManager.CurrentEmulation;
            }
        }

        public IEnumerable<string> CurrentPathPrefixes
        {
            get
            {
                return monitorPath.PathElements;
            }
        }

        private readonly EmulationManager emulationManager;
        private MonitorPath monitorPath = new MonitorPath(Environment.CurrentDirectory);
        public const string StartupCommandEnv = "STARTUP_COMMAND";
        private bool swallowExceptions;
        private bool breakOnException;

        public Monitor()
        {
            swallowExceptions = ConfigurationManager.Instance.Get(ConfigurationSection, "consume-exceptions-from-command", true);
            breakOnException = ConfigurationManager.Instance.Get(ConfigurationSection, "break-script-on-exception", true);

            CurrentBindingFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;
            Commands = new HashSet<Command>(new CommandComparer());
            TypeManager.Instance.AutoLoadedType += InitializeAutoCommand;

            this.emulationManager = EmulationManager.Instance;

            pythonRunner = new MonitorPythonEngine(this);
            Quitted += pythonRunner.Dispose;
            var startingCurrentDirectory = Environment.CurrentDirectory;
            variableCollections = new Dictionary<VariableType, Dictionary<string, Token>>
            {
                { VariableType.Variable, variables },
                { VariableType.Macro, macros },
                { VariableType.Alias, aliases },
            };
            SetBasePath();
            InitCommands();
            emulationManager.CurrentEmulation.MachineAdded += RegisterResetCommand;
            emulationManager.CurrentEmulation.MachineRemoved += UpdateMonitorPrompt;
            emulationManager.EmulationChanged += () =>
            {
                Token oldOrigin;
                variables.TryGetValue(OriginVariable, out oldOrigin);

                variables.Clear();
                SetVariable(CurrentDirectoryVariable, new PathToken("@" + startingCurrentDirectory), variables);
                if(oldOrigin != null)
                {
                    SetVariable(OriginVariable, oldOrigin, variables);
                }
                macros.Clear();
                aliases.Clear();
                Machine = null;
                emulationManager.CurrentEmulation.MachineAdded += RegisterResetCommand;
                monitorPath.Reset();
            };

            SetVariable(CurrentDirectoryVariable, new PathToken("@" + startingCurrentDirectory), variables);
            CurrentNumberFormat = ConfigurationManager.Instance.Get<NumberModes>(ConfigurationSection, "number-format", NumberModes.Hexadecimal);

            JoinEmulation();
        }

        private void RegisterResetCommand(Machine machine)
        {
            machine.MachineReset += ResetMachine;
        }

        private void UpdateMonitorPrompt(Machine machine)
        {
            if(currentMachine == machine)
            {
                currentMachine = null;
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

        private void JoinEmulation()
        {
            Emulation.MachineExchanged += (oldMachine, newMachine) =>
            {
                if(currentMachine == oldMachine)
                {
                    currentMachine = newMachine;
                }
            };
        }

        private static void SetBasePath()
        {
            if(!Misc.TryGetRootDirectory(out var baseDirectory))
            {
                Logger.Log(LogLevel.Warning, "Monitor: could not find root base path, using current instead.");
                return;
            }
            Directory.SetCurrentDirectory(baseDirectory);
        }

        public void RegisterCommand(Command command)
        {
            if(Commands.Contains(command))
            {
                Logger.LogAs(this, LogLevel.Warning, "Command {0} already registered.", command.Name);
                return;
            }
            Commands.Add(command);
        }

        public void UnregisterCommand(Command command)
        {
            if(!Commands.Contains(command))
            {
                Logger.LogAs(this, LogLevel.Warning, "Command {0} not registered.", command.Name);
                return;
            }
            Commands.Remove(command);
        }

        private void InitCommands()
        {
            Bind(Machine.MachineKeyword, () => Machine);
            BindStatic("connector", () => emulationManager.CurrentEmulation.Connector);
            BindStatic("emulation", () => Emulation);
            BindStatic("plugins", () => TypeManager.Instance.PluginManager);
            BindStatic("EmulationManager", () => emulationManager);

            var includeCommand = new IncludeFileCommand(this, (x, y) => pythonRunner.TryExecutePythonScript(x, y), x => TryExecuteScript(x), (x, y) => TryCompilePlugin(x, y));
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
            Commands.Add(new CreatePlatformCommand(this, x => currentMachine = x));
            Commands.Add(new UsingCommand(this, () => usings));
            Commands.Add(new QuitCommand(this, x => currentMachine = x, () => Quitted));
            Commands.Add(new PeripheralsCommand(this, () => currentMachine));
            Commands.Add(new MonitorPathCommand(this, monitorPath));
            Commands.Add(new UsingCommand(this, () => usings));
            Commands.Add(new StartCommand(this, includeCommand));
            Commands.Add(new SetCommand(this, "set", "VARIABLE", (x, y) => SetVariable(x, y, variables), (x, y) => EnableStringEater(x, y, VariableType.Variable),
                DisableStringEater, () => stringEaterMode, GetVariableName));
            Commands.Add(new SetCommand(this, "macro", "MACRO", (x, y) => SetVariable(x, y, macros), (x, y) => EnableStringEater(x, y, VariableType.Macro),
                DisableStringEater, () => stringEaterMode, GetVariableName));
            Commands.Add(new SetCommand(this, "alias", "ALIAS", (x, y) => SetVariable(x, y, aliases), (x, y) => EnableStringEater(x, y, VariableType.Alias),
	        DisableStringEater, () => stringEaterMode, GetVariableName));
            Commands.Add(new PythonExecuteCommand(this, x => ExpandVariable(x, variables), (x, y) => pythonRunner.ExecutePythonCommand(x, y)));
            Commands.Add(new ExecuteCommand(this, "execute", "VARIABLE", x => ExpandVariable(x, variables), () => variables.Keys));
            Commands.Add(new ExecuteCommand(this, "runMacro", "MACRO", x => ExpandVariable(x, macros), () => macros.Keys));
            Commands.Add(new MachCommand(this, () => currentMachine, x => currentMachine = x));
            Commands.Add(new VerboseCommand(this, x => verboseMode = x));
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

        private void ResetMachine(Machine machine)
        {
            string machineName;
            if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(machine, out machineName))
            {
                var activeMachine = _currentMachine;
                _currentMachine = machine;
                var macroName = GetVariableName("reset");
                Token resetMacro;
                if(macros.TryGetValue(macroName, out resetMacro))
                {
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
                _currentMachine = activeMachine;
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
                    var lastExpandedToken = ExpandVariable(lastVariableToken, variables);
                    // replace the last token with the expanded version
                    var newString = result.Tokens.Take(result.Tokens.Count() - 1).Select(x => x.OriginalValue).Stringify() + lastExpandedToken.OriginalValue + cmd.Substring(cmd.Length - result.UnmatchedCharactersLeft);
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

        public void SetVariable(string var, Token val, Dictionary<string, Token> collection)
        {
            collection[var] = val;
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
                    if(TryGetFilenameFromAvailablePaths(pathToken.Value, out fileName))
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
            catch(Exception e)
            {
                var ex = e as AggregateException;
                if(ex != null)
                {
                    if(ex.InnerExceptions.Any(x => !(x is RecoverableException)))
                    {
                        throw;
                    }
                }
                else if(!(e is RecoverableException))
                {
                    throw;
                }
                if(swallowExceptions)
                {
                    if(ex != null)
                    {
                        foreach(var inner in ex.InnerExceptions)
                        {
                            PrintException(String.Join(" ", result.Select(x => x.OriginalValue)), inner, writer);
                        }
                    }
                    else
                    {
                        PrintException(String.Join(" ", result.Select(x => x.OriginalValue)), e, writer);
                    }
                    if(breakOnException)
                    {
                        return false;
                    }
                }
                else
                {
                    throw;
                }
            }
            return true;
        }

        public bool Parse(string cmd, ICommandInteraction writer = null)
        {
            if(writer == null)
            {
                writer = Interaction;
            }

            if(stringEaterMode > 0)
            {
                //For multiline scripts in variables
                if(cmd.Contains(MultiLineTerminator))
                {
                    stringEaterMode += 1;
                    if(stringEaterMode > 2)
                    {
                        SetVariable(stringEaterVariableName, new StringToken(stringEaterValue), variableCollections[recordingType.Value]);
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
                SetVariable(stringEaterVariableName, null, variableCollections[recordingType.Value]);
                stringEaterValue = "";
                stringEaterMode = 0;
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

        private string GetVariableName(string variableName)
        {
            var elements = variableName.Split(new[] { '.' }, 2);

            if(elements.Length == 1 || (!elements[0].Equals("global") && !EmulationManager.Instance.CurrentEmulation.Names.Select(x => x.Replace("-", "_")).Any(x => x == elements[0])))
            {
                if(currentMachine != null)
                {
                    variableName = String.Format("{0}.{1}", EmulationManager.Instance.CurrentEmulation[currentMachine].Replace("-", "_"), variableName);
                }
                else
                {
                    variableName = String.Format("global.{0}", variableName);
                }
            }
            return variableName;
        }

        private bool TryCompilePlugin(string filename, ICommandInteraction writer)
        {
            string sha; 
            using(var shaComputer = SHA256.Create())
            {
                using(var f = File.OpenRead(filename))
                {
                    var bytesSha = shaComputer.ComputeHash(f);

                    var strBldr = new StringBuilder(32 * 2);
                    foreach(var b in bytesSha)
                    {
                        strBldr.AppendFormat("{0:X2}", b);
                    }
                    sha = strBldr.ToString();

                    if(scannedFilesCache.Contains(sha))
                    {
                        writer.WriteLine($"Code from file {filename} has already been compiled. Ignoring...");
                        return true;
                    }
                }
            }

            try
            {
                if(!EmulationManager.Instance.CompiledFilesCache.TryGetEntryWithSha(sha, out var compiledCode))
                {
                    var compiler = new AdHocCompiler();
                    compiledCode = compiler.Compile(filename);
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

        private List<string> scannedFilesCache = new List<string>();

        public bool TryExecuteScript(string filename, ICommandInteraction writer = null)
        {
            if(writer == null)
            {
                writer = Interaction;
            }

            Token oldOrigin;
            var originalFilename = filename;
            if(!TryGetFilenameFromAvailablePaths(filename, out filename))
            {
                writer.WriteError($"Could not find file '{originalFilename}'");
                return false;
            }
            variables.TryGetValue(OriginVariable, out oldOrigin);
            SetVariable(OriginVariable, new PathToken("@" + Path.GetDirectoryName(filename).Replace(" ", @"\ ")), variables);
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
            foreach(var ln in processedLines)
            {
                if(!Parse(ln))
                {
                    success = false;
                    break;
                }
            }
            if(oldOrigin != null)
            {
                SetVariable(OriginVariable, oldOrigin, variables);
            }
            return success;
        }

        public object ExecutePythonCommand(string command)
        {
            return pythonRunner.ExecutePythonCommand(command, Interaction);
        }

        private bool TryGetFilenameFromAvailablePaths(string fileName, out string fullPath)
        {
            fullPath = String.Empty;
            //Try to find the given file, then the file with path prefix
            foreach(var pathElement in monitorPath.PathElements.Prepend(String.Empty))
            {
                var currentPath = Path.Combine(pathElement, fileName);
                if(File.Exists(currentPath) || Directory.Exists(currentPath))
                {
                    fullPath = Path.GetFullPath(currentPath);
                    return true;
                }
            }
            return false;
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

        private void PrintException(string commandName, Exception e, ICommandInteraction writer)
        {
            writer.WriteError(string.Format("There was an error executing command '{0}'", commandName));
            PrintExceptionDetails(e, writer);
        }

        private IList<Token> ExpandVariables(IEnumerable<Token> tokens)
        {
            return tokens.Select(x => x is VariableToken ? ExpandVariable(x as VariableToken, variables) ?? x : x).ToList(); // ?? to prevent null tokens
        }

        private Token ExpandVariable(VariableToken token, Dictionary<string, Token> collection)
        {
            Token result;
            if(!TryExpandVariable(token, collection, out result))
            {
                throw new RecoverableException(string.Format("No such variable: ${0}", token.Value));
            }
            return result;
        }

        private bool TryExpandVariable(VariableToken token, Dictionary<string, Token> collection, out Token expandedVariable)
        {
            expandedVariable = null;
            var varName = token.Value;
            string newName;
            if(collection.TryGetValue(varName, out expandedVariable))
            {
                return true;
            }
            if(currentMachine != null)
            {
                newName = String.Format("{0}.{1}", Emulation[currentMachine].Replace("-", "_"), varName);
                if(collection.TryGetValue(newName, out expandedVariable))
                {
                    return true;
                }
            }
            newName = String.Format("{0}{1}", globalVariablePrefix, varName);
            if(collection.TryGetValue(newName, out expandedVariable))
            {
                return true;
            }
            return false;
        }

        private bool ExecuteCommand(Token[] com, ICommandInteraction writer)
        {
            if(verboseMode)
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
                if(com[1] is ConditionalEqualityToken && TryExpandVariable(variableToExpand, variables, out dummy))
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
            else if(IsNameAvailable(command.Value))
            {
                ProcessDeviceActionByName(command.Value, ExpandVariables(com.Skip(1)), writer);
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

                if (TryExpandVariable(new VariableToken(string.Format("${0}", com[0].OriginalValue)), aliases, out var cmd)) {
                        var aliasedCommand = Tokenize(cmd.GetObjectValue().ToString(), writer).Tokens;
                        return ParseTokens(aliasedCommand.Concat(com.Skip(1)), writer);
                }

                if(!pythonRunner.ExecuteBuiltinCommand(ExpandVariables(com).ToArray(), writer))
                {
                    writer.WriteError(string.Format("No such command or device: {0}", com[0].GetObjectValue()));
                    return false;
                }
            }
            return true;
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
                                     .Select(x => allButLast + "@" + StripPrefix(x, prefix).Replace(" ", @"\ "));
                var dirs = Directory.GetDirectories(directoryPath, lastElement + '*', SearchOption.TopDirectoryOnly)
                                    .Select(x => allButLast + "@" + (StripPrefix(x, prefix) + '/').Replace(" ", @"\ "));

                var result = new List<string>();
                //We change "\" characters to "/", unless they were followed by the space character, in which case they treated as escape char.
                foreach(var file in files.Concat(dirs))
                {
                    var sanitizedFile = SanitizePathSeparator(file);
                    result.Add(sanitizedFile);
                }
                return result;
            }
            catch(UnauthorizedAccessException)
            {
                return new[] { "{0}@{1}/".FormatWith(allButLast, Path.Combine(StripPrefix(directoryPath, prefix), lastElement)) };
            }
        }

        private static string StripPrefix(string path, string prefix)
        {
            if(String.IsNullOrEmpty(prefix))
            {
                return path;
            }
            return path.StartsWith(prefix, StringComparison.Ordinal) ? path.Substring(prefix.Length + (prefix.EndsWith(Path.DirectorySeparatorChar) ? 0 : 1)) : path;
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
            var allButLastOptional = AllButLastAndAggregate(prefixSplit, prefix.EndsWith(' '));
            if(!string.IsNullOrEmpty(allButLastOptional))
            {
                allButLastOptional += ' ';
            }
            var allButLast = AllButLastAndAggregate(prefixSplit);
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
#if PLATFORM_WINDOWS
                var rootIndicator = "^[a-zA-Z]:/";
#else
                var rootIndicator = "^/";
#endif
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
                    foreach(var pathEntry in monitorPath.PathElements.Select(x => Path.GetFullPath(x)))
                    {
                        if(!Directory.Exists(pathEntry))
                        {
                            continue;
                        }
                        try
                        {
                            suggestions.AddRange(SuggestFiles(allButLast, pathEntry, directory, file));
                        }
                        catch(Exception)
                        {
                            Logger.LogAs(this, LogLevel.Debug, "Bug in mono on Directory.GetFiles!");
                        }
                    }
                }
            }
            //variables
            else if(lastElement.StartsWith('$'))
            {
                var varName = lastElement.Substring(1);
                var options = variables.Keys.Concat(macros.Keys).Where(x => x.StartsWith(varName, StringComparison.Ordinal)).ToList();
                var machinePrefix = currentMachine == null ? globalVariablePrefix : Emulation[currentMachine] + ".";
                options.AddRange(variables.Keys.Concat(macros.Keys).Where(x => x.StartsWith(String.Format("{0}{1}", machinePrefix, varName), StringComparison.Ordinal)).Select(x => x.Substring(machinePrefix.Length)));

                if(options.Any())
                {
                    suggestions.AddRange(options.Select(x => allButLast + '$' + x));
                }
            }
            var currentCommandSplit = currentCommand.Split(' ');

            if(currentCommand.Contains(' '))
            {
                if(currentCommandSplit.Length <= 2)
                {
                    var cmd = Commands.SingleOrDefault(c => c.Name == currentCommandSplit[0] || c.AlternativeNames.Contains(currentCommandSplit[0])) as ISuggestionProvider;
                    if(cmd != null)
                    {
                        var sugs = cmd.ProvideSuggestions(currentCommandSplit.Length > 1 ? currentCommandSplit[1] : string.Empty);
                        suggestions.AddRange(sugs.Select(s => string.Format("{0}{1}", allButLastOptional, s)));
                    }
                    else if(currentCommandSplit.Length == 2 && GetAllAvailableNames().Contains(currentCommandSplit[0]))
                    {
                        var devInfo = GetDeviceSuggestions(currentCommandSplit[0]).Distinct();
                        if(devInfo != null)
                        {
                            suggestions.AddRange(devInfo.Where(x => x.StartsWith(currentCommandSplit[1], StringComparison.OrdinalIgnoreCase))
                                .Select(x => allButLastOptional + x));
                        }
                    }
                }
            }
            else
            {
                var sugg = Commands.Select(x => x.Name).ToList();

                sugg.AddRange(GetAllAvailableNames());
                sugg.AddRange(pythonRunner.GetPythonCommands());
                sugg.AddRange(aliases.Keys);
                sugg.AddRange(aliases.Keys.Select(x => x.Substring(x.IndexOf('.') + 1))); // remove the "global." or "{machine-name}." prefix
                suggestions.AddRange(sugg.Where(x => x.StartsWith(currentCommandSplit[0])).Select(x => prefixToAdd + x));

                if(suggestions.Count == 0) //EmulationManager
                {
                    var devInfo = GetDeviceSuggestions(typeof(EmulationManager).Name).Distinct();
                    if(devInfo != null)
                    {
                        suggestions.AddRange(devInfo.Where(x => x.StartsWith(currentCommandSplit[0], StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            return suggestions.OrderBy(x => x).Distinct();
        }

        private IEnumerable<string> GetAvailableNames()
        {
            if(currentMachine != null)
            {
                return currentMachine.GetAllNames().Union(Emulation.ExternalsManager.GetNames().Union(staticObjectDelegateMappings.Keys.Union(objectDelegateMappings.Keys)));
            }
            return Emulation.ExternalsManager.GetNames().Union(staticObjectDelegateMappings.Keys);
        }

        private IEnumerable<string> GetAllAvailableNames()
        {
            var baseNames = GetAvailableNames().ToList();
            var result = new List<string>(baseNames);
            foreach(var use in usings)
            {
                var localUse = use;
                result.AddRange(baseNames.Where(x => x.StartsWith(localUse, StringComparison.Ordinal) && x.Length > localUse.Length).Select(x => x.Substring(localUse.Length)));
            }
            return result;
        }

        private bool IsNameAvailable(string name)
        {
            var names = GetAvailableNames();
            var ret = names.Contains(name);
            if(!ret)
            {
                foreach(var use in usings)
                {
                    ret = names.Contains(use + name);
                    if(ret)
                    {
                        break;
                    }
                }
            }
            return ret;
        }

        private bool IsNameAvailableInEmulationManager(string name)
        {
            var info = GetMonitorInfo(typeof(EmulationManager));
            return info.AllNames.Contains(name);
        }

        private static IEnumerable<T> AllButLast<T>(IEnumerable<T> value) where T : class
        {
            var list = value.ToList();
            if(list.Any())
            {
                var last = list.Last();
                return list.Where(x => x != last);
            }
            return value;
        }

        private static String AllButLastAndAggregate(IEnumerable<String> value, bool dontDropLast = false)
        {
            if(dontDropLast)
            {
                return value.Any() ? value.Aggregate((x, y) => x + ' ' + y) : string.Empty;
            }
            var list = value.ToList();
            if(list.Count < 2)
            {
                return String.Empty;
            }
            var output = AllButLast(value);
            return output.Aggregate((x, y) => x + ' ' + y);
        }

        public void Bind(string name, Func<object> objectServer)
        {
            objectDelegateMappings[name] = objectServer;
        }

        public void BindStatic(string name, Func<object> objectServer)
        {
            staticObjectDelegateMappings[name] = objectServer;
        }

        private object FromStaticMapping(string name)
        {
            Func<object> value;
            if(staticObjectDelegateMappings.TryGetValue(name, out value))
            {
                return value();
            }
            return null;
        }

        private object FromMapping(string name)
        {
            Func<object> value;
            if(objectDelegateMappings.TryGetValue(name, out value))
            {
                return value();
            }
            return null;
        }

        public IEnumerable<Command> RegisteredCommands
        {
            get
            {
                return Commands;
            }
        }

        private readonly Dictionary<string, Func<object>> staticObjectDelegateMappings = new Dictionary<string, Func<object>>();
        private readonly Dictionary<string, Func<object>> objectDelegateMappings = new Dictionary<string, Func<object>>();
        private readonly Dictionary<string, Token> variables = new Dictionary<string, Token>();
        private readonly Dictionary<string, Token> macros = new Dictionary<string, Token>();
        private readonly Dictionary<string, Token> aliases = new Dictionary<string, Token>();
        private readonly Dictionary<VariableType, Dictionary<string, Token>> variableCollections;
        private int stringEaterMode;
        private string stringEaterValue = "";
        private string stringEaterVariableName = "";
        private VariableType? recordingType;
        private bool verboseMode;

        public ICommandInteraction Interaction { get; set; }

        public void OnMachineRemoved(Machine m)
        {
            if(m == currentMachine)
            {
                currentMachine = null;
            }
        }

        private Machine _currentMachine;

        private Machine currentMachine
        {
            get
            {
                return _currentMachine;
            }
            set
            {
                _currentMachine = value;

                var mc = MachineChanged;
                if(mc != null)
                {
                    mc(_currentMachine != null ? Emulation[_currentMachine] : null);
                }
            }
        }

        public event Action<string> MachineChanged;

        private readonly Tokenizer.Tokenizer tokenizer = Tokenizer.Tokenizer.CreateTokenizer();

        internal delegate void CommandHandler(IEnumerable<Token> p, ICommandInteraction w);

        private const string globalVariablePrefix = "global.";

        private const string ConfigurationSection = "monitor";

        private const string MultiLineTerminator = @"""""""";

        private const string OriginVariable = globalVariablePrefix + "ORIGIN";

        private const string CurrentDirectoryVariable = globalVariablePrefix + "CWD";

        private HashSet<Command> Commands { get; set; }

        private readonly MonitorPythonEngine pythonRunner;

        private enum VariableType
        {
            Variable,
            Macro,
            Alias
        }
    }
}
