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
    public class VerboseCommand : Command
    {
        public VerboseCommand(Monitor monitor, Action<bool> setVerbosity) : base(monitor, "verboseMode", "controls the verbosity of the Monitor.")
        {
            verbose = false;
            this.setVerbosity = setVerbosity;
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine("Current value: " + verbose);
        }

        [Runnable]
        public void SetVerbosity(ICommandInteraction _, BooleanToken verbosity)
        {
            verbose = verbosity.Value;
            setVerbosity(verbose);
        }

        private bool verbose;

        private readonly Action<bool> setVerbosity;
    }
}