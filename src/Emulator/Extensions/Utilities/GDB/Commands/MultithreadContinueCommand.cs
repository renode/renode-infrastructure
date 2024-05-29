//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.GDB;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class MultithreadContinueCommand : Command, IMultithreadCommand
    {
        public MultithreadContinueCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("vCont?")]
        public PacketData GetSupportedActions()
        {
            // packets `C` is deprecated for multi-threading support, but at the same time it is required in a valid reply
            return new PacketData("vCont;c;C;s");
        }

        [Execute("vCont;")]
        public PacketData Continue([Argument(Encoding = ArgumentAttribute.ArgumentEncoding.String)]string data)
        {
            if(!TryParseData(data, out var operations))
            {
                return PacketData.ErrorReply();
            }

            if(EmulationManager.Instance.CurrentEmulation.SingleStepBlocking)
            {
                if(!TryHandleBlockingExecution(ref operations))
                {
                    return PacketData.ErrorReply();
                }
            }

            foreach(var operation in operations)
            {
                ManageOperation(operation);
            }

            return null;
        }

        private bool TryHandleBlockingExecution(ref IEnumerable<Operation> operations)
        {
            /* This method solves problem of blocking, i.e.:
             *  Given two cores A and B and time intervals of their time source T(n) (current time interval), where:
             *   - core A reports finishing T(n) and wants to obtain T(n+1), and
             *   - core B has some work left to execute in T(n).
             *  Time source won't grant T(n+1) until all of its sinks report finishing T(n).
             *  Calling Step on core A before having core B finish T(n) would block.
             *  To avoid such a scenario:
             *   1) `operations` are split into:
             *     - cores, similar to core B, that have some work left - `blocking`
             *     - cores, similar to core A, that have to obtian new time interval - `blocked`
             *   2) cores in `blocking` are resumed to do available work
             *   3) if any of those cores has some work left to execute in current time interval then consume the time left for those cores
             *  At this point all cores have finished current time interval and it's safe to perform Step for cores from `blocked`.
             */
            // step 1)
            var blocking = operations.Where(operation => !manager.ManagedCpus[operation.CoreId].TimeHandle.IsDone).ToList();
            var blocked = operations.Where(operation => manager.ManagedCpus[operation.CoreId].TimeHandle.IsDone).ToList();

            // if there is no operation that would block then return and execute `operations` in any order
            var blockedStep = blocked.Where(operation => operation.Type == OperationType.Step).ToList();
            if(!blockedStep.Any())
            {
                // we support only all-stop mode, skip `continue` requests if we got any `step` request
                var stepping = blocking.Where(operation => operation.Type == OperationType.Step).ToList();
                if(stepping.Any())
                {
                    operations = stepping;
                }
                return true;
            }
            // preempt cores from waiting on time request and switch to SingleStep mode
            // this helps to keep control of the cores as none of them will successfully obtain a new time interval
            foreach(var operation in operations)
            {
                manager.ManagedCpus[operation.CoreId].ExecutionMode = ExecutionMode.SingleStep;
                manager.ManagedCpus[operation.CoreId].TimeHandle.DelayGrant = true;
            }

            // we support only all-stop mode, skip `continue` requests
            operations = blockedStep;
            // abort is set when core fails to stop at the sync-point in the callbacks
            var abort = false;
            var blockingContinue = blocking.Where(operation => operation.Type == OperationType.Continue).ToList();
            var cde = new CountdownEvent(blockingContinue.Count());

            // a part of step 3)
            // make sure that cores with Continue reach the sync-point
            // pause execution at sync-point or skip time if core stopped before sync-point
            foreach(var operation in blockingContinue)
            {
                var callbacks = new Dictionary<uint, Action>();
                var cpu = manager.ManagedCpus[operation.CoreId];
                Action callback = () =>
                {
                    var isDone = cpu.TimeHandle.IsDone;
                    // if core didn't stop at a breakpoint (would change ExecutionMode) or didn't reach the sync-point then let it continue
                    if(cpu.ExecutionMode == ExecutionMode.Continuous && !isDone)
                    {
                        return;
                    }
                    cpu.TimeHandle.ReportedBack -= callbacks[operation.CoreId];
                    cpu.ExecutionMode = ExecutionMode.SingleStep;
                    // skip time if core stopped before sync-point
                    if(!isDone)
                    {
                        if(!cpu.TimeHandle.TrySkipToSyncPoint(out var interval))
                        {
                            cpu.Log(LogLevel.Error, "Aborted execution of gdb command, core #{0} would block.", operation.CoreId);
                            abort = true;
                            return;
                        }
                        cpu.Log(LogLevel.Warning, "Jumped {0}s in time.", interval);
                    }
                    cde.Signal();
                };
                cpu.TimeHandle.ReportedBack += callback;
                callbacks.Add(operation.CoreId, callback);
            }
            // step 2)
            // run blocking cores
            foreach(var operation in blocking)
            {
                ManageOperation(operation);
            }
            // the rest of step 3)
            // wait for the blocking cores to get to the sync-point
            cde.Wait();
            if(abort)
            {
                return false;
            }
            foreach(var operation in blocking.Where(operation => operation.Type != OperationType.Continue))
            {
                var cpu = manager.ManagedCpus[operation.CoreId];
                // skip time if core didn't get to the sync-point
                if(!cpu.TimeHandle.IsDone)
                {
                    if(!cpu.TimeHandle.TrySkipToSyncPoint(out var interval))
                    {
                        cpu.Log(LogLevel.Error, "Aborted execution of gdb command, core #{0} would block.", operation.CoreId);
                        abort = true;
                    }
                    cpu.Log(LogLevel.Warning, "Jumped {0}s in time.", interval);
                }
            }
            return true;
        }

        private bool TryParseData(string data, out IEnumerable<Operation> operations)
        {
            operations = Enumerable.Empty<Operation>();
            var operationsList = new List<Operation>();
            // ATM we support only all-stop mode, so if we receive a `step` request in the packet we skip the `continue` request,
            // but don't skip for synchronus execution, its needed in TryHandleBlockingExecution
            var skipContinue = !EmulationManager.Instance.CurrentEmulation.SingleStepBlocking && data.Contains('s') && data.Contains('c');
            var gdbCpuIdsToHandle = new HashSet<uint>(manager.ManagedCpus.GdbCpuIds);
            foreach(var pair in data.Split(';'))
            {
                // No id means that command should be applied to the rest of the threads
                var coreId = AllCores;
                var operation = pair.Split(':');
                if(pair.Length > 1)
                {
                    coreId = int.Parse(operation[1]);
                }

                var type = Operation.ParseType(operation[0]);
                if(!type.HasValue)
                {
                    manager.Cpu.Log(LogLevel.Error, "Encountered an unsupported operation in command: \"{0}\".", pair);
                    return false;
                }
                if(skipContinue && type == OperationType.Continue)
                {
                    continue;
                }

                if(coreId == AllCores)
                {
                    foreach(var id in gdbCpuIdsToHandle)
                    {
                        operationsList.Add(new Operation(id, type.Value));
                    }
                    gdbCpuIdsToHandle.Clear();
                }
                else if(coreId == AnyCore)
                {
                    if(gdbCpuIdsToHandle.Count == 0)
                    {
                        manager.Cpu.Log(LogLevel.Error, "No CPUs available to execute \"{0}\" command.", pair);
                        return false;
                    }
                    var firstAvailable = gdbCpuIdsToHandle.First();
                    operationsList.Add(new Operation(firstAvailable, type.Value));
                    gdbCpuIdsToHandle.Remove(firstAvailable);
                }
                else
                {
                    // coreId has proper core id
                    if(!gdbCpuIdsToHandle.Remove((uint)coreId))
                    {
                        var index = operationsList.FindIndex(op => op.CoreId == (uint)coreId);
                        if(index != -1)
                        {
                            manager.Cpu.Log(LogLevel.Error, "CPU #{1} already set to {2}, error in \"{0}\" command.", pair, coreId, operationsList[index].Type);
                        }
                        else
                        {
                            manager.Cpu.Log(LogLevel.Error, "Invalid CPU id in \"{0}\" command.", pair);
                        }
                        return false;
                    }
                    operationsList.Add(new Operation((uint)coreId, type.Value));
                }
            }
            if(!operationsList.Any())
            {
                manager.Cpu.Log(LogLevel.Error, "No actions specified.");
                return false;
            }
            foreach(var id in gdbCpuIdsToHandle)
            {
                operationsList.Add(new Operation(id, manager.ManagedCpus[id].ExecutionMode == ExecutionMode.Continuous ? OperationType.Continue : OperationType.None));
            }
            operations = operationsList;
            return true;
        }

        private void ManageOperation(Operation operation)
        {
            var cpu = manager.ManagedCpus[operation.CoreId];
            switch(operation.Type)
            {
                case OperationType.Continue:
                    cpu.ExecutionMode = ExecutionMode.Continuous;
                    cpu.Resume();
                    break;
                case OperationType.Step:
                    cpu.Step(1);
                    break;
                case OperationType.None:
                    break;
                default:
                    cpu.Log(LogLevel.Info, "Encountered an unsupported operation.");
                    break;
            }
        }

        private const int AllCores = PacketThreadId.All;
        private const int AnyCore = PacketThreadId.Any;

        private struct Operation
        {
            public Operation(uint id, OperationType type) : this()
            {
                this.CoreId = id;
                this.Type = type;
            }

            public static OperationType? ParseType(string str)
            {
                switch(str)
                {
                    case "c":
                        return OperationType.Continue;
                    case "s":
                        return OperationType.Step;
                    default:
                        return null;
                }
            }

            public uint CoreId { get; }
            public OperationType Type { get; }
        }

        private enum OperationType
        {
            Continue,
            Step,
            None,
        }
    }
}
