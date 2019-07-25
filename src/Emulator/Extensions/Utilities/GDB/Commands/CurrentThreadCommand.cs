//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
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
            return new PacketData(string.Format("QC{0}", manager.ManagedCpus.Single(x => x.Value == manager.Cpu).Key));
        }
    }
}
