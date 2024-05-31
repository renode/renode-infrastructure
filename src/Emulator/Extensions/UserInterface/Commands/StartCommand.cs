//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class StartCommand : Command
    {

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine(String.Format("{0} - starts the whole emulation", Name));
            writer.WriteLine(String.Format("{0} @path - executes the script and starts the emulation", Name));
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            writer.WriteLine("Starting emulation...");
            EmulationManager.Instance.CurrentEmulation.StartAll();
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken path)
        {
            if(IncludeCommand.Run(writer, path))
            {
                EmulationManager.Instance.CurrentEmulation.StartAll();
            }
        }

        private readonly IncludeFileCommand IncludeCommand;

        public StartCommand(Monitor monitor, IncludeFileCommand includeCommand) :  base(monitor, "start", "starts the emulation.", "s")
        {
            if(includeCommand == null)
            {
                throw new ArgumentException("includeCommand cannot be null.", "includeCommand");
            }
            IncludeCommand = includeCommand;
        }
    }
}

