//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Commands;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class NumbersModeCommand : AutoLoadCommand
    {
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
        public void Run(ICommandInteraction writer,
                        [Values("Both", "Decimal", "Hexadecimal")] LiteralToken format)
        {
            monitor.CurrentNumberFormat = (Monitor.NumberModes)Enum.Parse(typeof(Monitor.NumberModes), format.Value);
        }

        public NumbersModeCommand(Monitor monitor):base(monitor, "numbersMode", "sets the way numbers are displayed.")
        {

        }
    }
}

