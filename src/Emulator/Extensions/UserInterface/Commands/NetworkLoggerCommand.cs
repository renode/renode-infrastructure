//
// Copyright (c) 2010-2024 Antmicro
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
            writer.WriteError("\nYou must specify the port number for the logger's socket.");
        }

        [Runnable]
        public void Run(DecimalIntegerToken port)
        {
            Logger.AddBackend(new NetworkBackend((int)port.Value), BackendName, true);
        }

        public NetworkLoggerCommand(Monitor monitor) : base(monitor, "logNetwork", "sets the output port for logger.", "logN")
        {
        }

        private readonly string BackendName = "network";
    }
}

