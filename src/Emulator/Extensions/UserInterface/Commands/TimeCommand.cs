//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class TimeCommand : AutoLoadCommand
    {
        public TimeCommand(Monitor monitor) : base(monitor, "currentTime", "prints out and logs the current emulation virtual and real time")
        {
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            var output = $"Current virtual time: {EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime}\nCurrent real time: {EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedHostTime}";
            writer.WriteLine(output);
            Logger.LogAs(monitor, LogLevel.Info, output);
        }
    }
}