//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class ThreadContextCommand : Command, IMultithreadCommand
    {
        public ThreadContextCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("Hg")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.ThreadId)]PacketThreadId threadId)
        {
            var cpuId = threadId.ProcessId ?? threadId.ThreadId;
            if(cpuId == PacketThreadId.All)
            {
                // Choosing all isn't currently supported.
                return PacketData.ErrorReply(Error.OperationNotPermitted);
            }

            manager.SelectCpuForDebugging(manager.ManagedCpus[cpuId]);
            return PacketData.Success;
        }
    }
}
