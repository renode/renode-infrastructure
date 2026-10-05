//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

#nullable enable

namespace Antmicro.Renode.UserInterface.Commands
{
    public class RepeatLastCommand : AutoLoadCommand
    {
        public RepeatLastCommand(Monitor monitor) : base(monitor, "repeatLast", "Enables or disables rerunning the previous command on empty input.", "rl")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("You can use the following commands:");
            writer.WriteLine($"{base.Name}   set| true|t       to enable");
            writer.WriteLine($"{base.Name} unset|false|f|clear to disable");
            writer.WriteLine($"{base.Name} toggle              to toggle");
        }

        [Runnable]
        public void Query(ICommandInteraction writer)
        {
            writer.WriteLine($"{base.monitor.RepeatLastCommand}");
        }

        [Runnable]
        public void Run(ICommandInteraction _, [Values("set", "t", "unset", "f", "clear")] LiteralToken action)
        {
            switch(action.Value)
            {
            case "set":
            case "t":
                base.monitor.RepeatLastCommand = true;
                break;
            case "unset":
            case "f":
            case "clear":
                base.monitor.RepeatLastCommand = false;
                break;
            }
        }

        [Runnable]
        public void Run(ICommandInteraction _, BooleanToken action)
        {
            base.monitor.RepeatLastCommand = action.Value;
        }

        [Runnable]
        public void Toggle(ICommandInteraction writer, [Values("toggle")] LiteralToken _)
        {
            base.monitor.RepeatLastCommand = !base.monitor.RepeatLastCommand;
            Query(writer);
        }
    }
}
