//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
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
        public PacketData Continue(
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string operation1,
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.DecimalNumber)]int coreId1 = -1,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string operation2 = "",
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.DecimalNumber)]int coreId2 = -1
            )
        {
            ProcessOperation(operation1, coreId1);
            if(operation2 != "")
            {
                ProcessOperation(operation2, coreId2);
            }
            return null;
        }

        [Execute("vCont?")]
        public PacketData GetSupportedActions()
        {
            // packets `C` is deprecated for multi-threading support, but at the same time it is required in a valid reply 
            return new PacketData("vCont;c;C;s");
        }

        private void ProcessOperation(string operation, int coreId)
        {
            switch(operation)
            {
                case "c":
                    if(coreId == -1)
                    {
                        foreach(var cpu in manager.ManagedCpus.Values)
                        {
                            cpu.ExecutionMode = ExecutionMode.Continuous;
                            cpu.Resume();
                        }
                    }
                    else if(coreId == 0)
                    {
                        manager.Cpu.ExecutionMode = ExecutionMode.Continuous;
                        manager.Cpu.Resume();
                    }
                    else
                    {
                        manager.ManagedCpus[(uint)coreId].ExecutionMode = ExecutionMode.Continuous;
                        manager.ManagedCpus[(uint)coreId].Resume();
                    }
                    break;
                case "s":
                    if(coreId == -1)
                    {
                        foreach(var cpu in manager.ManagedCpus.Values)
                        {
                            cpu.Step();
                        }
                    }
                    else if(coreId == 0)
                    {
                        manager.Cpu.Step();
                    }
                    else
                    {
                        manager.ManagedCpus[(uint)coreId].Step();
                    }
                    break;
                default:
                    manager.Cpu.Log(LogLevel.Info, "Encountered an unsupported operation in packet: {0}", operation);
                    break;
            }
        }
    }
}
