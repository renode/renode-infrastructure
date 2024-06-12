//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class CurrentThreadCommand : Command, IMultithreadCommand
    {
        public CurrentThreadCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("qC")]
        public PacketData Execute()
        {
            return new PacketData(string.Format("QC{0:x}", manager.ManagedCpus[manager.Cpu]));
        }
    }
}
