//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class ReverseExecModeCommand : AutoLoadCommand
    {
        public ReverseExecModeCommand(Monitor monitor) : base(monitor, "reverseExecMode", "enables gdb reverse execution from current emulation state")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine($"Current value: {EmulationManager.Instance.CurrentEmulation.ReverseExecutionEnabled}");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableReverseExecution)
        {
            EmulationManager.Instance.CurrentEmulation.ReverseExecutionEnabled = enableReverseExecution.Value;
            AutoSaveCommand.SetAutoSave(writer, enableReverseExecution.Value);
        }
    }
}