//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class MultithreadContinueCommand : Command, IMultithreadCommand
    {
        public MultithreadContinueCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("vCont;")]
        public PacketData Continue([Argument(Encoding = ArgumentAttribute.ArgumentEncoding.String)]string data)
        {
            ProcessCommandData(data);
            return null;
        }

        [Execute("vCont?")]
        public PacketData GetSupportedActions()
        {
            // packets `C` is deprecated for multi-threading support, but at the same time it is required in a valid reply 
            return new PacketData("vCont;c;C;s");
        }

        private void ProcessCommandData(string data)
        {
            // ATM we support only all-stop mode, so if we receive a `step` request in the packet we skip the `continue` request
            var skipContinue = data.Contains('s') && data.Contains('c');
            var cpuIdsToHandle = new HashSet<uint>(manager.ManagedCpus.Keys);
            foreach(var pair in data.Split(';'))
            {
                // If no coreId is provided use `-1` as an indicator of all cores
                var coreId = -1;
                var operation = pair.Split(':');
                if(pair.Length > 1)
                {
                    coreId = int.Parse(operation[1]);
                }
                if(skipContinue && operation[0] == "c")
                {
                    continue;
                }
                ManageOperation(operation[0], coreId, cpuIdsToHandle);
            }
        }

        private void ManageOperation(string operation, int coreId, HashSet<uint> managedCpuIds)
        {
            switch(operation)
            {
                case "c":
                    if(coreId == -1)
                    {
                        foreach(var id in managedCpuIds)
                        {
                            if(manager.ManagedCpus[id].IsHalted)
                            {
                                manager.ManagedCpus[id].IsHalted = false;
                            }
                            manager.ManagedCpus[id].ExecutionMode = ExecutionMode.Continuous;
                            manager.ManagedCpus[id].Resume();
                        }
                        managedCpuIds.Clear();
                    }
                    else if(coreId == 0)
                    {
                        if(managedCpuIds.Count == 0)
                        {
                            manager.Cpu.Log(LogLevel.Warning, "No CPUs available to execute {0} command", operation);
                            break;
                        }
                        var firstAvailable = managedCpuIds.First();
                        manager.ManagedCpus[firstAvailable].ExecutionMode = ExecutionMode.Continuous;
                        manager.ManagedCpus[firstAvailable].Resume();
                        managedCpuIds.Remove(firstAvailable);
                    }
                    else
                    {
                        manager.ManagedCpus[(uint)coreId].ExecutionMode = ExecutionMode.Continuous;
                        manager.ManagedCpus[(uint)coreId].Resume();
                        managedCpuIds.Remove((uint)coreId);
                    }
                    break;
                case "s":
                    if(coreId == -1)
                    {
                        foreach(var id in managedCpuIds)
                        {
                            manager.ManagedCpus[id].Step();
                        }
                        managedCpuIds.Clear();
                    }
                    else if(coreId == 0)
                    {
                        if(managedCpuIds.Count == 0)
                        {
                            manager.Cpu.Log(LogLevel.Warning, "No CPUs available to execute {0} command", operation);
                            break;
                        }
                        var firstAvailable = managedCpuIds.First();
                        manager.ManagedCpus[firstAvailable].Step();
                        managedCpuIds.Remove(firstAvailable);
                    }
                    else
                    {
                        manager.ManagedCpus[(uint)coreId].Step();
                        managedCpuIds.Remove((uint)coreId);
                    }
                    break;
                default:
                    manager.Cpu.Log(LogLevel.Info, "Encountered an unsupported operation in packet: {0}", operation);
                    break;
            }
        }
    }
}
