//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class MonitorPathCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            PrintCurrentPath(writer);
            writer.WriteLine(string.Format("Default 'PATH' value is: {0}", monitorPath.DefaultPath));
            writer.WriteLine();
            writer.WriteLine("You can use following commands:");
            writer.WriteLine(String.Format("'{0} set @path'\tto set 'PATH' to the given value", Name));
            writer.WriteLine(String.Format("'{0} add @path'\tto append the given value to 'PATH'", Name));
            writer.WriteLine(String.Format("'{0} reset'\t\tto reset 'PATH' to it's default value", Name));
        }
        private void PrintCurrentPath(ICommandInteraction writer)
        {
            writer.WriteLine(string.Format("Current 'PATH' value is: {0}", monitorPath.Path));
        }

        [Runnable]
        public void SetOrAdd(ICommandInteraction writer, [Values( "set", "add")] LiteralToken action, StringToken path)
        {
            switch(action.Value)
            {
            case "set":
                monitorPath.Path = path.Value;
                break;
            case "add":
                monitorPath.Append(path.Value);
                break;
            }
            PrintCurrentPath(writer);
        }

        [Runnable]
        public void Reset(ICommandInteraction writer, [Values( "reset")] LiteralToken action)
        {
            monitorPath.Reset();
            PrintCurrentPath(writer);
        }

        MonitorPath monitorPath;
        public MonitorPathCommand(Monitor monitor, MonitorPath path) : base(monitor, "path", "allows modification of internal 'PATH' variable.")
        {
            monitorPath = path;
        }
    }
}

