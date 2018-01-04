//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Commands;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class VersionCommand : AutoLoadCommand
    {
        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            writer.WriteLine(EmulationManager.Instance.VersionString);
        }

        public VersionCommand(Monitor monitor) : base(monitor, "version", "shows version information.")
        {
        }
    }
}

