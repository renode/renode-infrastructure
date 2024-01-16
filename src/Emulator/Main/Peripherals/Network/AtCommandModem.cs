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
using System.Text;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract partial class AtCommandModem : IUART
    {
        public AtCommandModem(IMachine machine)
        {
            this.machine = machine;
            commandOverrides = new Dictionary<string, CommandOverride>();
            Init();
            Reset();
        }

        public virtual void Reset()
        {
            Enabled = true;
            PassthroughMode = false;
            echoEnabled = EchoEnabledAtReset;
            lineBuffer = new StringBuilder();
        }

        public virtual void WriteChar(byte value)
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Warning, "Modem is not enabled, ignoring incoming byte 0x{0:x2}", value);
                return;
            }

            if(PassthroughMode)
            {
                PassthroughWriteChar(value);
                return;
            }

            // The Zephyr bg9x driver sends a ^Z after sending fixed-length binary data.
            // Ignore it and similar things done by other drivers.
            if(value == ControlZ || value == Escape)
            {
                this.Log(LogLevel.Debug, "Ignoring byte 0x{0:x2} in AT mode", value);
                return;
            }

            var charValue = (char)value;

            // Echo is only active in AT command mode
            if(echoEnabled)
            {
                SendChar(charValue);
            }

            // Ignore newline characters
            // Commands are supposed to end with \r, some software sends \r\n
            if(charValue == '\n')
            {
                // do nothing
            }
            // \r indicates that the command is complete
            else if(charValue == '\r')
            {
                var command = lineBuffer.ToString();
                lineBuffer.Clear();
                if(command == "")
                {
                    this.Log(LogLevel.Debug, "Ignoring empty command");
                    return;
                }
                this.Log(LogLevel.Debug, "Command received: '{0}'", command);
                var response = HandleCommand(command);
                if(response != null)
                {
                    if(CommandResponseDelayMilliseconds.HasValue)
                    {
                        machine.ScheduleAction(TimeInterval.FromMilliseconds(CommandResponseDelayMilliseconds.Value), _ => SendResponse(response)); 
                    }
                    else
                    {
                        SendResponse(response);
                    }
                }
            }
            else
            {
                lineBuffer.Append(charValue);
            }
        }

        public void OverrideResponseForCommand(string command, string status, string parameters = "", bool oneShot = false)
        {
            var splitParams = string.IsNullOrEmpty(parameters) ? new string[] { } : parameters.Split('\n');
            commandOverrides[command] = new CommandOverride(new Response(status, splitParams), oneShot);
        }

        public void ClearOverrides()
        {
            commandOverrides.Clear();
        }

        public abstract void PassthroughWriteChar(byte value);

        public bool EchoEnabledAtReset { get; set; } = true;

        public bool EchoEnabled => echoEnabled;

        public virtual uint BaudRate { get; protected set; } = 115200;

        public Bits StopBits => Bits.One;

        public Parity ParityBit => Parity.None;

        public ulong? CommandResponseDelayMilliseconds { get; set; }

        public uint? TransferBandwidth { get; set; }

        [field: Transient]
        public event Action<byte> CharReceived;

        protected static readonly Encoding StringEncoding = Encoding.UTF8;
        protected static readonly byte[] CrLfBytes = StringEncoding.GetBytes(CrLf);
        protected static readonly Response Ok = new Response(OkMessage);
        protected static readonly Response Error = new Response(ErrorMessage);

        protected void SendChar(char ch)
        {
            var charReceived = CharReceived;
            if(charReceived == null)
            {
                this.Log(LogLevel.Warning, "Wanted to send char '{0}' but nothing is connected to {1}",
                    ch, nameof(CharReceived));
                return;
            }

            lock(uartWriteLock)
            {
                CharReceived?.Invoke((byte)ch);
            }
        }

        protected void SendBytes(byte[] bytes)
        {
            var charReceived = CharReceived;
            if(charReceived == null)
            {
                this.Log(LogLevel.Warning, "Wanted to send bytes '{0}' but nothing is connected to {1}",
                    Misc.PrettyPrintCollectionHex(bytes), nameof(CharReceived));
                return;
            }

            if(TransferBandwidth == null)
            {
                lock(uartWriteLock)
                {
                    foreach(var b in bytes)
                    {
                        charReceived(b);
                    }
                }
            }
            else
            {
                var currentByte = 0;
                IManagedThread thread = null;
                thread = machine.ObtainManagedThread(() =>
                {
                    lock(uartWriteLock)
                    {
                        var b = bytes[currentByte];
                        currentByte++;
                        charReceived(b);
                    }

                    if(currentByte == bytes.Length)
                    {
                        thread.Stop();
                    }
                }, TransferBandwidth.Value);
                thread.Start();
            }
        }

        protected void SendString(string str)
        {
            var strWithNewlines = str.SurroundWith(CrLf);
            var stringForDisplay = str.SurroundWith(CrLfSymbol);
            this.Log(LogLevel.Debug, "Sending string: '{0}'", stringForDisplay);
            SendBytes(StringEncoding.GetBytes(strWithNewlines));
        }

        protected void SendResponse(Response response)
        {
            this.Log(LogLevel.Debug, "Sending response: {0}", response);
            SendBytes(response.GetBytes());
        }

        // This method is intended for cases where a modem driver sends a command
        // and expects a URC (Unsolicited Result Code) after some time, and a URC
        // sent immediately after the normal (solicited) command response and status
        // string (like OK) is not accepted. In this case we can use something like
        // `ExecuteWithDelay(() => SendResponse(...))` in the command callback.
        protected void ExecuteWithDelay(Action action, ulong milliseconds = 50)
        {
            machine.ScheduleAction(TimeInterval.FromMilliseconds(milliseconds), _ => action());
        }

        // AT - Do Nothing, Successfully (used to detect modem presence)
        [AtCommand("AT")]
        protected virtual Response At()
        {
            return Ok;
        }

        // ATE - Enable/Disable Echo
        [AtCommand("ATE")]
        protected virtual Response Ate(int value = 0)
        {
            echoEnabled = value != 0;
            return Ok;
        }

        // ATH - Hook Status
        // This is a stub implementation - models should override it if they need to do
        // something special on hangup/pickup
        [AtCommand("ATH")]
        protected virtual Response Ath(int offHook = 0)
        {
            return Ok;
        }

        // AT&W - Save Current Parameters to NVRAM
        [AtCommand("AT&W")]
        protected virtual Response Atw()
        {
            EchoEnabledAtReset = echoEnabled;
            return Ok;
        }

        protected bool Enabled { get; set; }
        protected bool PassthroughMode { get; set; }

        protected const string OkMessage = "OK";
        protected const string ErrorMessage = "ERROR";
        protected const string CrLf = "\r\n";
        protected const string CrLfSymbol = "⏎";
        protected const byte ControlZ = 26;
        protected const byte Escape = 27;

        protected readonly IMachine machine;

        private Response DefaultTestCommand()
        {
            return Ok;
        }

        private bool TryFindCommandMethod(ParsedCommand command, out MethodInfo method)
        {
            if(!commandMethods.TryGetValue(command.Command, out var types))
            {
                method = null;
                return false;
            }

            // Look for an implementation method for this combination of command and type
            if(!types.TryGetValue(command.Type, out method))
            {
                // If we are looking for a test command and there is no implementation, return the default one
                // We know the command itself exists because it has an entry in commandMethods (it just has no
                // explicitly-implemented Test behavior)
                if(command.Type == CommandType.Test)
                {
                    method = defaultTestCommandMethodInfo;
                }
            }

            return method != null;
        }

        protected virtual Response HandleCommand(string command)
        {
            if(commandOverrides.TryGetValue(command, out var overrideResp))
            {
                this.Log(LogLevel.Debug, "Using overridden response for '{0}'{1}",
                    command, overrideResp.oneShot ? " once" : "");
                if(overrideResp.oneShot)
                {
                    commandOverrides.Remove(command);
                }
                return overrideResp.response;
            }

            if(!ParsedCommand.TryParse(command, out var parsed))
            {
                this.Log(LogLevel.Warning, "Failed to parse command '{0}'", command);
                return Error;
            }

            if(!TryFindCommandMethod(parsed, out var handler))
            {
                this.Log(LogLevel.Warning, "Unhandled command '{0}'", command);
                return Error;
            }

            var parameters = handler.GetParameters();
            var argumentsString = parsed.Arguments;
            object[] arguments;
            try
            {
                arguments = ParseArguments(argumentsString, parameters);
            }
            catch(ArgumentException e)
            {
                this.Log(LogLevel.Warning, "Failed to parse arguments: {0}", e.Message);
                // An incorrectly-formatted argument always leads to a plain ERROR
                return Error;
            }
            // Pad arguments to the number of parameters with Type.Missing to use defaults
            if(arguments.Length < parameters.Length)
            {
                arguments = arguments
                    .Concat(Enumerable.Repeat(Type.Missing, parameters.Length - arguments.Length))
                    .ToArray();
            }

            try
            {
                return (Response)handler.Invoke(this, arguments);
            }
            catch(ArgumentException)
            {
                var parameterTypesString = string.Join(", ", parameters.Select(t => t.ParameterType.FullName));
                var argumentTypesString = string.Join(", ", arguments.Select(a => a?.GetType()?.FullName ?? "(null)"));
                this.Log(LogLevel.Error, "Argument type mismatch in command '{0}'. Got types [{1}], expected [{2}]",
                    command, argumentTypesString, parameterTypesString);
                return Error;
            }
        }

        private Dictionary<string, Dictionary<CommandType, MethodInfo>> GetCommandMethods()
        {
            // We want to get a hierarchy like
            // AT+IPR
            // | - Read  -> IprRead
            // | - Write -> IprWrite
            // but with the possibility of having for example
            // AT+ABC
            // | - Read  -> AbcReadWrite
            // | - Write -> AbcReadWrite
            // which would come from AbcReadWrite being annotated with [AtCommand("AT+ABC", Read|Write)]
            // so we first flatten the types and then group by the command name.
            // Also, if unrelated (i.e. not in an override hierarchy) methods in a base class
            // and a subclass are both annotated with [AtCommand("AT+ABC", Read)], we want to use the
            // implementation from the most derived class. This is done using DistinctBy(type), in order to turn
            // AT+ABC
            // | - Read  -> AbcReadDerivedDerived (in subclass C <: B)
            // | - Read  -> AbcReadDerived (in subclass B <: A)
            // | - Read  -> AbcRead (in base class A)
            // into
            // AT+ABC
            // | - Read  -> AbcReadDerivedDerived (in subclass C <: B)
            // This relies on the fact that GetMethodsWithAttribute returns methods sorted by
            // the depth of their declaring class in the inheritance hierarchy, deepest first.

            // We don't inherit the [AtCommand] attribute in order to allow "hiding" commands
            // in subclasses by overriding them and not marking them with [AtCommand]
            var commandMethods = this.GetType().GetMethodsWithAttribute<AtCommandAttribute>(inheritAttribute: false)
                .SelectMany(ma => ma.Attribute.Types, (ma, type) => new { ma.Attribute.Command, type, ma.Method })
                .GroupBy(ma => ma.Command)
                .ToDictionary(g => g.Key, g => g.DistinctBy(h => h.type).ToDictionary(ma => ma.type, ma => ma.Method));

            // Verify that all command methods return Response
            foreach(var typeMethod in commandMethods.Values.SelectMany(m => m))
            {
                var type = typeMethod.Key;
                var method = typeMethod.Value;
                if(method.ReturnType != typeof(Response))
                {
                    throw new InvalidOperationException($"Command method {method.Name} ({type}) does not return {nameof(Response)}");
                }
            }

            return commandMethods;
        }

        [PostDeserialization]
        private void Init()
        {
            commandMethods = GetCommandMethods();
            argumentParsers = GetArgumentParsers();
            defaultTestCommandMethodInfo = typeof(AtCommandModem).GetMethod(nameof(DefaultTestCommand), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private bool echoEnabled;
        private StringBuilder lineBuffer;

        [Transient]
        private Dictionary<string, Dictionary<CommandType, MethodInfo>> commandMethods;
        [Transient]
        private Dictionary<Type, Func<string, object>> argumentParsers;
        [Transient]
        private MethodInfo defaultTestCommandMethodInfo;

        private readonly object uartWriteLock = new object();
        private readonly Dictionary<string, CommandOverride> commandOverrides;

        [AttributeUsage(AttributeTargets.Method)]
        protected class AtCommandAttribute : Attribute
        {
            public AtCommandAttribute(string command, CommandType type = CommandType.Execution)
            {
                Command = command;
                Type = type;
            }

            public string Command { get; }

            public CommandType Type { get; }

            public IEnumerable<CommandType> Types
            {
                get => Enum.GetValues(typeof(CommandType)).Cast<CommandType>().Where(t => (Type & t) != 0);
            }
        }

        protected class Response
        {
            public Response(string status, params string[] parameters) : this(status, "", parameters, null)
            {
            }

            public Response WithParameters(params string[] parameters)
            {
                return new Response(Status, Trailer, parameters, null);
            }

            public Response WithParameters(byte[] parameters)
            {
                return new Response(Status, Trailer, null, parameters);
            }

            public Response WithTrailer(string trailer)
            {
                return new Response(Status, trailer, Parameters, BinaryBody);
            }

            public override string ToString()
            {
                string bodyRepresentation;
                if(Parameters != null)
                {
                    bodyRepresentation = string.Join(", ", Parameters.Select(p => p.SurroundWith("'")));
                    bodyRepresentation = $"[{bodyRepresentation}]";
                }
                else
                {
                    bodyRepresentation = Misc.PrettyPrintCollectionHex(BinaryBody);
                }

                var result = $"Body: {bodyRepresentation}; Status: {Status}";
                if(Trailer.Length > 0)
                {
                    result += $"; Trailer: {Trailer}";
                }
                return result;
            }

            // Get the binary representation formatted as a modem would send it
            public byte[] GetBytes()
            {
                return bytes;
            }

            // The status line is usually "OK" or "ERROR"
            public string Status { get; }
            // The parameters are the actual useful data returned by a command, for example
            // the current value of a parameter in the case of a Read command
            public string[] Parameters { get; }
            // Alternatively to parameters, a binary body can be provided. It is placed where
            // the parameters would be and surrounded with CrLf
            public byte[] BinaryBody { get; }
            // The trailer can be thought of as an immediately-sent URC: it is sent after
            // the status line.
            public string Trailer { get; }

            private Response(string status, string trailer, string[] parameters, byte[] binaryBody)
            {
                // We want exactly one of Parameters or BinaryBody. If neither is provided or both
                // are, throw an exception.
                if((parameters != null) == (binaryBody != null))
                {
                    throw new InvalidOperationException("Either parameters xor a binary body must be provided");
                }

                Status = status;
                Trailer = trailer;
                Parameters = parameters;
                BinaryBody = binaryBody;

                byte[] bodyContent;
                if(Parameters != null)
                {
                    var parametersContent = string.Join(CrLf, Parameters);
                    bodyContent = StringEncoding.GetBytes(parametersContent);
                }
                else
                {
                    bodyContent = BinaryBody;
                }

                var bodyBytes = CrLfBytes.Concat(bodyContent).Concat(CrLfBytes);
                var statusPart = Status.SurroundWith(CrLf);
                var statusBytes = StringEncoding.GetBytes(statusPart);
                var trailerBytes = StringEncoding.GetBytes(Trailer.SurroundWith(CrLf));
                bytes = bodyBytes.Concat(statusBytes).Concat(trailerBytes).ToArray();
            }

            private readonly byte[] bytes;
        }

        [Flags]
        protected enum CommandType
        {
            Test = 1 << 0,
            Read = 1 << 1,
            Write = 1 << 2,
            Execution = 1 << 3,
        }

        private struct CommandOverride
        {
            public CommandOverride(Response response, bool oneShot)
            {
                this.response = response;
                this.oneShot = oneShot;
            }

            public readonly Response response;
            public readonly bool oneShot;
        }
    }
}
