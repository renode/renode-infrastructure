//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    public class ThreadAttachedCommand : Command, IMultithreadCommand
    {
        public ThreadAttachedCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("qAttached")]
        public PacketData Execute()
        {
            // return information to gdb that it has successfully attached to an existing process
            return new PacketData("1");
        }
    }
}
