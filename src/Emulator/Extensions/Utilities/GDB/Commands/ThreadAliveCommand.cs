//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class ThreadAliveCommand : Command, IMultithreadCommand
    {
        public ThreadAliveCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("T")]
        public PacketData Execute()
        {
            // return information to gdb that the thread is still alive
            return new PacketData("OK");
        }
    }
}
