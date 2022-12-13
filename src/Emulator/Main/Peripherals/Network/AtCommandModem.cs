//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract partial class AtCommandModem : IUART
    {
        public AtCommandModem(Machine machine)
        {
            this.machine = machine;
            commandMethods = GetCommandMethods();
            argumentParsers = GetArgumentParsers();

            Reset();
        }

        public virtual void Reset()
        {
            Enabled = true;
            PassthroughMode = false;
            echoEnabled = true;
            lineBuffer = new StringBuilder();
        }

        public void WriteChar(byte value)
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
                this.Log(LogLevel.Debug, "Command received: '{0}'", command);
                var response = HandleCommand(command);
                if(response != null)
                {
                    SendResponse(response);
                }
            }
            else
            {
                lineBuffer.Append(charValue);
            }
        }

        public abstract void PassthroughWriteChar(byte value);

        public virtual uint BaudRate => 115200;

        public Bits StopBits => Bits.One;

        public Parity ParityBit => Parity.None;

        [field: Transient]
        public event Action<byte> CharReceived;

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

        protected void SendString(string str)
        {
            var stringForDisplay = str.Replace(CrLf, CrLfSymbol);
            var charReceived = CharReceived;
            if(charReceived == null)
            {
                this.Log(LogLevel.Warning, "Wanted to send string '{0}' but nothing is connected to {1}",
                    stringForDisplay, nameof(CharReceived));
                return;
            }

            this.Log(LogLevel.Debug, "Sending string: '{0}'", stringForDisplay);
            lock(uartWriteLock)
            {
                foreach(var ch in str)
                {
                    charReceived((byte)ch);
                }
            }
        }

        protected void SendResponse(Response response)
        {
            SendString(response.StringValue);
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

        protected bool Enabled { get; set; }
        protected bool PassthroughMode { get; set; }

        protected const string OkMessage = "OK";
        protected const string ErrorMessage = "ERROR";
        protected const string CrLf = "\r\n";
        protected const string CrLfSymbol = "⏎";
        protected const byte ControlZ = 26;
        protected const byte Escape = 27;

        protected readonly Machine machine;

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

        private Response HandleCommand(string command)
        {
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
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var argumentsString = parsed.Arguments;
            object[] arguments;
            try
            {
                arguments = ParseArguments(argumentsString, parameterTypes);
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
                var parameterTypesString = string.Join(", ", parameterTypes.Select(t => t.FullName));
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
            var commandMethods = this.GetType().GetMethodsWithAttribute<AtCommandAttribute>()
                .SelectMany(ma => ma.Attribute.Types, (ma, type) => new { ma.Attribute.Command, type, ma.Method })
                .GroupBy(ma => ma.Command)
                .ToDictionary(g => g.Key, g => g.ToDictionary(ma => ma.type, ma => ma.Method));

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

        private bool echoEnabled;
        private StringBuilder lineBuffer;

        private readonly Dictionary<string, Dictionary<CommandType, MethodInfo>> commandMethods;
        private readonly Dictionary<Type, Func<string, object>> argumentParsers;
        private readonly MethodInfo defaultTestCommandMethodInfo = typeof(AtCommandModem)
            .GetMethod(nameof(DefaultTestCommand), BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly object uartWriteLock = new object();

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
            public Response(string status, params string[] parameters) : this(status, "", parameters)
            {
            }

            public Response WithParameters(params string[] parameters)
            {
                return new Response(Status, Trailer, parameters);
            }

            public Response WithTrailer(string trailer)
            {
                return new Response(Status, trailer, Parameters);
            }

            // The status line is usually "OK" or "ERROR"
            public string Status { get; }
            // The parameters are the actual useful data returned by a command, for example
            // the current value of a parameter in the case of a Read command
            public string[] Parameters { get; }
            // The trailer can be thought of as an immediately-sent URC: it is sent after
            // the status line
            public string Trailer { get; }
            // The string representation formatted as a modem would send it
            public string StringValue { get; }

            private Response(string status, string trailer, string[] parameters)
            {
                Status = status;
                Trailer = trailer;
                Parameters = parameters;

                var parametersContent = string.Join(CrLf, Parameters);
                var parametersPart = parametersContent.Length > 0 ? parametersContent.SurroundWith(CrLf) : "";
                var statusPart = Status.SurroundWith(CrLf);
                var trailerPart = Trailer.Length > 0 ? Trailer.SurroundWith(CrLf) : "";
                StringValue = parametersPart + statusPart + trailerPart;
            }
        }

        [Flags]
        protected enum CommandType
        {
            Test = 1 << 0,
            Read = 1 << 1,
            Write = 1 << 2,
            Execution = 1 << 3,
        }
    }
}
