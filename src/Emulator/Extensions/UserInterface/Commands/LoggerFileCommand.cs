//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class LoggerFileCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteError("\nYou must specify the filename (full path or relative) for output file.");
        }

        [Runnable]
        public void Run(PathToken path)
        {
            Logger.AddBackend(new FileBackend(path.Value), "file", true);
        }

        public LoggerFileCommand(Monitor monitor) : base(monitor, "logFile", "sets the output file for logger.", "logF")
        {
        }
    }
}

