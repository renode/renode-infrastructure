//
// Copyright (c) 2010-2018 Antmicro
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
    public class NumbersModeCommand : AutoLoadCommand
    {
        public NumbersModeCommand(Monitor monitor) : base(monitor, "numbersMode", "sets the way numbers are displayed.")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();

            writer.WriteLine(string.Format("Current mode: {0}", monitor.CurrentNumberFormat));
            writer.WriteLine();
            writer.WriteLine("Options:");
            foreach(var item in typeof(Monitor.NumberModes).GetEnumNames())
            {
                writer.WriteLine(item);
            }
        }

        [Runnable]
        public void Run(ICommandInteraction _, [Values("Both", "Decimal", "Hexadecimal")] LiteralToken format)
        {
            monitor.CurrentNumberFormat = (Monitor.NumberModes)Enum.Parse(typeof(Monitor.NumberModes), format.Value);
        }
    }
}