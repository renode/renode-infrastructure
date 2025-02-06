//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Commands;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class NetworkLoggerCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usages:");
            writer.WriteLine($" {FullCommand} PORT [PLAIN_MODE]");
            writer.WriteError("\nYou must specify the port number for the logger's socket.");
        }

        [Runnable]
        public void Run(DecimalIntegerToken port, BooleanToken plainMode)
        {
            Run((int)port.Value, plainMode.Value);
        }

        [Runnable]
        public void Run(DecimalIntegerToken port)
        {
            Run((int)port.Value);
        }

        public NetworkLoggerCommand(Monitor monitor) : base(monitor, FullCommand, "sets the output port for logger.", "logN")
        {
        }

        private void Run(int port, bool plainMode = true)
        {
            Logger.AddBackend(new NetworkBackend(port, plainMode), BackendName, overwrite: true);
        }

        private readonly string BackendName = "network";
        private const string FullCommand = "logNetwork";
    }
}

