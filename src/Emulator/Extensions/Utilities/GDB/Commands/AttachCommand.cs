//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class AttachCommand(CommandsManager manager) : Command(manager)
    {
        [Execute("vAttach;")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] int pid)
        {
            var allPids = manager.ManagedCpus.All.Select(id => id.ProcessId).Distinct().ToArray();
            if(!allPids.Contains(pid))
            {
                return PacketData.ErrorReply($"Invalid PID: {pid}, valid PID values are: {Misc.PrettyPrintCollection(allPids)}");
            }

            var processThreads = manager.ManagedCpus.All.Where(id => id.ProcessId == pid).OrderBy(id => id.ThreadId).ToArray();
            if(!processThreads.Any())
            {
                return PacketData.ErrorReply($"PID: {pid} does not contain any threads");
            }

            var sendExplicitStopResponse = true;

            manager.ManagedCpus.AttachedProcesses.Add(pid);

            // In All-Stop mode all threads of the process have to be stopped during 'attach'
            foreach(var cpu in processThreads.Select(id => manager.ManagedCpus[id]))
            {
                if(!cpu.IsPaused && !cpu.HasAnyHaltingCondition)
                {
                    sendExplicitStopResponse = true;
                }
                cpu.Pause();
            }

            if(sendExplicitStopResponse)
            {
                // In All-Stop mode vAttach has to respond with a stop response
                // so send one explicitly if pausing this process's CPUs wouldn't
                // produce one (i.e. CPU are already paused or halted)
                manager.SelectCpuForDebugging(manager.ManagedCpus[processThreads[0]]);
                return PacketData.StopReply(GdbStub.TrapSignal, processThreads[0]);
            }
            return null;
        }
    }
}
