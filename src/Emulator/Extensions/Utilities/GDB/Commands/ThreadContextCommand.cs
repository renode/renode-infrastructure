//
// Copyright (c) 2010-2019 Antmicro
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
        public PacketData Execute([Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint id)
        {
            manager.SelectCpuForDebugging(id);
            return new PacketData("OK");
        }
    }
}
