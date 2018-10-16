//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class WatchCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);

            writer.WriteLine("\nUsage:");
            writer.WriteLine("watch \"<command>\" <refresh period in ms>");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken command, DecimalIntegerToken delay)
        {
            var terminal = writer as CommandInteraction;
            if(terminal == null)
            {
                writer.WriteError("Watch command can be used on a full-featured terminal only");
                return;
            }

            var eater = new CommandInteractionEater();
            while(!terminal.HasNewInput)
            {
                terminal.SaveCursor();
                monitor.Parse(command.Value, eater);

                writer.Write(eater.GetContents());
                var error = eater.GetError();
                if(error.Length > 0)
                {
                    writer.WriteError(error);
                    break;
                }
                eater.Clear();

                System.Threading.Thread.Sleep((int)delay.Value);
                terminal.RestoreCursor();
                terminal.ClearToEnd();
            }
        }

        public WatchCommand(Monitor monitor) : base(monitor, "watch", "executes a command periodically, showing output in monitor", "w")
        {
        }
    }
}