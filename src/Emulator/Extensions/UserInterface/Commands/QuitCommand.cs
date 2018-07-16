//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class QuitCommand : Command
    {
        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            writer.WriteLine("Renode is quitting", ConsoleColor.Green);
            SetCurrentMachine(null);
            Quitted?.Invoke()?.Invoke();
            writer.QuitEnvironment = true;
        }

        private Action<Machine> SetCurrentMachine;
        private event Func<Action> Quitted;

        public QuitCommand(Monitor monitor, Action<Machine> setCurrentMachine, Func<Action> quitted) : base(monitor, "quit", "quits the emulator.", "q")
        {
            SetCurrentMachine = setCurrentMachine;
            Quitted = quitted;
        }
    }
}

