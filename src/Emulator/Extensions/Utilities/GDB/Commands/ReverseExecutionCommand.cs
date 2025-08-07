//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReverseExecutionCommand : Command
    {
        public ReverseExecutionCommand(CommandsManager manager) : base(manager)
        {
            notPossible = PacketData.ErrorReply("Reverse execution not possible");
        }

        [Execute("bs")]
        public PacketData ReverseStep()
        {
            if(!CheckIfPossible(out string message))
            {
                Logger.LogAs(this, LogLevel.Warning, "Reverse execution not possible: {0}", message);
                return notPossible;
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
                return notPossible;
            }
        }

        [Execute("bc")]
        public PacketData ReverseContinue()
        {
            var inDebugMode = manager.Cpu.ShouldEnterDebugMode;
            if(!CheckIfPossible(out string message))
            {
                Logger.LogAs(this, LogLevel.Warning, "Reverse execution not possible: {0}", message);
                return notPossible;
            }

            try
            {
                var commandsManager = manager;

                var breakpoints = commandsManager.Breakpoints;
                var watchpoints = commandsManager.Watchpoints;
                var hooksAddresses = breakpoints.Select(x => x.Item1).ToList();

                Logger.LogAs(this, LogLevel.Debug, "Reverse continuing. Executed Instructions: {0}, searching for breakpoints at: {1}", commandsManager.Cpu.ExecutedInstructions, string.Join(", ", hooksAddresses.Select(x => x.ToString("X"))));

                // GDB hooks are still alive, we need to remove them so they don't halt the CPU
                commandsManager.RemoveAllBreakpoints();

                var foundExecutedInstructions = default(ulong);
                var foundBreakpointAddress = default(ulong);
                var found = false;
                do
                {
                    var executedInstructions = commandsManager.Cpu.ExecutedInstructions;
                    var time = EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime - TimeInterval.FromTicks(1);
                    Logger.LogAs(this, LogLevel.Debug, "Loading snapshot with max virtual time: {0}", time);
                    commandsManager.LoadLatestSnapshot(time, newManager => commandsManager = newManager);

                    // We disable AutoSnapshotCreator here to make sure it does not create any new state during search.
                    // It is not re-enabled because same snapshot is reloaded at the end of the loop anyway.
                    EmulationManager.Instance.CurrentEmulation.AutoSnapshotCreator.DisableSnapshotCreator();
                    commandsManager.Cpu.ShouldEnterDebugMode = false;
                    commandsManager.Cpu.ExecutionMode = ExecutionMode.Continuous;

                    // Re-run period to find the last breakpoint. In the next line snapshot is reloaded,
                    // if a breakpoint is found, we can exit the loop and run to last ExecutedInstruction which triggered the breakpoint
                    commandsManager.RemoveAllBreakpoints();
                    found = TryRunForToFindLastBreakpoint(commandsManager.Cpu, EmulationManager.Instance.CurrentEmulation.AutoSnapshotCreator.Period, executedInstructions, hooksAddresses, out foundExecutedInstructions, out foundBreakpointAddress);

                    commandsManager.LoadLatestSnapshot(time, newManager => commandsManager = newManager);
                } while(!found && EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime.Ticks > 0);

                commandsManager.RemoveAllBreakpoints();
                if(found)
                {
                    Logger.LogAs(this, LogLevel.Debug, "Snapshot found with PC: {0}", foundExecutedInstructions);

                    commandsManager.Cpu.ExecutionMode = ExecutionMode.Continuous;
                    commandsManager.Cpu.ShouldEnterDebugMode = false;
                    if(!TryRunToBreakpoint(commandsManager.Cpu, foundExecutedInstructions, foundBreakpointAddress))
                    {
                        return LogAndErrorReply(LogLevel.Error, "Unexpected state encountered, execution may not have stopped at the expected breakpoint");
                    }
                }

                Logger.LogAs(this, LogLevel.Info, "Reverse continued to PC: {0}", commandsManager.Cpu.PC);
                commandsManager.Cpu.ExecutionMode = ExecutionMode.SingleStep;
                commandsManager.Cpu.ShouldEnterDebugMode = inDebugMode;
                foreach(var breakpoint in breakpoints)
                {
                    commandsManager.AddBreakpoint(breakpoint.Item1, breakpoint.Item2);
                }
                foreach(var watchpoint in watchpoints)
                {
                    commandsManager.AddWatchpoint(watchpoint.Key, watchpoint.Value);
                }

                EmulationManager.Instance.CurrentEmulation.StartAll();
                if(found)
                {
                    return null;
                }
                else
                {
                    return LogAndErrorReply(LogLevel.Warning, "Reverse executed to the beginning of the Emulation");
                }
            }
            catch(RecoverableException e)
            {
                Logger.LogAs(this, LogLevel.Warning, "Reverse execution not possible: {0}", e.Message);
                EmulationManager.Instance.CurrentEmulation.StartAll();
                return notPossible;
            }
        }

        private PacketData LogAndErrorReply(LogLevel logLevel, string message)
        {
            Logger.LogAs(this, logLevel, message);
            return PacketData.ErrorReply(message);
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

        private bool TryRunToBreakpoint(ICpuSupportingGdb cpu, ulong minInstructions, ulong stopAddress)
        {
            var targetHitEvent = new ManualResetEventSlim();

            CpuAddressHook hook = (_,addr) =>
            {
                if(cpu.ExecutedInstructions >= minInstructions)
                {
                    cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, addr, BreakpointType.MemoryBreakpoint));
                    targetHitEvent.Set();
                }
            };

            cpu.AddHook(stopAddress, hook);
            EmulationManager.Instance.CurrentEmulation.StartAll();
            targetHitEvent.Wait();
            cpu.RemoveHook(stopAddress, hook);

            if(cpu.ExecutedInstructions > minInstructions)
            {
                Logger.LogAs(this, LogLevel.Error, "CPU executed more instructions than requested");
                return false;
            }

            return true;
        }

        private bool TryRunForToFindLastBreakpoint(ICpuSupportingGdb cpu, TimeInterval period, ulong maxInstructions, IEnumerable<ulong> breakpoints, out ulong foundExecutedInstructions, out ulong foundBreakpointAddress)
        {
            var localFoundExecutedInstructions = default(ulong);
            var localFoundBreakpointAddress = default(ulong);
            var found = false;

            CpuAddressHook hook = (_, address) =>
            {
                var executedInstructions = cpu.ExecutedInstructions;
                if(executedInstructions >= maxInstructions)
                {
                    cpu.RemoveAllHooks();
                    return;
                }
                localFoundExecutedInstructions = executedInstructions;
                localFoundBreakpointAddress = address;
                found = true;
            };

            foreach(var address in breakpoints)
            {
                cpu.AddHook(address, hook);
            }

            EmulationManager.Instance.CurrentEmulation.RunFor(period);

            foundExecutedInstructions = localFoundExecutedInstructions;
            foundBreakpointAddress = localFoundBreakpointAddress;
            return found;
        }

        private readonly PacketData notPossible;
    }
}