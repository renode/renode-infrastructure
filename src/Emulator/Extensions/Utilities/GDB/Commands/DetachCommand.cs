//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class DetachCommand : Command, IMultithreadCommand
    {
        public DetachCommand(CommandsManager manager) : base(manager)
        {
        }

        // This command is only there so that GDB doesn't report that the
        // "Remote doesn't know how to detach". It doesn't need to do anything,
        // the actual detaching/cleanup is done when the connection is closed.
        [Execute("D")]
        public PacketData Execute()
        {
            return PacketData.Success;
        }

        [Execute("D;")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] int pid)
        {
            manager.ManagedCpus.AttachedProcesses.Remove(pid);
            return PacketData.Success;
        }
    }
}
