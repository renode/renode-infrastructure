//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.Plugins.SampleCommandPlugin
{
    public sealed class HelloCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine(String.Format("{0} \"name\"", Name));
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken name)
        {
            writer.WriteLine(String.Format("Hello, {0}!", name.Value));
        }

        public HelloCommand(Monitor monitor) : base(monitor, "hello", "Greets a user.")
        {
        }
    }
}
