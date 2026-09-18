//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class PrintEnvironmentCommand : Command
    {
        public PrintEnvironmentCommand(Monitor monitor) : base(monitor, "printenv", "prints all variables, macros and aliases")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine(Name);
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            var env = monitor.MonitorContext;

            if(monitor.IsContextOverriden)
            {
                writer.WriteLine("Global Env Overriden");
            }

            writer.WriteLine("Variables:");
            foreach(var name in env.Variables.Keys)
            {
                var typename = env.Variables[name]
                    .GetType()
                    .ToString()
                    .Split(".")[^1];

                var value = env.Variables[name]
                    .GetObjectValue()
                    .ToString();

                var originalValue = env.Variables[name].OriginalValue;

                writer.WriteLine($"\t{typename} {name} = {value} ({originalValue})");
            }

            writer.WriteLine("Macros:");
            foreach(var name in env.Macros.Keys)
            {
                var body = env.Macros[name]
                    .GetObjectValue()
                    .ToString()
                    .Replace("\n", "\n\t");

                writer.WriteLine($"{name} \"{body}\"");
            }

            writer.WriteLine("Aliases:");
            foreach(var name in env.Aliases.Keys)
            {
                var value = env.Aliases[name]
                    .GetObjectValue()
                    .ToString();

                writer.WriteLine($"\t{name} = {value}");
            }
        }
    }
}
