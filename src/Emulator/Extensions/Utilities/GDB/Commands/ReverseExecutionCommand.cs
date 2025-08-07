//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReverseExecutionCommand : Command
    {
        public ReverseExecutionCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("bs")]
        public PacketData ReverseStep()
        {
            if(!CheckIfPossible(out string message))
            {
                Logger.LogAs(this, LogLevel.Warning, "Reverse execution not possible: {0}", message);
                return PacketData.ErrorReply();
            }

            try
            {
                var executedInstructions = manager.Cpu.ExecutedInstructions;
                manager.LoadLatestSnapshot((CommandsManager newManager) =>
                {
                    EmulationManager.Instance.CurrentEmulation.StartAll();
                    var instructionsToExecute = executedInstructions - newManager.Cpu.ExecutedInstructions - 1;
                    var inDebugMode = newManager.Cpu.ShouldEnterDebugMode;
                    newManager.Cpu.ShouldEnterDebugMode = false;
                    newManager.Cpu.Step((int)instructionsToExecute);
                    newManager.Cpu.ShouldEnterDebugMode = inDebugMode;
                });
                return null;
            }
            catch(RecoverableException e)
            {
                Logger.LogAs(this, LogLevel.Warning, "Reverse execution not possible: {0}", e.Message);
                return PacketData.ErrorReply();
            }
        }

        private bool CheckIfPossible(out string message)
        {
            if(manager.Cpu.ExecutedInstructions <= 0)
            {
                message = "There are no instructions to revert to";
                return false;
            }
            if(manager.Machine.Profiler != null)
            {
                message = "Profiling not supported";
                return false;
            }
            message = null;
            return true;
        }
    }
}