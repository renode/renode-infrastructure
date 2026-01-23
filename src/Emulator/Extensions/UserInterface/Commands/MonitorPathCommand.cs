//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class MonitorPathCommand : Command
    {
        public MonitorPathCommand(Monitor monitor, MonitorPath path) : base(monitor, "path", "allows modification of internal 'PATH' variable.")
        {
            monitorPath = path;
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            PrintCurrentPath(writer);
            writer.WriteLine(string.Format("Default 'PATH' value is: {0}", monitorPath.DefaultPath));
            writer.WriteLine();
            writer.WriteLine("You can use following commands:");
            writer.WriteLine(String.Format("'{0} set @path'\tto set 'PATH' to the given value", Name));
            writer.WriteLine(String.Format("'{0} add @path'\tto prepend the given value to 'PATH'", Name));
            writer.WriteLine(String.Format("'{0} reset'\t\tto reset 'PATH' to it's default value", Name));
        }

        [Runnable]
        public void Reset(ICommandInteraction writer, [Values("reset")] LiteralToken _)
        {
            monitorPath.Reset();
            PrintCurrentPath(writer);
        }

        [Runnable]
        public void SetOrAdd(ICommandInteraction writer, [Values("set", "add")] LiteralToken action, StringToken path)
        {
            switch(action.Value)
            {
            case "set":
                monitorPath.Path = path.Value;
                break;
            case "add":
                monitorPath.Prepend(path.Value);
                break;
            }
            PrintCurrentPath(writer);
        }

        private void PrintCurrentPath(ICommandInteraction writer)
        {
            writer.WriteLine(string.Format("Current 'PATH' value is: {0}", monitorPath.Path));
        }

        readonly MonitorPath monitorPath;
    }
}
