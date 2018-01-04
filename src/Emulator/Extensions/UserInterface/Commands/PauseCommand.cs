//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class PauseCommand : AutoLoadCommand
    {
        [Runnable]
        public void Halt(ICommandInteraction writer)
        {
            writer.WriteLine("Pausing emulation...");
            EmulationManager.Instance.CurrentEmulation.PauseAll();
        }

        public PauseCommand(Monitor monitor) :  base(monitor, "pause", "pauses the emulation.", "p")
        {
        }
    }
}

