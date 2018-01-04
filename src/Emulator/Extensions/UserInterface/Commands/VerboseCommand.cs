//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class VerboseCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine("Current value: " + verbose);
        }

        [Runnable]
        public void SetVerbosity(ICommandInteraction writer, BooleanToken verbosity)
        {
            verbose = verbosity.Value;
            setVerbosity(verbose);
        }

        public VerboseCommand(Monitor monitor, Action<bool> setVerbosity) : base(monitor, "verboseMode", "controls the verbosity of the Monitor.")
        {
            verbose = false;
            this.setVerbosity = setVerbosity;
        }

        private readonly Action<bool> setVerbosity;
        private bool verbose;
    }
}

